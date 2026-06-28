using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class ManagementDepthTests
{
	// Finance history (T-049): ParkFinances samples one (balance, income, expense) point per in-game
	// "month" (8s of Tick), oldest dropped past MaxHistory — the data behind the F11 finance graph.

	[TestMethod]
	public void RecordsAMonthlySampleWithThatMonthsFlows()
	{
		var fin = new ParkFinances( starting: 1000f, entryFee: 5f );
		fin.TakeRideTicket( 50f ); // income
		fin.PayUpkeep( 20f );      // expense

		Assert.AreEqual( 0, fin.History.Count, "no sample before a month elapses" );
		fin.Tick( 8f );            // crosses one month boundary

		Assert.AreEqual( 1, fin.History.Count );
		var s = fin.History[0];
		Assert.AreEqual( 50f, s.Income, 1e-3f );
		Assert.AreEqual( 20f, s.Expense, 1e-3f );
		Assert.AreEqual( 1030f, s.Balance, 1e-3f ); // 1000 + 50 - 20
	}

	[TestMethod]
	public void IncomeAndExpenseArePerMonthDeltas()
	{
		var fin = new ParkFinances( 0f, 0f );
		fin.TakeRideTicket( 100f );
		fin.Tick( 8f ); // month 1 captures 100
		fin.TakeFoodSale( 30f );
		fin.Tick( 8f ); // month 2 captures only the new 30

		Assert.AreEqual( 2, fin.History.Count );
		Assert.AreEqual( 100f, fin.History[0].Income, 1e-3f );
		Assert.AreEqual( 30f, fin.History[1].Income, 1e-3f );
	}

	[TestMethod]
	public void HistoryIsCappedToMaxHistory()
	{
		var fin = new ParkFinances( 0f, 0f );
		for ( int i = 0; i < ParkFinances.MaxHistory + 12; i++ )
			fin.Tick( 8f );

		Assert.AreEqual( ParkFinances.MaxHistory, fin.History.Count );
	}

	[TestMethod]
	public void EachLoanOfferTakenAndRepaidIndependently()
	{
		// T-042: the UI exposes one button per offer, so both loans must work by index (not just the first).
		var fin = new ParkFinances( starting: 0f, entryFee: 0f );
		Assert.AreEqual( 2, fin.Loans.Count, "small + large offers" );
		Assert.AreEqual( 0f, fin.Debt, 1e-3f );

		// Take the large (index 1): debt = principal × (1 + APR).
		fin.TakeLoan( 1 );
		var large = fin.Loans[1];
		Assert.IsTrue( large.Bought );
		Assert.AreEqual( large.Principal * (1f + large.Apr / 100f), fin.Debt, 1e-2f );

		// Re-taking the same offer is a no-op.
		float debtAfter = fin.Debt;
		fin.TakeLoan( 1 );
		Assert.AreEqual( debtAfter, fin.Debt, 1e-3f );

		// The small offer is independent and still available → debt is the sum of both.
		fin.TakeLoan( 0 );
		Assert.IsTrue( fin.Loans[0].Bought );
		Assert.AreEqual( fin.Loans[0].Outstanding + fin.Loans[1].Outstanding, fin.Debt, 1e-2f );

		// Repay the large in full → its debt clears, the small remains.
		fin.RepayLoan( 1 );
		Assert.IsFalse( fin.Loans[1].Bought );
		Assert.AreEqual( fin.Loans[0].Outstanding, fin.Debt, 1e-2f );
	}

	// Patrol zones (T-049): a pure circular zone tested in the XY plane (height ignored), boundary inclusive.

	[TestMethod]
	public void PatrolZoneContainsPointsWithinRadius()
	{
		var z = new PatrolZone( new Vector3( 10f, 10f, 0f ), 5f );

		Assert.IsTrue( z.Contains( new Vector3( 12f, 10f, 999f ) ), "inside (and height is ignored)" );
		Assert.IsTrue( z.Contains( new Vector3( 13f, 14f, 0f ) ), "3-4-5 lands exactly on the boundary (inclusive)" );
		Assert.IsFalse( z.Contains( new Vector3( 20f, 10f, 0f ) ), "10 units away is outside a radius-5 zone" );
	}
}
