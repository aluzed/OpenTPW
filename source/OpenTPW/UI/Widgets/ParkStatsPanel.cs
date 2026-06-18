using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// A dev HUD readout of the park's live state — the bank balance and the income/cost flows driven by
/// peeps and rides (see <see cref="ParkFinances"/>), plus the visitor count. Renders nothing until a
/// park economy exists, so it is harmless in the plain lobby. Text only; no background.
/// </summary>
internal sealed class ParkStatsPanel : Panel
{
	// GAME12 is the 1bpp font the decoder reads cleanly (the antialiased menu fonts aren't decoded yet —
	// see PurpleButton), shared so the atlas is built once.
	private static Font? font;
	private static Font Font => font ??= new Font( "Language/English/GAME12.bf4" );

	private const float Scale = 1.5f;
	private const float Left = 30f;   // top-left margin (UI space is Y-up, origin bottom-left)
	private const float Top = 655f; // sits below the FPS counter in the top-left corner
	private const float LineStep = 28f;

	protected override void OnRender()
	{
		if ( ParkFinances.Current is not { } fin )
			return;

		int visitors = Entity.All.Count( e => e is Peep );

		// One line per stat, stepping down from the top-left.
		var lines = new List<string>
		{
			$"MONEY {fin.Money:0}",
			$"TICKETS {fin.RideRevenue:0}",
			$"GATE {fin.EntryRevenue:0}",
			$"FOOD {fin.FoodRevenue:0}",
			$"UPKEEP {fin.UpkeepPaid:0}",
			$"WAGES {fin.WagesPaid:0}",
			$"VISITORS {visitors}",
			$"LITTER {Litter.Active.Count}",
			$"ADMISSION {fin.EntryFee:0}  ([ ] adjust)",
		};
		if ( fin.Debt > 0 )
			lines.Add( $"DEBT {fin.Debt:0}  (L loan, K repay)" );
		else
			lines.Add( "LOAN: L=take  (no debt)" );
		if ( fin.Bankrupt )
			lines.Add( "*** BANKRUPT ***" );

		// Build palette (T-041): number keys pick an item to place, left-click places it.
		if ( BuildMode.Current is { } build )
		{
			if ( build.SelectedRide is { } sel )
				lines.Add( $"RIDE {sel.Name} price {sel.TicketPrice:0}  (, . adjust)" );

			lines.Add( "" );
			lines.Add( "BUILD (1-N pick, click place, 0/Esc cancel):" );
			for ( int i = 0; i < build.Catalog.Count; i++ )
			{
				var it = build.Catalog[i];
				string mark = build.Selected == i ? ">" : " ";
				lines.Add( $"{mark}[{i + 1}] {it.Name} ${it.Cost:0}" );
			}
		}

		for ( int i = 0; i < lines.Count; i++ )
			Graphics.DrawText( Font, lines[i], Left, Top - i * LineStep, TextAlign.Left, Scale );
	}
}
