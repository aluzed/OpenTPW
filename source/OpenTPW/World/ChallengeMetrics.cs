namespace OpenTPW;

/// <summary>
/// Maps a <see cref="Challenge.Type"/> to the live park counter it measures + a human description (T-054).
/// <para><b>Type semantics</b> are taken from <c>Challenges.sam</c>'s own per-challenge comments (EA's authored
/// descriptions), which decode the numeric types: 1 = sell fries, 2 = sell burgers, 3 = sell drinks, 11 = new
/// visitors, 13 = sideshow profit, 20 = peeps using a ride. Fries + burgers both map to our single
/// <see cref="ParkFinances.FoodSold"/> counter (OpenTPW's economy doesn't split food into items), and the
/// "use the Minecart ride" goal is approximated by total <see cref="ParkFinances.RidesRidden"/> (we don't track
/// a specific ride's rider count). The manager only offers <see cref="IsSupported"/> types, so the still-opaque
/// ones (6 = balloons, 18/19 = build a specific ride, 24 = toilet cleanliness) are parsed but skipped until a
/// counter exists. Adding a type = one case in each switch (+ a counter on <see cref="ParkFinances"/> if needed).</para>
/// </summary>
public static class ChallengeMetrics
{
	/// <summary>Whether this challenge Type is tracked (so the manager can offer it).</summary>
	public static bool IsSupported( int type ) => type is 1 or 2 or 3 or 11 or 13 or 20;

	/// <summary>The current absolute value of the counter <paramref name="c"/>'s Type measures (the manager
	/// tracks the gain since the challenge was accepted).</summary>
	public static float Current( Challenge c )
	{
		var fin = ParkFinances.Current;
		if ( fin == null )
			return 0f;
		return c.Type switch
		{
			1 => fin.FoodSold,         // "Sell N Fries"  — food (we don't split food into items)
			2 => fin.FoodSold,         // "Sell N Burgers" — food
			3 => fin.DrinkSold,        // "Sell N Drinks"
			11 => fin.VisitorsTotal,   // "Get N new visitors"
			13 => fin.SideshowRevenue, // "Get your sideshows to make N profit"
			20 => fin.RidesRidden,     // "Get N peeps to use [a] ride" (approx: any ride)
			_ => 0f,
		};
	}

	/// <summary>A short goal description for the UI.</summary>
	public static string Describe( Challenge c ) => c.Type switch
	{
		1 => $"Sell {c.TargetVal} portions of fries in {c.TargetTime} days",
		2 => $"Sell {c.TargetVal} burgers in {c.TargetTime} days",
		3 => $"Sell {c.TargetVal} drinks in {c.TargetTime} days",
		11 => $"Attract {c.TargetVal} new visitors in {c.TargetTime} days",
		13 => $"Make {c.TargetVal} sideshow profit in {c.TargetTime} days",
		20 => $"Get {c.TargetVal} peeps to ride in {c.TargetTime} days",
		_ => $"Challenge: reach {c.TargetVal} in {c.TargetTime} days",
	};
}
