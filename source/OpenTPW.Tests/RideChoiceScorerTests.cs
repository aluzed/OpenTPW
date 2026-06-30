using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class RideChoiceScorerTests
{
	private static readonly DecisionWeights W = DecisionWeights.Defaults;

	[TestMethod]
	public void LoadsWeightsFromSam()
	{
		const string sam = @"
PeepInfo.DecisionVarDistWeight			1
PeepInfo.DecisionVarQueueWeight			3
PeepInfo.DecisionVarExcitementWeight	2
PeepInfo.DecisionVariable1				7
PeepInfo.DecisionVariable2				5
";
		var w = DecisionWeights.Load( new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( sam ) ) ) );
		Assert.AreEqual( 1f, w.Distance, 1e-3f );
		Assert.AreEqual( 3f, w.Queue, 1e-3f );
		Assert.AreEqual( 2f, w.Excitement, 1e-3f );
		Assert.AreEqual( 5f, w.NewRideMultiplier, 1e-3f );
		Assert.AreEqual( 7, w.NewRideDays );
	}

	[TestMethod]
	public void MoreExcitingScoresHigher()
	{
		var dull = RideChoiceScorer.Score( new RideOption( 30f, 50f, 0, false ), W );
		var wild = RideChoiceScorer.Score( new RideOption( 90f, 50f, 0, false ), W );
		Assert.IsTrue( wild > dull, "a more exciting ride at the same distance/queue scores higher" );
	}

	[TestMethod]
	public void FartherAndLongerQueueScoreLower()
	{
		var near = RideChoiceScorer.Score( new RideOption( 60f, 20f, 0, false ), W );
		var far = RideChoiceScorer.Score( new RideOption( 60f, 400f, 0, false ), W );
		Assert.IsTrue( far < near, "a farther ride scores lower" );

		var empty = RideChoiceScorer.Score( new RideOption( 60f, 50f, 0, false ), W );
		var busy = RideChoiceScorer.Score( new RideOption( 60f, 50f, 20, false ), W );
		Assert.IsTrue( busy < empty, "a longer queue scores lower" );
	}

	[TestMethod]
	public void NewRideGetsABonus()
	{
		var o = new RideOption( 60f, 50f, 0, IsNew: false );
		Assert.IsTrue( RideChoiceScorer.Score( o with { IsNew = true }, W ) > RideChoiceScorer.Score( o, W ) );
	}

	[TestMethod]
	public void IndoorBonusAppliesOnlyToIndoorRidesAndOnlyWhenPositive()
	{
		var outdoor = new RideOption( 60f, 50f, 0, IsNew: false, IsIndoors: false );
		var indoor = outdoor with { IsIndoors = true };

		// With no bonus (clear weather), the indoor flag changes nothing.
		Assert.AreEqual( RideChoiceScorer.Score( outdoor, W ), RideChoiceScorer.Score( indoor, W ), 1e-4f );

		// With a bad-weather bonus, the indoor ride scores higher; the outdoor one is unaffected.
		float bonus = 1.5f;
		Assert.AreEqual( RideChoiceScorer.Score( outdoor, W ), RideChoiceScorer.Score( outdoor, W, bonus ), 1e-4f );
		Assert.AreEqual( RideChoiceScorer.Score( indoor, W ) + bonus, RideChoiceScorer.Score( indoor, W, bonus ), 1e-4f );
		Assert.IsTrue( RideChoiceScorer.Score( indoor, W, bonus ) > RideChoiceScorer.Score( outdoor, W, bonus ) );
	}

	[TestMethod]
	public void ChooseWeightedFavoursHigherScoreButNotExclusively()
	{
		var scores = new[] { 1f, 3f }; // total 4

		// A low roll lands in the first bucket, a high roll in the second (which is bigger → more often).
		Assert.AreEqual( 0, RideChoiceScorer.ChooseWeighted( scores, 0.10f ) ); // 0.4 < 1
		Assert.AreEqual( 1, RideChoiceScorer.ChooseWeighted( scores, 0.50f ) ); // 2.0 → past the first bucket
		Assert.AreEqual( 1, RideChoiceScorer.ChooseWeighted( scores, 0.90f ) );
	}

	[TestMethod]
	public void ChooseWeightedFallsBackToLeastBadWhenAllNonPositive()
	{
		var scores = new[] { -5f, -1f, -3f };
		Assert.AreEqual( 1, RideChoiceScorer.ChooseWeighted( scores, 0.5f ), "the least-negative is taken" );
	}

	[TestMethod]
	public void PreferredExcitementMatchBeatsRawIntensity()
	{
		var gentle = new RideOption( 30f, 50f, 0, IsNew: false );
		var intense = new RideOption( 90f, 50f, 0, IsNew: false );

		// A timid peep (prefers 30) ranks the gentle ride above the intense one — the opposite of the raw
		// "more excitement is better" ranking.
		Assert.IsTrue( RideChoiceScorer.Score( gentle, W, 0f, preferredExcitement: 30f )
			> RideChoiceScorer.Score( intense, W, 0f, preferredExcitement: 30f ),
			"a timid peep prefers the ride nearest its taste" );

		// A thrill-seeker (prefers 90) flips the ranking back.
		Assert.IsTrue( RideChoiceScorer.Score( intense, W, 0f, preferredExcitement: 90f )
			> RideChoiceScorer.Score( gentle, W, 0f, preferredExcitement: 90f ),
			"a thrill-seeker prefers the most intense ride" );
	}

	[TestMethod]
	public void PreferredExcitementMinusOneMatchesPlainExcitement()
	{
		var o = new RideOption( 70f, 50f, 2, IsNew: true );
		// -1 (no taste) must reproduce the original excitement-only behaviour exactly (back-compat).
		Assert.AreEqual( RideChoiceScorer.Score( o, W ), RideChoiceScorer.Score( o, W, 0f, -1f ), 1e-4f );
	}

	[TestMethod]
	public void LoadsPreferredExcitementFromColumnarSam()
	{
		// The engine's columnar .sam layout: one key path names several fields, then the columns follow; the
		// parser keeps the first column as the value, so PreferredExcitement is the first number.
		const string sam = "PeepTypes[0].PreferredExcitement.StartingCash.BoredomThreshold\t80\t300\t40\n"
			+ "PeepTypes[1].PreferredExcitement.StartingCash.BoredomThreshold\t65\t250\t30\n";
		var prefs = PeepTypes.Load( new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( sam ) ) ) );

		Assert.AreEqual( 2, prefs.Count, "stops at the first missing type" );
		Assert.AreEqual( 80f, prefs[0], 1e-3f );
		Assert.AreEqual( 65f, prefs[1], 1e-3f );
	}

	[TestMethod]
	public void PreferredForWrapsIntoRange()
	{
		var saved = PeepTypes.Preferences;
		PeepTypes.Preferences = new[] { 10f, 90f };
		try
		{
			Assert.AreEqual( 10f, PeepTypes.PreferredFor( 0 ) );
			Assert.AreEqual( 90f, PeepTypes.PreferredFor( 1 ) );
			Assert.AreEqual( 10f, PeepTypes.PreferredFor( 2 ), "type index wraps modulo the table size" );
			Assert.AreEqual( 2, PeepTypes.Count );
		}
		finally { PeepTypes.Preferences = saved; }
	}
}
