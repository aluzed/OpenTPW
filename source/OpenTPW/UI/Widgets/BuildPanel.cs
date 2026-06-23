using System.Numerics;
using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// The clickable build/manage catalog (T-038): a vertical list of buttons down the right side, one per
/// <see cref="BuildMode"/> catalog item, that selects the item to place on click — so every item is
/// mouse-reachable (the old number-key list capped at 1–9). The selected item is highlighted; items the
/// park can't afford are dimmed red. Renders nothing outside build mode (e.g. the plain lobby).
///
/// <para>UI is drawn in the fixed 1280×720 base space (Y-up, origin bottom-left), like the other widgets;
/// the window mouse is mapped into that space for hit-testing. <see cref="BuildMode"/> consults
/// <see cref="ContainsMouse"/> so a click on the panel doesn't also act on the tile behind it.</para>
/// </summary>
internal sealed class BuildPanel : Panel
{
	public static BuildPanel? Current { get; private set; }

	private static Font? font;
	private static Font Font => font ??= new Font( "Language/English/GAME12.bf4" );

	// Layout in 1280×720 base space.
	private const float PanelW = 250f;
	private const float Left = 1280f - PanelW - 12f; // right-anchored column
	private const float Right = Left + PanelW;
	private const float TopY = 690f;     // y (Y-up) of the title row top
	private const float RowH = 30f;
	private const float RowGap = 4f;
	private const float FirstRowTop = TopY - 34f; // first button top, below the title
	private const float LabelScale = 1.3f;

	// Cached solid-colour fills (built once) so rendering allocates nothing per frame.
	private static Texture? bg, normal, selected, unaffordable;
	private static Texture Bg => bg ??= Solid( 18, 18, 28, 190 );
	private static Texture Normal => normal ??= Solid( 58, 58, 92, 225 );
	private static Texture Selected => selected ??= Solid( 80, 165, 90, 240 );
	private static Texture Unaffordable => unaffordable ??= Solid( 95, 48, 48, 215 );

	private static Texture Solid( byte r, byte g, byte b, byte a ) => new( [r, g, b, a], 1, 1 );

	public BuildPanel() => Current = this;

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	// The button rect (base space) for catalog row i: X,Y = bottom-left.
	private static Rectangle Row( int i )
	{
		var top = FirstRowTop - i * (RowH + RowGap);
		return new Rectangle( Left, top - RowH, PanelW, RowH );
	}

	private static int RowCount => BuildMode.Current?.Catalog.Count ?? 0;

	// Whole-panel bounds (title + all rows), used to block world clicks while the cursor is over the UI.
	private Rectangle PanelBounds()
	{
		var bottom = RowCount > 0 ? Row( RowCount - 1 ).Y - RowGap : TopY;
		return new Rectangle( Left - 6f, bottom, PanelW + 12f, TopY + 24f - bottom );
	}

	/// <summary>True when the window mouse (mapped to base space) is over the panel.</summary>
	public bool ContainsMouse()
	{
		if ( BuildMode.Current == null || RowCount == 0 )
			return false;
		return Contains( PanelBounds(), MouseBase() );
	}

	// Window mouse (pixels, Y-down) → base 1280×720 space (Y-up).
	private static Vector2 MouseBase()
	{
		var w = Screen.Width <= 0 ? 1f : Screen.Width;
		var h = Screen.Height <= 0 ? 1f : Screen.Height;
		return new Vector2(
			Input.Mouse.Position.X / w * 1280f,
			(1f - Input.Mouse.Position.Y / h) * 720f );
	}

	private static bool Contains( Rectangle r, Vector2 p )
		=> p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;

	protected override void OnUpdate()
	{
		if ( BuildMode.Current is not { } build || RowCount == 0 || !Input.MouseLeftPressed )
			return;

		var m = MouseBase();
		for ( var i = 0; i < build.Catalog.Count; i++ )
		{
			if ( !Contains( Row( i ), m ) )
				continue;
			build.SelectIndex( i ); // toggles off if already selected
			break;
		}
	}

	protected override void OnRender()
	{
		if ( BuildMode.Current is not { } build || build.Catalog.Count == 0 )
			return;

		var mat = Material.UI;
		var fin = ParkFinances.Current;

		// Panel backdrop.
		mat.Set( "Color", Bg );
		Graphics.Quad( PanelBounds(), mat );

		// Title.
		Graphics.DrawText( Font, "BUILD", Left + 8f, TopY + 20f, TextAlign.Left, LabelScale );

		for ( var i = 0; i < build.Catalog.Count; i++ )
		{
			var item = build.Catalog[i];
			var rect = Row( i );
			bool affordable = fin?.CanAfford( item.Cost ) ?? true;
			var fill = build.Selected == i ? Selected : affordable ? Normal : Unaffordable;

			mat.Set( "Color", fill );
			Graphics.Quad( rect, mat );

			// Label: "name $cost", left-aligned inside the button.
			Graphics.DrawText( Font, $"{item.Name}  ${item.Cost:0}", rect.X + 8f, rect.Y + RowH - 6f, TextAlign.Left, LabelScale );
		}
	}
}
