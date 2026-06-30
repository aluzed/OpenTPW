using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class ParkFinancesTests
{
	// Admit `n` fresh visitors (each pays the gate + bumps VisitorsTotal).
	private static void Admit( ParkFinances fin, int n )
	{
		for ( int i = 0; i < n; i++ )
			fin.TakeEntryFee();
	}

	[TestMethod]
	public void RecentVisitorsCountsOverTheMonthsWindow()
	{
		var fin = new ParkFinances( 10000f, 5f );

		Admit( fin, 10 ); fin.SettleMonth();   // month 1 close: cumulative 10
		Admit( fin, 20 ); fin.SettleMonth();   // month 2 close: cumulative 30
		Admit( fin, 5 ); fin.SettleMonth();    // month 3 close: cumulative 35

		Assert.AreEqual( 5, fin.RecentVisitors( 1 ), "last 1 month = month 3's 5" );
		Assert.AreEqual( 25, fin.RecentVisitors( 2 ), "last 2 months = 20 + 5" );
		Assert.AreEqual( 35, fin.RecentVisitors( 3 ), "the whole history" );
		Assert.AreEqual( 35, fin.RecentVisitors( 10 ), "a window past the history clamps to all of it" );
		Assert.AreEqual( 0, fin.RecentVisitors( 0 ), "a zero-month window measures nothing" );
	}

	[TestMethod]
	public void RecentVisitorsIsZeroBeforeAnyMonthCloses()
	{
		var fin = new ParkFinances( 10000f, 5f );
		Admit( fin, 7 ); // admitted, but no month has closed yet
		Assert.AreEqual( 0, fin.RecentVisitors( 6 ) );
	}
}
