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
}
