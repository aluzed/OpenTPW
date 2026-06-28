namespace OpenTPW;

/// <summary>
/// Maps a <see cref="Challenge.Type"/> to the live park counter it measures + a human description (T-054).
/// <para><b>Scope note.</b> <c>Challenges.sam</c> has ~28 distinct Types; their exact semantics need the
/// localized <c>.str</c> descriptions or a Ghidra trace of the <c>ChallengeType</c> dispatch. Mapped here are
/// the confidently-identified ones (visitors / food / drinks, from the RE recon); the manager only offers
/// challenges of a <see cref="IsSupported"/> Type, so unmapped ones are parsed but skipped until their metric
/// is wired. Adding a Type = one case in each switch + (if needed) a counter on <see cref="ParkFinances"/>.</para>
/// </summary>
public static class ChallengeMetrics
{
	/// <summary>Whether this challenge Type is tracked (so the manager can offer it).</summary>
	public static bool IsSupported( int type ) => type is 1 or 2 or 3;

	/// <summary>The current absolute value of the counter <paramref name="c"/>'s Type measures (the manager
	/// tracks the gain since the challenge was accepted).</summary>
	public static float Current( Challenge c )
	{
		var fin = ParkFinances.Current;
		if ( fin == null )
			return 0f;
		return c.Type switch
		{
			1 => fin.VisitorsTotal,
			2 => fin.FoodSold,
			3 => fin.DrinkSold,
			_ => 0f,
		};
	}

	/// <summary>A short goal description for the UI.</summary>
	public static string Describe( Challenge c ) => c.Type switch
	{
		1 => $"Attract {c.TargetVal} visitors in {c.TargetTime} days",
		2 => $"Sell {c.TargetVal} meals in {c.TargetTime} days",
		3 => $"Sell {c.TargetVal} drinks in {c.TargetTime} days",
		_ => $"Challenge: reach {c.TargetVal} in {c.TargetTime} days",
	};
}
