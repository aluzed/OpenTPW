using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class AdvisorAdviceTests
{
	// Real-ish .sam scoring params for the advice rules under test.
	private const string Sam = @"
VisitorsThirsty.ScorePerThirstyPerson	2
VisitorsThirsty.ThirstierThan			80
VisitorsHungry.ScorePerHungryPerson		3
InTheRedSixMonths.Score					75
InTheRedThreeMonths.Score				50
InTheRedMonthLeft.Score					110
NewResearchGroupRide.Score				40
CongratVisitorsHappy.Score				60
";

	private static AdvisorConfig Cfg() => new( new SAMParser( Sam ).Parse() );

	// A healthy, solvent, empty-ish park: no advice.
	private static ParkSnapshot Healthy() =>
		new( Money: 5000f, MonthsInRed: 0, ThirstyVisitors: 0, HungryVisitors: 0, AverageHappiness: 50f, ResearchAvailable: false );

	[TestMethod]
	public void HealthyParkProducesNoAdvice()
	{
		Assert.AreEqual( 0, AdvisorAdvice.Evaluate( Healthy(), Cfg() ).Count );
	}

	[TestMethod]
	public void InTheRedEscalatesWithMonths()
	{
		var c = Cfg();
		string One( ParkSnapshot s ) => AdvisorAdvice.Evaluate( s, c ).Single( a => a.Id.StartsWith( "InTheRed" ) ).Id;

		Assert.AreEqual( "InTheRedMonthLeft", One( Healthy() with { Money = -100f, MonthsInRed = 1 } ) );
		Assert.AreEqual( "InTheRedThreeMonths", One( Healthy() with { Money = -100f, MonthsInRed = 3 } ) );
		Assert.AreEqual( "InTheRedSixMonths", One( Healthy() with { Money = -100f, MonthsInRed = 8 } ) );
	}

	[TestMethod]
	public void NoRedAdviceWhileSolventEvenWithStaleCounter()
	{
		// MonthsInRed only matters while Money < 0.
		var advice = AdvisorAdvice.Evaluate( Healthy() with { Money = 10f, MonthsInRed = 6 }, Cfg() );
		Assert.IsFalse( advice.Any( a => a.Id.StartsWith( "InTheRed" ) ) );
	}

	[TestMethod]
	public void ThirstyAndHungryScalePerVisitorWithSamParams()
	{
		var c = Cfg();
		var advice = AdvisorAdvice.Evaluate( Healthy() with { ThirstyVisitors = 4, HungryVisitors = 2 }, c );

		var thirst = advice.Single( a => a.Id == "VisitorsThirsty" );
		var hunger = advice.Single( a => a.Id == "VisitorsHungry" );
		Assert.AreEqual( 2f * 4, thirst.Score, 1e-4f, "ScorePerThirstyPerson(2) × 4" );
		Assert.AreEqual( 3f * 2, hunger.Score, 1e-4f, "ScorePerHungryPerson(3) × 2" );
		Assert.AreEqual( AdvisorAdvice.GroupGeneral, thirst.Group );
	}

	[TestMethod]
	public void ResearchAndCongratsFireOnState()
	{
		var c = Cfg();
		var research = AdvisorAdvice.Evaluate( Healthy() with { ResearchAvailable = true }, c );
		Assert.IsTrue( research.Any( a => a.Id == "NewResearchGroupRide" && a.Group == AdvisorAdvice.GroupResearch ) );

		var congrats = AdvisorAdvice.Evaluate( Healthy() with { AverageHappiness = 80f }, c );
		var happy = congrats.Single( a => a.Id == "CongratVisitorsHappy" );
		Assert.AreEqual( 60f, happy.Score, 1e-4f );
		Assert.AreEqual( AdvisorAdvice.GroupCongrats, happy.Group );

		// Just below the congrats threshold: nothing.
		Assert.IsFalse( AdvisorAdvice.Evaluate( Healthy() with { AverageHappiness = 74f }, c )
			.Any( a => a.Id == "CongratVisitorsHappy" ) );
	}

	[TestMethod]
	public void MissingSamParamsFallBackToDefaults()
	{
		var empty = new AdvisorConfig( System.Array.Empty<SettingsPair>() );
		var advice = AdvisorAdvice.Evaluate( Healthy() with { Money = -1f, MonthsInRed = 1, ThirstyVisitors = 3 }, empty );
		Assert.AreEqual( 110f, advice.Single( a => a.Id == "InTheRedMonthLeft" ).Score, 1e-4f, "default red score" );
		Assert.AreEqual( 1f * 3, advice.Single( a => a.Id == "VisitorsThirsty" ).Score, 1e-4f, "default 1/thirsty person" );
	}
}
