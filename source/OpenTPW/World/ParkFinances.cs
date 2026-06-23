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

	/// <summary>The park's gate admission fee (player-settable, T-042).</summary>
	public float EntryFee { get; set; }

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

	/// <summary>Refund part of a build cost when a ride/shop is sold (credits the balance back).</summary>
	public void RefundBuild( float amount )
	{
		Money += amount;
		BuildSpent -= amount;
	}

	// ── Loans (T-042) ───────────────────────────────────────────────────────────────────────────
	/// <summary>A bank loan offer: principal in, repaid (with APR) over 12 monthly instalments.</summary>
	public sealed class Loan
	{
		public string Name = "";
		public float Principal;
		public float Apr;            // annual percentage rate
		public float Outstanding;    // remaining to repay (principal + interest), 0 when not bought
		public float Monthly;        // per-month instalment
		public bool Bought;
	}

	private readonly List<Loan> loans = new()
	{
		new Loan { Name = "Small", Principal = 5000f, Apr = 10f },
		new Loan { Name = "Large", Principal = 15000f, Apr = 18f },
	};

	public IReadOnlyList<Loan> Loans => loans;
	public float Debt => loans.Where( l => l.Bought ).Sum( l => l.Outstanding );

	/// <summary>True once the balance has dropped below the bankruptcy limit.</summary>
	public bool Bankrupt { get; private set; }

	private const float BankruptLimit = -5000f;
	private const float MonthSeconds = 8f; // one in-game "month" of loan repayment
	private float monthTimer;

	/// <summary>Take a loan: principal is credited now, repaid (principal + APR) over 12 months.</summary>
	public void TakeLoan( int index )
	{
		if ( index < 0 || index >= loans.Count )
			return;
		var l = loans[index];
		if ( l.Bought )
			return;
		l.Bought = true;
		l.Outstanding = l.Principal * (1f + l.Apr / 100f);
		l.Monthly = l.Outstanding / 12f;
		Money += l.Principal;
	}

	/// <summary>Pay off a loan's remaining balance in full (if affordable).</summary>
	public void RepayLoan( int index )
	{
		if ( index < 0 || index >= loans.Count )
			return;
		var l = loans[index];
		if ( !l.Bought || Money < l.Outstanding )
			return;
		Money -= l.Outstanding;
		l.Outstanding = 0f;
		l.Bought = false;
	}

	/// <summary>Advances time: debits monthly loan instalments and updates the bankruptcy flag.</summary>
	public void Tick( float dt )
	{
		monthTimer += dt;
		while ( monthTimer >= MonthSeconds )
		{
			monthTimer -= MonthSeconds;
			foreach ( var l in loans )
			{
				if ( !l.Bought )
					continue;
				float pay = MathF.Min( l.Monthly, l.Outstanding );
				Money -= pay;
				l.Outstanding -= pay;
				if ( l.Outstanding <= 0.01f )
				{
					l.Outstanding = 0f;
					l.Bought = false;
				}
			}
		}
		Bankrupt = Money < BankruptLimit;
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
