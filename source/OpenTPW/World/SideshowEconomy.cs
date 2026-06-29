namespace OpenTPW;

/// <summary>
/// Pure sideshow takings model (T-058), from the authored <c>UsageInfo</c> fields in a sideshow's <c>.sam</c>
/// (e.g. jungle <c>InitPricePerUse 10</c>, <c>InitCostOfGoods 30</c>, <c>InitChanceOfLoosing 70</c>). A peep
/// always pays the price to play; with the complementary chance they <b>win</b> and the park hands over a prize
/// costing <c>costOfGoods</c>. The park's net for one play is therefore <c>price</c> on a loss and
/// <c>price − costOfGoods</c> on a win — across many plays the house edge is the authored margin. Unit-tested;
/// <see cref="Ride.PlaySideshow"/> supplies the random roll.
/// </summary>
public static class SideshowEconomy
{
	/// <summary>Resolve one play. <paramref name="roll"/> is in [0,1): the peep wins when it lands in the
	/// non-losing band (<c>roll ≥ chanceOfLoosing%</c>). Returns the park's net takings + whether the peep won.</summary>
	public static (float Net, bool Won) Play( int pricePerUse, int costOfGoods, int chanceOfLoosingPct, double roll )
	{
		float lose = Math.Clamp( chanceOfLoosingPct, 0, 100 ) / 100f;
		bool won = roll >= lose;
		float net = pricePerUse - (won ? costOfGoods : 0);
		return (net, won);
	}
}
