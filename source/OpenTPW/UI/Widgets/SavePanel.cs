using System.Numerics;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Save/load slot menu (T-061): a centred overlay listing the <see cref="SaveGame.SlotCount"/> save slots, each
/// with a one-line summary of the saved park (money, in-game date, ride/shop/visitor counts via
/// <see cref="SaveGame.Summary"/>) and per-slot <b>SAVE</b> / <b>LOAD</b> buttons. Toggled with F8; hidden by
/// default, so it costs nothing until opened. Builds on the keyboard slots from T-059 (F5/F9/F6) by letting the
/// player see what's in a slot before overwriting/loading it. Shares the mouse-mapping / fill helpers with the
/// other <see cref="HudPanel"/>s.
/// </summary>
internal sealed class SavePanel : HudPanel
{
	public static SavePanel? Current { get; private set; }

	public SavePanel() => Current = this;

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	private bool open;
	private bool wasToggleDown;

	// Cached per-slot saves (null = empty/unreadable), so the menu doesn't re-read the files every frame; it's
	// refreshed when the menu opens and after a save writes a slot.
	private readonly SaveGame?[] slots = new SaveGame?[SaveGame.SlotCount];

	// Panel geometry in the fixed 1280×720 base space (Y-up, origin bottom-left).
	private const float PanelX = 360f, PanelW = 560f, PanelH = 70f + SaveGame.SlotCount * RowH;
	private const float RowH = 54f;
	private const float BtnW = 86f, BtnH = 30f;

	private static Rectangle PanelBounds() => new( PanelX, 700f - PanelH, PanelW, PanelH );

	private static Texture? bg, save, load, dim, empty;
	private static Texture Bg => bg ??= Solid( 18, 18, 28, 230 );
	private static Texture SaveBtn => save ??= Solid( 70, 130, 80, 240 );
	private static Texture LoadBtn => load ??= Solid( 70, 100, 160, 240 );
	private static Texture Dim => dim ??= Solid( 55, 55, 62, 235 );      // disabled (empty slot's LOAD)
	private static Texture EmptyRow => empty ??= Solid( 30, 30, 40, 180 );

	private readonly record struct Btn( Rectangle Rect, string Label, Texture Fill, bool Enabled, Action Act );

	/// <summary>True when the menu is open and the cursor is over it (so world tools ignore the click).</summary>
	public bool ContainsMouse() => open && Contains( PanelBounds(), MouseBase() );

	private void Refresh()
	{
		for ( int i = 0; i < slots.Length; i++ )
			slots[i] = SaveGame.ReadFromFile( SaveGame.SlotPath( i + 1 ) );
	}

	// The Y (bottom) of slot row i, stepping down from just under the title.
	private static float RowY( int i ) => 700f - PanelH + PanelH - 56f - i * RowH;

	// Build the live button list (shared by render + click handling so they never drift).
	private List<Btn> Buttons()
	{
		var list = new List<Btn>();
		for ( int i = 0; i < SaveGame.SlotCount; i++ )
		{
			int slot = i + 1;                 // slots are 1-based on disk
			float y = RowY( i );
			bool filled = slots[i] != null;
			list.Add( new Btn( new Rectangle( PanelX + PanelW - 2 * BtnW - 28f, y, BtnW, BtnH ), "SAVE", SaveBtn, true, () =>
			{
				Level.CaptureSave().WriteToFile( SaveGame.SlotPath( slot ) );
				Refresh();
			} ) );
			list.Add( new Btn( new Rectangle( PanelX + PanelW - BtnW - 16f, y, BtnW, BtnH ), "LOAD", filled ? LoadBtn : Dim, filled, () =>
			{
				if ( SaveGame.ReadFromFile( SaveGame.SlotPath( slot ) ) is { } g )
					Level.ApplySave( g );
			} ) );
		}
		return list;
	}

	protected override void OnUpdate()
	{
		// F8 toggles the menu (edge-detected on the raw key, like the other overlays).
		var toggle = Input.Keyboard.KeysDown.Contains( Key.F8 );
		if ( toggle && !wasToggleDown )
		{
			open = !open;
			if ( open )
				Refresh();
		}
		wasToggleDown = toggle;

		if ( !open || !Input.MouseLeftPressed )
			return;

		var m = MouseBase();
		foreach ( var b in Buttons() )
		{
			if ( !b.Enabled || !Contains( b.Rect, m ) )
				continue;
			b.Act();
			break;
		}
	}

	protected override void OnRender()
	{
		if ( !open )
			return;

		var bounds = PanelBounds();
		var mat = Material.UI;
		mat.Set( "Color", Bg );
		Graphics.Quad( bounds, mat );

		Graphics.DrawText( Font, "SAVE / LOAD", bounds.X + 16f, bounds.Y + bounds.Height - 24f, TextAlign.Left, 1.5f );

		for ( int i = 0; i < SaveGame.SlotCount; i++ )
		{
			float y = RowY( i );
			mat.Set( "Color", EmptyRow );
			Graphics.Quad( new Rectangle( PanelX + 12f, y - 6f, PanelW - 24f, RowH - 10f ), mat );

			string summary = slots[i]?.Summary() ?? "- empty -";
			Graphics.DrawText( Font, $"SLOT {i + 1}", PanelX + 22f, y + BtnH - 8f, TextAlign.Left, LabelScale );
			Graphics.DrawText( Font, summary, PanelX + 22f, y + 2f, TextAlign.Left, 1.0f );
		}

		foreach ( var b in Buttons() )
			DrawButton( b.Rect, b.Label, b.Fill );

		Graphics.DrawText( Font, "click SAVE/LOAD - F8 to close", bounds.X + 16f, bounds.Y + 8f, TextAlign.Left, 1.0f );
	}
}
