using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class GameClockTests
{
	// Half an in-game day in seconds — added to land safely past a boundary (never exactly on it).
	private const float HalfDay = GameClock.SecondsPerMonth / GameClock.DaysPerMonth * 0.5f;

	[TestMethod]
	public void DayMonthYearAdvanceWithTime()
	{
		var c = new GameClock();
		Assert.AreEqual( 1, c.Day );
		Assert.AreEqual( 1, c.Month );
		Assert.AreEqual( 1, c.Year );

		c.Tick( GameClock.SecondsPerMonth + HalfDay ); // 30.5 in-game days → start of month 2
		Assert.AreEqual( 30, c.TotalDays );
		Assert.AreEqual( 1, c.TotalMonths );
		Assert.AreEqual( 1, c.Day );
		Assert.AreEqual( 2, c.Month );
		Assert.AreEqual( 1, c.Year );

		c.Tick( GameClock.SecondsPerMonth * GameClock.MonthsPerYear ); // +12 months → next year
		Assert.AreEqual( 2, c.Year );
	}

	[TestMethod]
	public void FiresOneDayAndMonthEventPerBoundary()
	{
		int days = 0, months = 0;
		var c = new GameClock();
		c.OnNewDay += () => days++;
		c.OnNewMonth += () => months++;

		c.Tick( GameClock.SecondsPerMonth + HalfDay ); // 30.5 days = 30 day-boundaries, 1 month-boundary
		Assert.AreEqual( 30, days );
		Assert.AreEqual( 1, months );
	}

	[TestMethod]
	public void BigTickFiresEachBoundaryExactlyOnce()
	{
		int days = 0, months = 0;
		var c = new GameClock();
		c.OnNewDay += () => days++;
		c.OnNewMonth += () => months++;

		c.Tick( GameClock.SecondsPerMonth * 3f + HalfDay ); // ~90.5 days in a single big tick
		Assert.AreEqual( 90, days, "every crossed day fires once, even in one jump" );
		Assert.AreEqual( 3, months );
	}

	[TestMethod]
	public void DrivesMonthlyFinanceSettlement()
	{
		// The clock is the time source: finances settle on OnNewMonth, not their own timer.
		var fin = new ParkFinances( starting: 1000f, entryFee: 0f );
		var c = new GameClock();
		c.OnNewMonth += fin.SettleMonth;

		fin.TakeRideTicket( 50f );
		Assert.AreEqual( 0, fin.History.Count, "no settle before a month elapses" );

		c.Tick( GameClock.SecondsPerMonth + HalfDay );
		Assert.AreEqual( 1, fin.History.Count, "one month settled → one finance sample" );
		Assert.AreEqual( 1050f, fin.History[0].Balance, 1e-3f );
	}
}
