using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ChallengeTests
{
	// ── Parser ────────────────────────────────────────────────────────────────────────────────────────

	private const string Sam = @"
Challenges[1].Type				3
Challenges[1].TargetTime		60
Challenges[1].TargetVal			30
Challenges[1].Prize				5000
Challenges[1].CheckAtEndOnly	0
Challenges[1].Independent		1
Challenges[2].Type				2
Challenges[2].FollowupType		3
Challenges[2].TargetTime		90
Challenges[2].TargetVal			200
Challenges[2].Prize				10000
Challenges[2].CheckAtEndOnly	1
";

	[TestMethod]
	public void ParsesChallengesByIndex()
	{
		var list = Challenge.ParseAll( new SAMParser( Sam ).Parse() );
		Assert.AreEqual( 2, list.Count );

		var c1 = list[0];
		Assert.AreEqual( 1, c1.Index );
		Assert.AreEqual( 3, c1.Type );
		Assert.AreEqual( 60, c1.TargetTime );
		Assert.AreEqual( 30, c1.TargetVal );
		Assert.AreEqual( 5000f, c1.Prize, 1e-3f );
		Assert.IsFalse( c1.CheckAtEndOnly );
		Assert.IsTrue( c1.Independent );

		var c2 = list[1];
		Assert.AreEqual( 3, c2.FollowupType );
		Assert.IsTrue( c2.CheckAtEndOnly );
	}

	// ── Engine ────────────────────────────────────────────────────────────────────────────────────────

	// Challenge ctor order is (Index, Type, FollowupType, TargetTime, TargetVal, …); this helper reorders to
	// the fields the tests care about.
	private static Challenge Ch( int idx, int type, int target, int time, float prize = 1000f, int followup = 0, bool checkEnd = false )
		=> new( idx, type, followup, time, target, 0, 0, 0, prize, checkEnd, true );

	[TestMethod]
	public void OffersAfterDelayThenAcceptRunsIt()
	{
		float metric = 0;
		var mgr = new ChallengeManager( new[] { Ch( 1, 3, 10, 5 ) }, _ => metric, _ => { } ) { DaysUntilFirst = 2 };

		mgr.OnNewDay();
		Assert.AreEqual( ChallengeManager.Phase.Idle, mgr.State, "not offered before the delay" );
		mgr.OnNewDay();
		Assert.AreEqual( ChallengeManager.Phase.Offered, mgr.State );
		Assert.AreEqual( 3, mgr.Active!.Type );

		mgr.Accept();
		Assert.AreEqual( ChallengeManager.Phase.Active, mgr.State );
		Assert.AreEqual( 5, mgr.DaysLeft );
	}

	[TestMethod]
	public void WinsOnMetricGainAndPaysPrize()
	{
		float metric = 100; // non-zero baseline → must measure the delta since accepting
		float paid = 0;
		var mgr = new ChallengeManager( new[] { Ch( 1, 3, 10, 30, prize: 5000f ) }, _ => metric, p => paid += p )
			{ DaysUntilFirst = 1 };

		mgr.OnNewDay(); mgr.Accept();         // baseline 100
		metric = 105; mgr.OnNewDay();          // gain 5 < 10
		Assert.AreEqual( ChallengeManager.Phase.Active, mgr.State );
		metric = 112; mgr.OnNewDay();          // gain 12 ≥ 10 → win
		Assert.AreEqual( ChallengeManager.Phase.Idle, mgr.State );
		Assert.AreEqual( 1, mgr.Won );
		Assert.AreEqual( 5000f, paid, 1e-3f );
		Assert.IsTrue( mgr.LastResult!.Value.Won );
	}

	[TestMethod]
	public void LosesOnTimeout()
	{
		var mgr = new ChallengeManager( new[] { Ch( 1, 3, 10, 3 ) }, _ => 0f, _ => { } ) { DaysUntilFirst = 1 };
		mgr.OnNewDay(); mgr.Accept();          // DaysLeft 3
		mgr.OnNewDay(); mgr.OnNewDay(); mgr.OnNewDay();
		Assert.AreEqual( ChallengeManager.Phase.Idle, mgr.State );
		Assert.AreEqual( 1, mgr.Lost );
		Assert.IsFalse( mgr.LastResult!.Value.Won );
	}

	[TestMethod]
	public void DeclineThenAnotherIsOffered()
	{
		var mgr = new ChallengeManager( new[] { Ch( 1, 3, 10, 5 ), Ch( 2, 2, 10, 5 ) }, _ => 0f, _ => { } )
			{ DaysUntilFirst = 1, DaysAfterDeclined = 2 };
		mgr.OnNewDay(); mgr.Decline();
		Assert.AreEqual( ChallengeManager.Phase.Idle, mgr.State );
		mgr.OnNewDay(); mgr.OnNewDay();
		Assert.AreEqual( ChallengeManager.Phase.Offered, mgr.State );
		Assert.AreEqual( 2, mgr.Active!.Type, "the next challenge is offered" );
	}

	[TestMethod]
	public void CheckAtEndOnlyWaitsForTheDeadline()
	{
		float metric = 0;
		var mgr = new ChallengeManager( new[] { Ch( 1, 3, 10, 2, checkEnd: true ) }, _ => metric, _ => { } )
			{ DaysUntilFirst = 1 };
		mgr.OnNewDay(); mgr.Accept();          // DaysLeft 2
		metric = 50; mgr.OnNewDay();           // target met early but CheckAtEndOnly → still active
		Assert.AreEqual( ChallengeManager.Phase.Active, mgr.State );
		mgr.OnNewDay();                        // deadline → win
		Assert.AreEqual( 1, mgr.Won );
	}

	[TestMethod]
	public void WinChainsToFollowupType()
	{
		float metric = 100;
		var mgr = new ChallengeManager(
			new[] { Ch( 1, 3, 1, 5, followup: 2 ), Ch( 2, 2, 10, 5 ) }, _ => metric, _ => { } )
			{ DaysUntilFirst = 1, DaysAfterCompleted = 1 };
		mgr.OnNewDay(); mgr.Accept();          // challenge Type 3
		metric = 200; mgr.OnNewDay();          // win
		Assert.AreEqual( 1, mgr.Won );
		mgr.OnNewDay();                        // after DaysAfterCompleted → offer the followup
		Assert.AreEqual( ChallengeManager.Phase.Offered, mgr.State );
		Assert.AreEqual( 2, mgr.Active!.Type, "FollowupType 2 is offered next" );
	}
}
