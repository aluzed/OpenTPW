using System.Globalization;

namespace OpenTPW;

/// <summary>What the sky is doing right now. Rain/snow are mutually exclusive; <see cref="Lightning"/> is an
/// extra flag that rides on top of the worst weather (a thunderstorm).</summary>
public enum WeatherKind { Clear, Rain, Snow }

/// <summary>The current visible weather — a kind plus whether it's also storming.</summary>
public readonly record struct WeatherState( WeatherKind Kind, bool Lightning )
{
	public bool IsClear => Kind == WeatherKind.Clear && !Lightning;
}

/// <summary>One of the level's four seasons (<c>Seasons[0..3]</c> in <c>Standard.sam</c>): a target weather
/// quality the sim rolls around, how far it normally strays (<c>NormalTolerance</c>), and the % chance a roll
/// breaks past that tolerance (<c>ChanceForExceptionalWeather</c>). Higher quality = nicer weather.</summary>
public readonly record struct SeasonInfo( int AvgWeatherQuality, int NormalTolerance, int ChanceForExceptionalWeather );

/// <summary>
/// The level's authored weather model (T-056), parsed from <c>Seasons[*]</c> / <c>Weather.*</c> /
/// <c>WeatherEffects.*</c> in the level <c>Standard.sam</c>. A quality value (0..100) maps to clear / rain /
/// snow / lightning via the <c>QualityFor{Rain,Snow,Lightning}{Low,High}</c> ranges; a Low or High of -1 means
/// that effect is disabled for this level (e.g. jungle never snows).
/// </summary>
public readonly record struct WeatherConfig(
	SeasonInfo[] Seasons, int DaysBetweenChanges, int DaysOfWarning, int SpeedOfChange,
	int RainLow, int RainHigh, int SnowLow, int SnowHigh, int LightningLow, int LightningHigh )
{
	/// <summary>A neutral fallback: always-clear weather that never changes (used when a level omits the block).</summary>
	public static WeatherConfig Clear => new(
		new[] { new SeasonInfo( 100, 0, 0 ), new SeasonInfo( 100, 0, 0 ), new SeasonInfo( 100, 0, 0 ), new SeasonInfo( 100, 0, 0 ) },
		DaysBetweenChanges: 7, DaysOfWarning: 4, SpeedOfChange: 20,
		RainLow: -1, RainHigh: -1, SnowLow: -1, SnowHigh: -1, LightningLow: -1, LightningHigh: -1 );

	/// <summary>Parse the weather block from a level settings file. Missing keys fall back to <see cref="Clear"/>'s
	/// values, so a level without a weather block stays sunny rather than throwing.</summary>
	public static WeatherConfig ParseLocal( SettingsFile sam )
	{
		int I( string key, int fallback )
			=> int.TryParse( sam[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v ) ? v : fallback;

		var seasons = new SeasonInfo[4];
		for ( int i = 0; i < 4; i++ )
			seasons[i] = new SeasonInfo(
				AvgWeatherQuality: I( $"Seasons[{i}].AvgWeatherQuality", 100 ),
				NormalTolerance: I( $"Seasons[{i}].NormalTolerance", 0 ),
				ChanceForExceptionalWeather: I( $"Seasons[{i}].ChanceForExceptionalWeather", 0 ) );

		return new WeatherConfig(
			Seasons: seasons,
			DaysBetweenChanges: Math.Max( 1, I( "Weather.DaysBetweenChanges", 7 ) ),
			DaysOfWarning: I( "Weather.DaysOfWarning", 4 ),
			SpeedOfChange: I( "Weather.SpeedOfChange", 20 ),
			RainLow: I( "WeatherEffects.QualityForRainLow", -1 ),
			RainHigh: I( "WeatherEffects.QualityForRainHigh", -1 ),
			SnowLow: I( "WeatherEffects.QualityForSnowLow", -1 ),
			SnowHigh: I( "WeatherEffects.QualityForSnowHigh", -1 ),
			LightningLow: I( "WeatherEffects.QualityForLightningLow", -1 ),
			LightningHigh: I( "WeatherEffects.QualityForLightningHigh", -1 ) );
	}
}

/// <summary>Pure weather maths (T-056): season selection, quality→state classification, and the per-change
/// quality roll. No game state — unit-tested in isolation; <see cref="WeatherSim"/> drives it off the clock.</summary>
public static class Weather
{
	/// <summary>Which season (0..3) an in-game month (1..12) falls in — three months each, matching the four
	/// <c>Seasons[*]</c> entries.</summary>
	public static int SeasonForMonth( int month )
		=> ((Math.Max( 1, month ) - 1) / 3) & 3;

	// An effect is active when both bounds are >= 0 (a -1 bound disables it) and the quality is within range.
	private static bool InRange( int quality, int low, int high )
		=> low >= 0 && high >= 0 && quality >= low && quality <= high;

	/// <summary>Map a weather quality (0..100, lower = worse) to the visible state via the config's effect ranges.
	/// Snow takes precedence over rain when both apply; lightning rides on top of any non-clear weather.</summary>
	public static WeatherState Classify( int quality, WeatherConfig cfg )
	{
		var kind = WeatherKind.Clear;
		if ( InRange( quality, cfg.SnowLow, cfg.SnowHigh ) )
			kind = WeatherKind.Snow;
		else if ( InRange( quality, cfg.RainLow, cfg.RainHigh ) )
			kind = WeatherKind.Rain;

		bool lightning = kind != WeatherKind.Clear && InRange( quality, cfg.LightningLow, cfg.LightningHigh );
		return new WeatherState( kind, lightning );
	}

	// Happiness lost per second by an exposed (outdoor, not-sheltered) peep, by weather. Tuned against the
	// other mood penalties (wait 1/s, litter 1.5/s) so bad weather is a steady nudge home, not an instant wipe.
	private const float RainDiscomfort = 1.5f;
	private const float SnowDiscomfort = 2.0f;
	private const float LightningDiscomfort = 1.0f; // extra, on top of rain/snow, during a storm

	/// <summary>How fast the current weather sours an exposed peep's mood (happiness/second). Clear = 0; rain
	/// and snow each have a base rate, with an extra bite during a lightning storm. Pure — peeps multiply this
	/// by <c>Time.Delta</c> while outdoors (T-056). Sheltered peeps (riding / under an indoor stall) are exempt.</summary>
	public static float ComfortPenaltyPerSec( WeatherState state )
	{
		float penalty = state.Kind switch
		{
			WeatherKind.Rain => RainDiscomfort,
			WeatherKind.Snow => SnowDiscomfort,
			_ => 0f,
		};
		if ( penalty > 0f && state.Lightning )
			penalty += LightningDiscomfort;
		return penalty;
	}

	/// <summary>Roll a new weather quality for a season. <paramref name="uniform"/> and
	/// <paramref name="exceptionalRoll"/> are both in [0,1) (the caller supplies the randomness, keeping this
	/// pure). The roll is the season average ± its tolerance; an "exceptional" roll (chance =
	/// <see cref="SeasonInfo.ChanceForExceptionalWeather"/>%) doubles that swing. Result is clamped to 0..100.</summary>
	public static int RollQuality( SeasonInfo s, double uniform, double exceptionalRoll )
	{
		bool exceptional = s.ChanceForExceptionalWeather > 0
			&& exceptionalRoll * 100.0 < s.ChanceForExceptionalWeather;
		double tolerance = exceptional ? s.NormalTolerance * 2.0 : s.NormalTolerance;
		double quality = s.AvgWeatherQuality + (uniform * 2.0 - 1.0) * tolerance;
		return Math.Clamp( (int)Math.Round( quality ), 0, 100 );
	}
}

/// <summary>
/// Drives the live weather (T-056): rolls a fresh quality for the current season every
/// <see cref="WeatherConfig.DaysBetweenChanges"/> in-game days (on <see cref="GameClock.OnNewDay"/>), classifies
/// it to a <see cref="WeatherState"/> the overlay/HUD read, and announces an upcoming change
/// <see cref="WeatherConfig.DaysOfWarning"/> days out. One <see cref="Current"/> per level; the pure maths live
/// in <see cref="Weather"/>.
/// </summary>
public sealed class WeatherSim
{
	/// <summary>The active level's weather (overlay + HUD read this); null before a level loads.</summary>
	public static WeatherSim? Current { get; set; }

	private readonly WeatherConfig cfg;
	private readonly Random rng;
	private int daysUntilChange;
	private bool warned;
	private bool forced; // a dev/demo pin (OPENTPW_WEATHER) freezes the daily roll

	/// <summary>The weather right now.</summary>
	public WeatherState State { get; private set; }
	/// <summary>The current weather quality (0..100, lower = worse) — what <see cref="State"/> was classified from.</summary>
	public int Quality { get; private set; }
	/// <summary>The current season index (0..3), derived from the clock's month.</summary>
	public int Season => Weather.SeasonForMonth( GameClock.Current?.Month ?? 1 );

	public WeatherSim( WeatherConfig cfg, Random? rng = null )
	{
		this.cfg = cfg;
		this.rng = rng ?? new Random();
		daysUntilChange = cfg.DaysBetweenChanges;
		Roll(); // start on a season-appropriate sample rather than a hardcoded clear day
	}

	/// <summary>Force a specific weather state (a dev/demo hook — <c>OPENTPW_WEATHER</c>): pins the visible
	/// weather regardless of the rolled quality, so the overlay can be exercised on a level that rarely produces
	/// it (jungle summers never dip into the rain band, and never snow at all).</summary>
	public void Force( WeatherState state, int quality )
	{
		State = state;
		Quality = quality;
		forced = true; // keep the daily roll from overwriting the pinned state
	}

	/// <summary>Advance one in-game day; rolls fresh weather when the change interval elapses.</summary>
	public void OnNewDay()
	{
		if ( forced )
			return;
		daysUntilChange--;
		if ( !warned && daysUntilChange == cfg.DaysOfWarning )
		{
			warned = true;
			Log.Info( $"[weather] a change is coming in {cfg.DaysOfWarning} day(s)" );
		}
		if ( daysUntilChange <= 0 )
		{
			daysUntilChange = cfg.DaysBetweenChanges;
			warned = false;
			Roll();
		}
	}

	private void Roll()
	{
		var season = cfg.Seasons[Season % cfg.Seasons.Length];
		Quality = Weather.RollQuality( season, rng.NextDouble(), rng.NextDouble() );
		var previous = State;
		State = Weather.Classify( Quality, cfg );
		if ( State != previous )
			Log.Info( $"[weather] season {Season}: quality {Quality} → {State.Kind}{(State.Lightning ? " + lightning" : "")}" );
	}
}
