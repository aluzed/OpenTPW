namespace OpenTPW;

/// <summary>
/// Clickable manage buttons (T-038): a bottom-left bar that exposes the park-economy and selected-ride
/// actions that were keyboard-only — admission fee ±, take/repay loan, and (when a ride is selected via
/// the Default tool) its ticket price ± and research/upgrade. Each button calls the same
/// <see cref="ParkFinances"/> / <see cref="Ride"/> methods the keyboard shortcuts do. Renders nothing
/// outside a park. Shares mouse/hit-test/button helpers with <see cref="HudPanel"/>.
/// </summary>
internal sealed class ManagePanel : HudPanel
{
	public static ManagePanel? Current { get; private set; }

	// Bottom-left bar in 1280×720 base space. Two rows: park economy (top) + selected ride (bottom).
	private const float EconY = 52f;  // bottom of the economy row buttons
	private const float RideY = 22f;  // bottom of the ride row buttons
	private const float BtnH = 26f;

	private static Texture? bg, normal, disabled, warn;
	private static Texture Bg => bg ??= Solid( 18, 18, 28, 190 );
	private static Texture Normal => normal ??= Solid( 58, 58, 92, 230 );
	private static Texture Disabled => disabled ??= Solid( 45, 45, 55, 170 );
	private static Texture Warn => warn ??= Solid( 150, 110, 50, 235 );

	private readonly record struct Btn( Rectangle Rect, string Label, bool Enabled, System.Action Act, bool Warned = false );

	public ManagePanel() => Current = this;

	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	private static Rectangle PanelBounds() => new( 12f, 14f, 460f, 76f );

	public bool ContainsMouse()
		=> BuildMode.Current != null && ParkFinances.Current != null && Contains( PanelBounds(), MouseBase() );

	// Build the live button list (shared by render + click handling so they never drift).
	private static List<Btn> Buttons()
	{
		var list = new List<Btn>();
		if ( ParkFinances.Current is not { } fin )
			return list;

		// Park economy row.
		list.Add( new Btn( new Rectangle( 16f, EconY, 64f, BtnH ), $"FEE-", fin.EntryFee > 0,
			() => fin.EntryFee = System.Math.Max( 0f, fin.EntryFee - 1f ) ) );
		list.Add( new Btn( new Rectangle( 84f, EconY, 64f, BtnH ), $"FEE+ {fin.EntryFee:0}", true,
			() => fin.EntryFee += 1f ) );
		if ( fin.Debt > 0 )
			list.Add( new Btn( new Rectangle( 152f, EconY, 130f, BtnH ), $"REPAY ${fin.Debt:0}",
				fin.CanAfford( fin.Debt ), () => fin.RepayLoan( 0 ), Warned: true ) );
		else
			list.Add( new Btn( new Rectangle( 152f, EconY, 130f, BtnH ), "TAKE LOAN", true,
				() => fin.TakeLoan( 0 ) ) );

		// Selected-ride row (only when a ride is selected via the Default tool).
		if ( BuildMode.Current?.SelectedRide is { } ride )
		{
			list.Add( new Btn( new Rectangle( 16f, RideY, 64f, BtnH ), "PRICE-", ride.TicketPrice > 1f,
				() => ride.TicketPrice = System.Math.Max( 1f, ride.TicketPrice - 1f ) ) );
			list.Add( new Btn( new Rectangle( 84f, RideY, 88f, BtnH ), $"PRICE+ {ride.TicketPrice:0}", true,
				() => ride.TicketPrice += 1f ) );
			list.Add( ResearchButton( fin, ride ) );
			list.Add( new Btn( new Rectangle( 350f, RideY, 116f, BtnH ),
				$"SELL ${ride.BuildCost * Ride.SellRefundFraction:0}", true,
				() => BuildMode.Current?.SellSelected(), Warned: true ) );
		}
		return list;
	}

	// The research/upgrade button mirrors the R/U keyboard logic + states.
	private static Btn ResearchButton( ParkFinances fin, Ride ride )
	{
		var rect = new Rectangle( 176f, RideY, 170f, BtnH );
		if ( ride.IsResearching )
			return new Btn( rect, $"RESEARCHING {ride.ResearchFraction * 100f:0}%", false, () => { } );
		if ( ride.NextResearched )
			return new Btn( rect, $"UPGRADE ${ride.NextUpgradeCost:0}", fin.CanAfford( ride.NextUpgradeCost ),
				() => { fin.PayBuild( ride.NextUpgradeCost ); ride.ApplyUpgrade(); }, Warned: true );
		if ( ride.HasNextLevel )
			return new Btn( rect, $"RESEARCH ${ride.NextResearchCost:0}", fin.CanAfford( ride.NextResearchCost ),
				() => { fin.PayBuild( ride.NextResearchCost ); ride.StartResearch(); } );
		return new Btn( rect, "MAX LEVEL", false, () => { } );
	}

	protected override void OnUpdate()
	{
		if ( BuildMode.Current == null || ParkFinances.Current == null || !Input.MouseLeftPressed )
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
		if ( BuildMode.Current == null || ParkFinances.Current == null )
			return;

		var mat = Material.UI;
		mat.Set( "Color", Bg );
		Graphics.Quad( PanelBounds(), mat );

		foreach ( var b in Buttons() )
			DrawButton( b.Rect, b.Label, !b.Enabled ? Disabled : b.Warned ? Warn : Normal );
	}
}
