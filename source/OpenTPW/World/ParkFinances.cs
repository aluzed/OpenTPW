namespace OpenTPW;

/// <summary>
/// The park's bank balance and the money flows that move it: ride tickets and the gate entry fee bring
/// money in, ride upkeep drains it. In the original the player sets ride prices and the entry fee at
/// runtime (they aren't in the ride <c>.sam</c>), so the per-ride <see cref="Ride.TicketPrice"/> and
/// <see cref="Ride.UpkeepPerSecond"/> are sensible derived defaults for now. Cumulative flow totals are
/// kept for diagnostics and a future finances HUD.
/// </summary>
public sealed class ParkFinances
{
	/// <summary>The active park's finances (set by the level on load).</summary>
	public static ParkFinances? Current { get; set; }

	public float Money { get; private set; }
	public float EntryFee { get; }

	public float RideRevenue { get; private set; }
	public float EntryRevenue { get; private set; }
	public float FoodRevenue { get; private set; }
	public float UpkeepPaid { get; private set; }
	public float WagesPaid { get; private set; }
	public float BuildSpent { get; private set; }

	public ParkFinances( float starting, float entryFee )
	{
		Money = starting;
		EntryFee = entryFee;
	}

	/// <summary>Can the park currently afford a build/upgrade of <paramref name="cost"/>?</summary>
	public bool CanAfford( float cost ) => Money >= cost;

	/// <summary>Pay a one-off build/placement cost (caller checks <see cref="CanAfford"/> first).</summary>
	public void PayBuild( float amount )
	{
		Money -= amount;
		BuildSpent += amount;
	}

	/// <summary>A peep pays to board a ride.</summary>
	public void TakeRideTicket( float price )
	{
		Money += price;
		RideRevenue += price;
	}

	/// <summary>A fresh visitor pays the gate entry fee.</summary>
	public void TakeEntryFee()
	{
		Money += EntryFee;
		EntryRevenue += EntryFee;
	}

	/// <summary>A hungry visitor buys a snack from a shop.</summary>
	public void TakeFoodSale( float price )
	{
		Money += price;
		FoodRevenue += price;
	}

	/// <summary>Ongoing ride running cost.</summary>
	public void PayUpkeep( float amount )
	{
		Money -= amount;
		UpkeepPaid += amount;
	}

	/// <summary>Ongoing staff wages.</summary>
	public void PayWages( float amount )
	{
		Money -= amount;
		WagesPaid += amount;
	}
}
