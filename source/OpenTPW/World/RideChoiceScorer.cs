using System.Globalization;

namespace OpenTPW;

/// <summary>The authored ride-choice decision weights (T-060), from <c>PeepInfo.DecisionVar*</c> in
/// <c>levels/Standard.sam</c>: how much distance / queue length / excitement pull a peep's ride choice, plus
/// the "new ride" bonus (<c>DecisionVariable2</c> multiplier while a ride is younger than
/// <c>DecisionVariable1</c> days). The need weights (thirst/hunger/…) steer toward shops, not rides, so they
/// aren't part of the ride scorer.</summary>
public readonly record struct DecisionWeights( float Distance, float Queue, float Excitement, float NewRideMultiplier, int NewRideDays )
{
	/// <summary>The shipped jungle defaults (dist 1, queue 1, excitement 1, new×5 for 7 days).</summary>
	public static DecisionWeights Defaults => new( 1f, 1f, 1f, 5f, 7 );

	/// <summary>Read the weights from a level settings file (falling back to <see cref="Defaults"/> per key).</summary>
	public static DecisionWeights Load( SettingsFile sam )
	{
		float G( string key, float fallback )
			=> float.TryParse( sam[$"PeepInfo.{key}"], NumberStyles.Float, CultureInfo.InvariantCulture, out var v ) ? v : fallback;
		return new DecisionWeights(
			Distance: G( "DecisionVarDistWeight", 1f ),
			Queue: G( "DecisionVarQueueWeight", 1f ),
			Excitement: G( "DecisionVarExcitementWeight", 1f ),
			NewRideMultiplier: G( "DecisionVariable2", 5f ),
			NewRideDays: (int)G( "DecisionVariable1", 7f ) );
	}
}

/// <summary>One ride a peep is weighing: its excitement (0–100), the world distance to it, how many peeps are
/// already queued, and whether it's still "new".</summary>
public readonly record struct RideOption( float Excitement, float Distance, int QueueLength, bool IsNew );

/// <summary>
/// The weighted-utility ride scorer (T-060): the original picks a ride by a score over distance / queue /
/// excitement (and a new-ride bonus), with the weights authored in the level <c>.sam</c>. This replaces the
/// previous excitement/rating-only heuristic. Pure (unit-tested); the factors are normalised to comparable
/// 0–1 ranges so the unit weights (≈1) balance, then a peep picks a ride <b>weighted by score</b> (keeping
/// crowd variety) rather than always the single best.
/// </summary>
public static class RideChoiceScorer
{
	/// <summary>The active level's decision weights (loaded from <c>Standard.sam</c>); defaults until set.</summary>
	public static DecisionWeights Weights { get; set; } = DecisionWeights.Defaults;

	private const float RefDistance = 200f; // world units that count as a "full" distance penalty
	private const float RefQueue = 10f;     // queued peeps that count as a "full" queue penalty

	/// <summary>The desirability of <paramref name="o"/> under <paramref name="w"/> (higher = more appealing;
	/// can be negative when a ride is far / heavily queued).</summary>
	public static float Score( RideOption o, DecisionWeights w )
	{
		float excitement = o.Excitement / 100f;
		float score = w.Excitement * excitement
			- w.Distance * (o.Distance / RefDistance)
			- w.Queue * (o.QueueLength / RefQueue);
		if ( o.IsNew )
			score += w.NewRideMultiplier * excitement; // a new ride draws extra interest
		return score;
	}

	/// <summary>Pick an option index weighted by clamped-positive score (so a more appealing ride is chosen
	/// more often, but not exclusively). <paramref name="roll01"/> is a [0,1) sample. Falls back to the
	/// highest score when every option scores ≤ 0, and to the last index when the list is empty of weight.</summary>
	public static int ChooseWeighted( IReadOnlyList<float> scores, float roll01 )
	{
		if ( scores.Count == 0 )
			return -1;

		float total = 0f;
		foreach ( var s in scores )
			total += MathF.Max( 0f, s );

		if ( total <= 0f )
		{
			// All unappealing — take the least-bad.
			int best = 0;
			for ( int i = 1; i < scores.Count; i++ )
				if ( scores[i] > scores[best] ) best = i;
			return best;
		}

		float r = Math.Clamp( roll01, 0f, 0.999999f ) * total;
		for ( int i = 0; i < scores.Count; i++ )
		{
			r -= MathF.Max( 0f, scores[i] );
			if ( r < 0f )
				return i;
		}
		return scores.Count - 1;
	}
}
