using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// A dev HUD readout of the park's live state — the bank balance and the income/cost flows driven by
/// peeps and rides (see <see cref="ParkFinances"/>), plus the visitor count. Renders nothing until a
/// park economy exists, so it is harmless in the plain lobby. Text only; no background.
/// </summary>
internal sealed class ParkStatsPanel : HudPanel
{
	private static Texture? bg, track, ratGood, ratMid, ratBad;
	private static Texture Bg => bg ??= Solid( 16, 16, 26, 170 );
	private static Texture Track => track ??= Solid( 50, 50, 62, 220 );
	private static Texture RatGood => ratGood ??= Solid( 80, 185, 90, 240 );
	private static Texture RatMid => ratMid ??= Solid( 210, 180, 60, 240 );
	private static Texture RatBad => ratBad ??= Solid( 200, 70, 60, 240 );

	private const float Scale = 1.5f;
	private const float Left = 30f;   // top-left margin (UI space is Y-up, origin bottom-left)
	private const float Top = 655f; // sits below the FPS counter in the top-left corner
	private const float LineStep = 28f;

	protected override void OnRender()
	{
		if ( ParkFinances.Current is not { } fin )
			return;

		int visitors = Entity.All.Count( e => e is Peep );
		int staff = Entity.All.Count( e => e is Staff );

		// One line per stat, stepping down from the top-left.
		var lines = new List<string>
		{
			$"MONEY {fin.Money:0}",
			$"TICKETS {fin.RideRevenue:0}",
			$"GATE {fin.EntryRevenue:0}",
			$"FOOD {fin.FoodRevenue:0}",
			$"UPKEEP {fin.UpkeepPaid:0}",
			$"WAGES {fin.WagesPaid:0}",
			$"VISITORS {visitors}   STAFF {staff}",
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
			if ( build.LayingTrack )
				lines.Add( build.TrackClosed
					? $"TRACK: {build.TrackSegments} segs - LOOP CLOSED (B back, T done)"
					: $"TRACK: {build.TrackSegments} segs (click lay, B back, T done)" );
			else if ( build.SelectedRide is { Shape.HasTrack: true } )
				lines.Add( "T: lay coaster track" );

			if ( build.SelectedStaff is { } staffSel )
				lines.Add( staffSel.HasPatrolZone
					? $"STAFF {staffSel.Role}  zone r{staffSel.Zone!.Value.Radius:0} (FIRE/ZONE buttons)"
					: $"STAFF {staffSel.Role}  free roam (FIRE/SET ZONE buttons)" );

			if ( build.SelectedRide is { } sel )
			{
				lines.Add( $"RIDE {sel.Name} L{sel.UpgradeLevel} cap {sel.Capacity}  price {sel.TicketPrice:0} (,.)" );
				if ( sel.IsResearching )
					lines.Add( $"  researching... {sel.ResearchFraction * 100f:0}%" );
				else if ( sel.NextResearched )
					lines.Add( $"  U: upgrade ${sel.NextUpgradeCost:0} -> cap {sel.Upgrades[sel.UpgradeLevel + 1].Capacity}" );
				else if ( sel.HasNextLevel )
					lines.Add( $"  R: research next ${sel.NextResearchCost:0}" );
				else
					lines.Add( "  (max upgrade level)" );
			}

			// The catalog itself is the clickable BuildPanel (right side); here just a hint + the
			// selected item, so the two panels don't duplicate the whole list.
			if ( build.Selected >= 0 && build.Selected < build.Catalog.Count )
			{
				var placing = build.Catalog[build.Selected];
				lines.Add( $"PLACING: {placing.Name} (click a tile, Esc cancel)" );
				if ( placing.RidePath != null )
					lines.Add( $"  ROTATE (R): {build.Rotation * 90} deg" );
			}
			else
				lines.Add( "BUILD: pick an item on the right -->" );
		}

		// Park rating = average visitor happiness (T-039); shown as a coloured gauge under the stats.
		float rating = Peep.AverageHappiness;
		if ( rating >= 0f )
			lines.Add( $"PARK RATING {rating:0}%" );

		// Translucent backing so the text reads over the bright park; covers the lines + the rating bar.
		int n = lines.Count;
		float blockTop = Top + 8f;
		float lastLineTop = Top - (n - 1) * LineStep;
		float barH = 12f;
		float barTop = lastLineTop - 20f; // just below the PARK RATING text line
		float backBottom = barTop - barH - 8f;
		var mat = Material.UI;
		mat.Set( "Color", Bg );
		Graphics.Quad( new Rectangle( Left - 12f, backBottom, 392f, blockTop - backBottom ), mat );

		for ( int i = 0; i < lines.Count; i++ )
			Graphics.DrawText( Font, lines[i], Left, Top - i * LineStep, TextAlign.Left, Scale );

		if ( rating >= 0f )
		{
			var fill = rating >= 66f ? RatGood : rating >= 33f ? RatMid : RatBad;
			DrawBar( new Rectangle( Left, barTop - barH, 364f, barH ), rating / 100f, fill, Track );
		}
	}
}
