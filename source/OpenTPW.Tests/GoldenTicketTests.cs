using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class GoldenTicketTests
{
	private const string Sam = @"
GoldenTicketLocal.Visitors						100
GoldenTicketLocal.PeopleInPark					200
GoldenTicketLocal.Happiness						75
GoldenTicketLocal.AtLeastThisManyHappyPeople	150
GoldenTicketLocal.ProfitYear					15000
GoldenTicketLocal.RecentVisitors				350
GoldenTicketLocal.RecentVisitorMonths			6
";

	private static GoldenTicketTargets Targets()
		=> GoldenTicketTargets.ParseLocal( new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( Sam ) ) ) );

	[TestMethod]
	public void ParsesLocalTargets()
	{
		var t = Targets();
		Assert.AreEqual( 100, t.Visitors );
		Assert.AreEqual( 200, t.PeopleInPark );
		Assert.AreEqual( 75f, t.Happiness, 1e-3f );
		Assert.AreEqual( 150, t.HappyPeople );
		Assert.AreEqual( 15000f, t.ProfitYear, 1e-3f );
		Assert.AreEqual( 350, t.RecentVisitors );
		Assert.AreEqual( 6, t.RecentVisitorMonths );
	}

	[TestMethod]
	public void EvaluateYieldsOneRowPerSetTargetWithMetFlag()
	{
		var t = Targets();
		var s = new ParkState( VisitorsTotal: 120, PeopleInPark: 150, AverageHappiness: 80f, HappyPeople: 100, ProfitYear: 20000f );
		var rows = GoldenTicket.Evaluate( t, s );

		Assert.AreEqual( 5, rows.Count, "five set targets (RecentVisitors isn't evaluated)" );
		Assert.IsTrue( rows.Single( r => r.Name == "Visitors" ).Met, "120 ≥ 100" );
		Assert.IsFalse( rows.Single( r => r.Name == "In park" ).Met, "150 < 200" );
		Assert.IsTrue( rows.Single( r => r.Name == "Happiness" ).Met, "80 ≥ 75" );
		Assert.IsFalse( rows.Single( r => r.Name == "Happy people" ).Met, "100 < 150" );
		Assert.IsTrue( rows.Single( r => r.Name == "Profit/yr" ).Met, "20000 ≥ 15000" );
	}

	[TestMethod]
	public void AllMetOnlyWhenEverySetTargetIsMet()
	{
		var t = Targets();
		var nearly = new ParkState( 120, 250, 80f, 200, 20000f ); // happiness... 80≥75 ok; all others ok
		Assert.IsTrue( GoldenTicket.AllMet( t, nearly ) );

		var oneShort = nearly with { ProfitYear = 14999f }; // profit just under
		Assert.IsFalse( GoldenTicket.AllMet( t, oneShort ) );
	}

	[TestMethod]
	public void NoTargetsMeansNotAwardable()
	{
		var none = new GoldenTicketTargets( 0, 0, 0, 0, 0, 0, 0 );
		Assert.IsFalse( GoldenTicket.AllMet( none, new ParkState( 999, 999, 100f, 999, 999999f ) ),
			"a level with no targets is never vacuously awarded" );
	}

	[TestMethod]
	public void UnsetTargetsAreSkipped()
	{
		var t = new GoldenTicketTargets( Visitors: 50, PeopleInPark: 0, Happiness: 0, HappyPeople: 0, ProfitYear: 0, 0, 0 );
		var rows = GoldenTicket.Evaluate( t, new ParkState( 60, 0, 0f, 0, 0f ) );
		Assert.AreEqual( 1, rows.Count, "only the one set target produces a row" );
		Assert.IsTrue( GoldenTicket.AllMet( t, new ParkState( 60, 0, 0f, 0, 0f ) ) );
	}
}
