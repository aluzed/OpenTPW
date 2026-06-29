using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class SideshowEconomyTests
{
	// Jungle's authored numbers: pay 10, prize costs 30, 70% lose.
	private const int Price = 10, Goods = 30, LosePct = 70;

	[TestMethod]
	public void LosingPlayKeepsTheFullPrice()
	{
		// roll below the losing threshold (0.70) → the peep loses, park keeps the price.
		var (net, won) = SideshowEconomy.Play( Price, Goods, LosePct, 0.0 );
		Assert.IsFalse( won );
		Assert.AreEqual( 10f, net, 1e-3f );
	}

	[TestMethod]
	public void WinningPlayPaysOutTheGoods()
	{
		// roll at/above the threshold → the peep wins, park nets price − costOfGoods.
		var (net, won) = SideshowEconomy.Play( Price, Goods, LosePct, 0.7 );
		Assert.IsTrue( won );
		Assert.AreEqual( -20f, net, 1e-3f );
	}

	[TestMethod]
	public void ThresholdIsInclusiveOnTheWinSide()
	{
		Assert.IsFalse( SideshowEconomy.Play( Price, Goods, LosePct, 0.699 ).Won );
		Assert.IsTrue( SideshowEconomy.Play( Price, Goods, LosePct, 0.700 ).Won );
	}

	[TestMethod]
	public void AlwaysLoseAndAlwaysWinExtremes()
	{
		// 100% lose → never wins for any roll in [0,1).
		Assert.IsFalse( SideshowEconomy.Play( Price, Goods, 100, 0.999 ).Won );
		// 0% lose → always wins.
		Assert.IsTrue( SideshowEconomy.Play( Price, Goods, 0, 0.0 ).Won );
	}

	[TestMethod]
	public void HouseEdgeIsPositiveOverManyPlaysAtAuthoredRates()
	{
		// Expected net per play = 0.70·10 + 0.30·(10−30) = 7 − 6 = +1.
		float total = 0f;
		int n = 1000;
		for ( int i = 0; i < n; i++ )
		{
			double roll = (i + 0.5) / n; // evenly spread rolls across [0,1)
			total += SideshowEconomy.Play( Price, Goods, LosePct, roll ).Net;
		}
		float avg = total / n;
		Assert.AreEqual( 1f, avg, 0.05f, "the authored numbers give the park a small positive margin" );
	}
}
