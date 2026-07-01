using System.Numerics;
using Veldrid;

namespace OpenTPW;

/// <summary>
/// Theme picker (T-062): a centred overlay listing the four park worlds (jungle / hallow / fantasy / space) with
/// the active one highlighted; clicking one reloads the park into that theme via <see cref="Level.RequestReload"/>
/// (a no-op on the current theme). Toggled with F12 — a discoverable, click-driven front end for the theme
/// switch that F7 cycles blindly. Hidden by default, so it costs nothing until opened. Shares the mouse-mapping /
/// fill helpers with the other <see cref="HudPanel"/>s.
/// </summary>
internal sealed class ThemePanel : HudPanel
{
	public static ThemePanel? Current { get; private set; }

	public ThemePanel() => Current = this;

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	// Starts closed; OPENTPW_THEME_PANEL=1 opens it on load (a diagnostic, so the picker can be seen without
	// needing a keypress to toggle F12).
	private bool open = System.Environment.GetEnvironmentVariable( "OPENTPW_THEME_PANEL" ) == "1";
	private bool wasToggleDown;

	// Panel geometry in the fixed 1280×720 base space (Y-up, origin bottom-left).
	private const float RowH = 46f;
	private static readonly int Count = LevelTheme.Known.Length;
	private const float PanelX = 470f, PanelW = 340f;
	private static float PanelH => 70f + Count * RowH;

	private static Rectangle PanelBounds() => new( PanelX, 380f, PanelW, PanelH );

	private static Texture? bg, active, other;
	private static Texture Bg => bg ??= Solid( 18, 18, 28, 235 );
	private static Texture ActiveRow => active ??= Solid( 70, 130, 90, 245 );   // the current world
	private static Texture OtherRow => other ??= Solid( 55, 60, 95, 240 );

	/// <summary>True when the menu is open and the cursor is over it (so world tools ignore the click).</summary>
	public bool ContainsMouse() => open && Contains( PanelBounds(), MouseBase() );

	// The Y (bottom) of theme row i, stepping down from just under the title.
	private static float RowY( int i ) => PanelBounds().Y + PanelH - 52f - i * RowH;

	protected override void OnUpdate()
	{
		var toggle = Input.Keyboard.KeysDown.Contains( Key.F12 );
		if ( toggle && !wasToggleDown )
			open = !open;
		wasToggleDown = toggle;

		if ( !open || !Input.MouseLeftPressed )
			return;

		var m = MouseBase();
		string current = Level.Current?.Name ?? LevelTheme.Default;
		for ( int i = 0; i < Count; i++ )
		{
			var rect = new Rectangle( PanelX + 16f, RowY( i ), PanelW - 32f, RowH - 10f );
			if ( !Contains( rect, m ) )
				continue;
			var theme = LevelTheme.Known[i];
			if ( theme != current )
			{
				open = false; // close as we start the reload
				Level.RequestReload( theme );
			}
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

		Graphics.DrawText( Font, "CHOOSE A WORLD", bounds.X + 16f, bounds.Y + bounds.Height - 24f, TextAlign.Left, 1.5f );

		string current = Level.Current?.Name ?? LevelTheme.Default;
		for ( int i = 0; i < Count; i++ )
		{
			var theme = LevelTheme.Known[i];
			bool isCurrent = theme == current;
			var rect = new Rectangle( PanelX + 16f, RowY( i ), PanelW - 32f, RowH - 10f );
			var label = LevelTheme.DisplayName( theme );
			DrawButton( rect, isCurrent ? $"{label}  (current)" : label, isCurrent ? ActiveRow : OtherRow );
		}

		Graphics.DrawText( Font, "click a world - F12 to close", bounds.X + 16f, bounds.Y + 8f, TextAlign.Left, 1.0f );
	}
}
