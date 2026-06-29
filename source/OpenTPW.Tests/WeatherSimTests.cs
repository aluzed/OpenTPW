using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class WeatherSimTests
{
	// The real jungle Standard.sam weather block (lower quality = worse weather; snow disabled with -1).
	private const string Sam = @"
Seasons[0].AvgWeatherQuality	75
Seasons[0].NormalTolerance		25
Seasons[0].ChanceForExceptionalWeather	10
Seasons[1].AvgWeatherQuality	80
Seasons[1].NormalTolerance		20
Seasons[1].ChanceForExceptionalWeather	10
Seasons[2].AvgWeatherQuality	90
Seasons[2].NormalTolerance		10
Seasons[2].ChanceForExceptionalWeather	5
Seasons[3].AvgWeatherQuality	50
Seasons[3].NormalTolerance		15
Seasons[3].ChanceForExceptionalWeather	10
Weather.DaysOfWarning			4
Weather.DaysBetweenChanges		7
Weather.SpeedOfChange			20
WeatherEffects.QualityForSnowLow	-1
WeatherEffects.QualityForSnowHigh	-1
WeatherEffects.QualityForRainLow	0
WeatherEffects.QualityForRainHigh	40
WeatherEffects.QualityForLightningLow	0
WeatherEffects.QualityForLightningHigh	15
";

	private static WeatherConfig Cfg()
		=> WeatherConfig.ParseLocal( new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( Sam ) ) ) );

	[TestMethod]
	public void ParsesSeasonsAndEffectRanges()
	{
		var c = Cfg();
		Assert.AreEqual( 4, c.Seasons.Length );
		Assert.AreEqual( 75, c.Seasons[0].AvgWeatherQuality );
		Assert.AreEqual( 25, c.Seasons[0].NormalTolerance );
		Assert.AreEqual( 10, c.Seasons[0].ChanceForExceptionalWeather );
		Assert.AreEqual( 90, c.Seasons[2].AvgWeatherQuality );
		Assert.AreEqual( 50, c.Seasons[3].AvgWeatherQuality );

		Assert.AreEqual( 7, c.DaysBetweenChanges );
		Assert.AreEqual( 4, c.DaysOfWarning );
		Assert.AreEqual( 0, c.RainLow );
		Assert.AreEqual( 40, c.RainHigh );
		Assert.AreEqual( -1, c.SnowLow, "jungle never snows" );
		Assert.AreEqual( 15, c.LightningHigh );
	}

	[TestMethod]
	public void MissingBlockFallsBackToClear()
	{
		var c = WeatherConfig.ParseLocal( new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( "Foo.Bar 1\n" ) ) ) );
		// No effects enabled → everything classifies clear; no exceptions thrown.
		Assert.IsTrue( Weather.Classify( 0, c ).IsClear );
		Assert.IsTrue( Weather.Classify( 100, c ).IsClear );
		Assert.AreEqual( 4, c.Seasons.Length );
	}

	[TestMethod]
	public void SeasonForMonthGroupsThreeMonthsEach()
	{
		Assert.AreEqual( 0, Weather.SeasonForMonth( 1 ) );
		Assert.AreEqual( 0, Weather.SeasonForMonth( 3 ) );
		Assert.AreEqual( 1, Weather.SeasonForMonth( 4 ) );
		Assert.AreEqual( 2, Weather.SeasonForMonth( 9 ) );
		Assert.AreEqual( 3, Weather.SeasonForMonth( 12 ) );
		// Clamps a bad month into season 0 rather than throwing.
		Assert.AreEqual( 0, Weather.SeasonForMonth( 0 ) );
	}

	[TestMethod]
	public void ClassifyMapsQualityToState()
	{
		var c = Cfg();
		// High quality = clear.
		Assert.AreEqual( WeatherKind.Clear, Weather.Classify( 80, c ).Kind );
		Assert.IsFalse( Weather.Classify( 80, c ).Lightning );
		// 0..40 = rain; 0..15 also storms.
		Assert.AreEqual( WeatherKind.Rain, Weather.Classify( 30, c ).Kind );
		Assert.IsFalse( Weather.Classify( 30, c ).Lightning, "30 > lightning-high 15" );
		var storm = Weather.Classify( 5, c );
		Assert.AreEqual( WeatherKind.Rain, storm.Kind );
		Assert.IsTrue( storm.Lightning, "5 is within 0..15 lightning band" );
		// Boundaries are inclusive.
		Assert.AreEqual( WeatherKind.Rain, Weather.Classify( 40, c ).Kind );
		Assert.AreEqual( WeatherKind.Clear, Weather.Classify( 41, c ).Kind );
	}

	[TestMethod]
	public void SnowTakesPrecedenceOverRainWhenBothEnabled()
	{
		// A hypothetical cold level: snow 0..20 overlaps rain 0..40.
		var c = WeatherConfig.Clear with { SnowLow = 0, SnowHigh = 20, RainLow = 0, RainHigh = 40 };
		Assert.AreEqual( WeatherKind.Snow, Weather.Classify( 10, c ).Kind, "snow wins inside the overlap" );
		Assert.AreEqual( WeatherKind.Rain, Weather.Classify( 30, c ).Kind, "above snow-high but in rain range" );
	}

	[TestMethod]
	public void DisabledEffectNeverTriggers()
	{
		var c = WeatherConfig.Clear; // all ranges -1
		for ( int q = 0; q <= 100; q += 10 )
			Assert.IsTrue( Weather.Classify( q, c ).IsClear, $"q={q} should be clear when all effects disabled" );
	}

	[TestMethod]
	public void RollQualityStaysWithinToleranceWhenNotExceptional()
	{
		var s = new SeasonInfo( AvgWeatherQuality: 75, NormalTolerance: 25, ChanceForExceptionalWeather: 0 );
		// exceptionalRoll = 1.0 → never exceptional (0% chance anyway). Extremes of uniform:
		Assert.AreEqual( 50, Weather.RollQuality( s, 0.0, 1.0 ) ); // 75 - 25
		Assert.AreEqual( 75, Weather.RollQuality( s, 0.5, 1.0 ) ); // centre
		Assert.AreEqual( 100, Weather.RollQuality( s, 1.0, 1.0 ) ); // 75 + 25
	}

	[TestMethod]
	public void RollQualityWidensAndClampsWhenExceptional()
	{
		var s = new SeasonInfo( AvgWeatherQuality: 50, NormalTolerance: 30, ChanceForExceptionalWeather: 100 );
		// exceptionalRoll 0.0 < 100% → exceptional → tolerance doubles to 60.
		Assert.AreEqual( 0, Weather.RollQuality( s, 0.0, 0.0 ), "50 - 60 clamped to 0" );
		Assert.AreEqual( 100, Weather.RollQuality( s, 1.0, 0.0 ), "50 + 60 clamped to 100" );
	}

	[TestMethod]
	public void ComfortPenaltyIsZeroWhenClearAndWorseInStorms()
	{
		Assert.AreEqual( 0f, Weather.ComfortPenaltyPerSec( new WeatherState( WeatherKind.Clear, false ) ), 1e-3f );

		float rain = Weather.ComfortPenaltyPerSec( new WeatherState( WeatherKind.Rain, false ) );
		float snow = Weather.ComfortPenaltyPerSec( new WeatherState( WeatherKind.Snow, false ) );
		float storm = Weather.ComfortPenaltyPerSec( new WeatherState( WeatherKind.Rain, true ) );

		Assert.IsTrue( rain > 0f, "rain is uncomfortable" );
		Assert.IsTrue( snow > rain, "snow is worse than rain" );
		Assert.IsTrue( storm > rain, "a lightning storm adds extra discomfort" );
		// A bare Lightning flag with no rain/snow (Clear kind) still reads as clear → no penalty.
		Assert.AreEqual( 0f, Weather.ComfortPenaltyPerSec( new WeatherState( WeatherKind.Clear, true ) ), 1e-3f );
	}

	[TestMethod]
	public void SimRollsFreshWeatherOnTheChangeInterval()
	{
		// The manager logs on weather changes; give it a real logger so the static Log isn't null in tests.
		OpenTPW.Common.GlobalNamespace.Log ??= new Logger();

		// Deterministic RNG; force a wet season so a change is observable.
		var c = Cfg() with { /* season 0 avg 75 tol 25 → can dip into rain */ };
		var sim = new WeatherSim( c, new Random( 1234 ) );

		// Drive a year of days; the quality should take at least two distinct values over many intervals.
		var seen = new System.Collections.Generic.HashSet<int> { sim.Quality };
		for ( int d = 0; d < 7 * 20; d++ )
		{
			sim.OnNewDay();
			seen.Add( sim.Quality );
		}
		Assert.IsTrue( seen.Count > 1, "weather quality should vary across change intervals" );
	}
}
