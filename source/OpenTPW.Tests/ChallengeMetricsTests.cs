using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class ChallengeMetricsTests
{
	private static Challenge OfType( int type, int target = 100, int time = 60 )
		=> new( 1, type, 0, time, target, 0, 0, 0, 1000f, false, true );

	[TestMethod]
	public void SupportsTheDecodedTypes()
	{
		foreach ( var t in new[] { 1, 2, 3, 11, 13, 20 } )
			Assert.IsTrue( ChallengeMetrics.IsSupported( t ), $"type {t} is decoded + tracked" );
		// Still-opaque types stay unsupported (skipped by the manager).
		foreach ( var t in new[] { 6, 18, 19, 24 } )
			Assert.IsFalse( ChallengeMetrics.IsSupported( t ), $"type {t} has no counter yet" );
	}

	[TestMethod]
	public void DescribesEachTypeFromTheSamSemantics()
	{
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 1, 200 ) ), "fries" );
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 2, 30 ) ), "burgers" );
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 3, 30 ) ), "drinks" );
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 11, 100 ) ), "new visitors" );
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 13, 500 ) ), "sideshow" );
		StringAssert.Contains( ChallengeMetrics.Describe( OfType( 20, 200 ) ), "ride" );
	}

	[TestMethod]
	public void CurrentReadsTheCounterEachTypeMeasures()
	{
		var saved = ParkFinances.Current;
		try
		{
			var fin = new ParkFinances( 10000f, 5f );
			ParkFinances.Current = fin;

			fin.TakeFoodSale( 3f );                 // food (fries/burgers counter)
			fin.TakeFoodSale( 3f, drink: true );    // drink
			fin.TakeEntryFee();                     // a visitor
			fin.TakeRideTicket( 4f );               // a ride ridden
			fin.TakeSideshowTakings( 25f, won: false ); // sideshow profit

			Assert.AreEqual( 1f, ChallengeMetrics.Current( OfType( 1 ) ), "type 1 → FoodSold" );
			Assert.AreEqual( 1f, ChallengeMetrics.Current( OfType( 2 ) ), "type 2 → FoodSold" );
			Assert.AreEqual( 1f, ChallengeMetrics.Current( OfType( 3 ) ), "type 3 → DrinkSold" );
			Assert.AreEqual( 1f, ChallengeMetrics.Current( OfType( 11 ) ), "type 11 → VisitorsTotal" );
			Assert.AreEqual( 25f, ChallengeMetrics.Current( OfType( 13 ) ), "type 13 → SideshowRevenue" );
			Assert.AreEqual( 1f, ChallengeMetrics.Current( OfType( 20 ) ), "type 20 → RidesRidden" );
			Assert.AreEqual( 0f, ChallengeMetrics.Current( OfType( 99 ) ), "an unmapped type reads 0" );
		}
		finally { ParkFinances.Current = saved; }
	}
}
