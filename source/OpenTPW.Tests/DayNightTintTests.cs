using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class DayNightTintTests
{
	[TestMethod]
	public void NightAmountPeaksAtMidnightAndZeroAtNoon()
	{
		Assert.AreEqual( 1f, DayNightTint.NightAmount( 0f ), 1e-3f, "midnight is fully dark" );
		Assert.AreEqual( 0f, DayNightTint.NightAmount( 0.5f ), 1e-3f, "noon is fully light" );
		Assert.AreEqual( 0.5f, DayNightTint.NightAmount( 0.25f ), 1e-3f, "dawn is halfway" );
		Assert.AreEqual( 0.5f, DayNightTint.NightAmount( 0.75f ), 1e-3f, "dusk is halfway" );
	}

	[TestMethod]
	public void NightAmountIsMonotonicFromNoonToMidnight()
	{
		float prev = DayNightTint.NightAmount( 0.5f );
		for ( float f = 0.55f; f <= 1.0f; f += 0.05f )
		{
			float now = DayNightTint.NightAmount( f );
			Assert.IsTrue( now >= prev - 1e-4f, $"darkness should rise from noon to midnight (f={f})" );
			prev = now;
		}
	}

	[TestMethod]
	public void NightAmountWrapsAndStaysInRange()
	{
		Assert.AreEqual( DayNightTint.NightAmount( 0f ), DayNightTint.NightAmount( 1f ), 1e-3f, "the day wraps" );
		for ( float f = -0.3f; f <= 1.3f; f += 0.1f )
		{
			float a = DayNightTint.NightAmount( f );
			Assert.IsTrue( a is >= -1e-4f and <= 1.0001f, $"night amount in [0,1] (f={f})" );
		}
	}

	[TestMethod]
	public void PhaseLabelsTrackTheClock()
	{
		Assert.AreEqual( "NIGHT", DayNightTint.Phase( 0.0f ) );
		Assert.AreEqual( "DAWN", DayNightTint.Phase( 0.27f ) );
		Assert.AreEqual( "DAY", DayNightTint.Phase( 0.5f ) );
		Assert.AreEqual( "DUSK", DayNightTint.Phase( 0.75f ) );
		Assert.AreEqual( "NIGHT", DayNightTint.Phase( 0.95f ) );
	}
}
