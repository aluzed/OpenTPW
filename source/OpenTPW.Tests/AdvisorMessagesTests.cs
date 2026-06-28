using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class AdvisorMessagesTests
{
	// A small Advisor.sam in the real text format, exercising the global params + a few groups + advice.
	private const string Sam = @"
# header comment
GeneralAdvisor.MinTimeAnyMessage	5
GeneralAdvisor.MinTimeSameMessage	120		trailing comment is ignored
GeneralAdvisor.MinScoreForConsideration	25

# General
MessageGroups[0].MinTimeSameMessage		30
MessageGroups[0].SayOnlyOnce			0
MessageGroups[0].DiscardAfterSlaps		0

# Tutorial messages
MessageGroups[1].MinTimeSameMessage		0
MessageGroups[1].SayOnlyOnce			1
MessageGroups[1].DiscardAfterSlaps		3

NewResearchGroupRide.Score				40
GoldTicketNearToXPeeps.PeopleMultiplier	0.9
";

	private static AdvisorConfig ParseSam( string text ) =>
		new( new SAMParser( text ).Parse() );

	[TestMethod]
	public void ConfigParsesGlobalsGroupsAndAdvice()
	{
		var c = ParseSam( Sam );

		Assert.AreEqual( 5f, c.MinTimeAnyMessage, 1e-3f );
		Assert.AreEqual( 120f, c.MinTimeSameMessage, 1e-3f, "trailing comment after the value is dropped" );
		Assert.AreEqual( 25f, c.MinScoreForConsideration, 1e-3f );

		Assert.AreEqual( 2, c.MessageGroups.Count );
		var g1 = c.Group( 1 );
		Assert.AreEqual( 0f, g1.MinTimeSameMessage, 1e-3f );
		Assert.IsTrue( g1.SayOnlyOnce );
		Assert.AreEqual( 3, g1.DiscardAfterSlaps );

		// Generic per-advice params, including a float.
		Assert.AreEqual( 40f, c.Param( "NewResearchGroupRide", "Score" ) );
		Assert.AreEqual( 0.9f, c.Param( "GoldTicketNearToXPeeps", "PeopleMultiplier" )!.Value, 1e-4f );
		Assert.IsNull( c.Param( "NewResearchGroupRide", "Nope" ) );
		Assert.IsNull( c.Param( "Unknown", "Score" ) );
	}

	[TestMethod]
	public void UndefinedGroupFallsBackToPermissiveDefault()
	{
		var c = ParseSam( Sam );
		var g = c.Group( 7 ); // not in the file
		Assert.AreEqual( c.MinTimeSameMessage, g.MinTimeSameMessage, 1e-3f );
		Assert.IsFalse( g.SayOnlyOnce );
		Assert.AreEqual( 0, g.DiscardAfterSlaps );
	}

	[TestMethod]
	public void MissingFileYieldsUsableDefaults()
	{
		var c = new AdvisorConfig( Array.Empty<SettingsPair>() );
		Assert.AreEqual( 5f, c.MinTimeAnyMessage, 1e-3f );
		Assert.AreEqual( 120f, c.MinTimeSameMessage, 1e-3f );
		Assert.AreEqual( 25f, c.MinScoreForConsideration, 1e-3f );
		Assert.AreEqual( 0, c.MessageGroups.Count );
	}

	[TestMethod]
	public void HighestEligibleScoreWins()
	{
		var c = ParseSam( Sam );
		var m = new AdvisorMessages( c );

		m.Submit( "low", c.Group( 0 ), 30f );
		m.Submit( "high", c.Group( 0 ), 90f );
		Assert.AreEqual( "high", m.Consider( now: 100f ) );
		Assert.AreEqual( "high", m.Active );
	}

	[TestMethod]
	public void BelowMinScoreIsNeverSaid()
	{
		var c = ParseSam( Sam ); // MinScoreForConsideration = 25
		var m = new AdvisorMessages( c );
		m.Submit( "meh", c.Group( 0 ), 24f );
		Assert.IsNull( m.Consider( 100f ) );
	}

	[TestMethod]
	public void GlobalGapSilencesEveryMessage()
	{
		var c = ParseSam( Sam ); // MinTimeAnyMessage = 5
		var m = new AdvisorMessages( c );

		m.Submit( "a", c.Group( 0 ), 50f );
		Assert.AreEqual( "a", m.Consider( 100f ) );

		// A different message 3s later is still blocked by the global 5s gap.
		m.Submit( "b", c.Group( 0 ), 99f );
		Assert.IsNull( m.Consider( 103f ) );

		// Past the gap it fires.
		m.Submit( "b", c.Group( 0 ), 99f );
		Assert.AreEqual( "b", m.Consider( 106f ) );
	}

	[TestMethod]
	public void SameMessageRespectsItsGroupGap()
	{
		var c = ParseSam( Sam ); // group 0 MinTimeSameMessage = 30
		var m = new AdvisorMessages( c );

		m.Submit( "tip", c.Group( 0 ), 50f );
		Assert.AreEqual( "tip", m.Consider( 0f ) );

		// 20s later the global gap is satisfied but the same-message gap (30s) is not.
		m.Submit( "tip", c.Group( 0 ), 50f );
		Assert.IsNull( m.Consider( 20f ) );

		// 30s later it may repeat.
		m.Submit( "tip", c.Group( 0 ), 50f );
		Assert.AreEqual( "tip", m.Consider( 30f ) );
		Assert.AreEqual( 2, m.TimesSaid( "tip" ) );
	}

	[TestMethod]
	public void SayOnlyOnceFiresExactlyOnce()
	{
		var c = ParseSam( Sam ); // group 1 SayOnlyOnce = 1
		var m = new AdvisorMessages( c );

		m.Submit( "welcome", c.Group( 1 ), 100f );
		Assert.AreEqual( "welcome", m.Consider( 0f ) );

		m.Submit( "welcome", c.Group( 1 ), 100f );
		Assert.IsNull( m.Consider( 1000f ), "a say-once message never repeats" );
		Assert.AreEqual( 1, m.TimesSaid( "welcome" ) );
	}

	[TestMethod]
	public void DiscardAfterSlapsRetiresAMessage()
	{
		var c = ParseSam( Sam ); // group 1 DiscardAfterSlaps = 3
		var m = new AdvisorMessages( c );
		var g = c.Group( 1 );

		m.Slap( "welcome", g );
		m.Slap( "welcome", g );
		Assert.IsFalse( m.IsDiscarded( "welcome" ), "two slaps below the threshold" );
		m.Slap( "welcome", g );
		Assert.IsTrue( m.IsDiscarded( "welcome" ), "third slap retires it" );

		m.Submit( "welcome", g, 100f );
		Assert.IsNull( m.Consider( 0f ), "a discarded message is never said" );
	}

	[TestMethod]
	public void ZeroDiscardAfterSlapsNeverRetires()
	{
		var c = ParseSam( Sam ); // group 0 DiscardAfterSlaps = 0
		var m = new AdvisorMessages( c );
		var g = c.Group( 0 );
		for ( int i = 0; i < 10; i++ )
			m.Slap( "tip", g );
		Assert.IsFalse( m.IsDiscarded( "tip" ) );
	}
}
