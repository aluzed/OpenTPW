using System.IO;

namespace OpenTPW;

/// <summary>
/// Drives the weather soundscape (T-056): a looping rain bed while it's precipitating and the odd thunder clap
/// during a lightning storm, both from the real <c>AmbientHD.sdt</c> bank (<c>RAIN.mp2</c> / <c>Thunder*.mp2</c>).
/// Polled once per frame from <see cref="Level.Update"/>; it watches <see cref="WeatherSim.Current"/> and
/// starts/stops the loop on a state change, so it also follows the <c>OPENTPW_WEATHER</c> demo pin. Audio is a
/// quiet no-op when the device or the assets are unavailable.
/// </summary>
public static class WeatherAudio
{
	private static bool loaded;
	private static byte[]? rain;
	private static byte[][] thunder = Array.Empty<byte[]>();

	private static bool raining;     // is the rain loop currently playing?
	private static float nextThunder; // Time.Now at which the next thunder clap may sound

	/// <summary>Reset between levels so a fresh park re-evaluates the loop from silence.</summary>
	public static void Reset()
	{
		if ( raining )
			Audio.StopWeatherLoop();
		raining = false;
		nextThunder = 0f;
	}

	private static void EnsureAssets()
	{
		if ( loaded )
			return;
		loaded = true;
		try
		{
			var path = Path.Join( GameDir.GamePath, "data", "global", "sound", "AmbientHD.sdt" );
			if ( !File.Exists( path ) )
				return;
			var bank = new SdtArchive( path );
			byte[]? Find( string stem ) => bank.soundFiles
				.FirstOrDefault( f => f.Name.StartsWith( stem, StringComparison.OrdinalIgnoreCase ) )?.SoundData;

			rain = Find( "RAIN" );
			thunder = new[] { "Thunder2", "Thunder3", "Thunder4" }
				.Select( Find ).Where( b => b != null ).Select( b => b! ).ToArray();
		}
		catch ( Exception e ) { Log.Warning( $"[weather] audio assets unavailable: {e.Message}" ); }
	}

	/// <summary>Per-frame: match the rain loop to the current weather and clap thunder during a storm.</summary>
	public static void Update()
	{
		var state = WeatherSim.Current?.State;
		bool wantRain = state is { IsClear: false };

		if ( wantRain && !raining )
		{
			EnsureAssets();
			if ( rain != null )
			{
				Audio.PlayWeatherLoop( rain );
				raining = true;
				Log.Info( "[weather] rain loop started" );
			}
		}
		else if ( !wantRain && raining )
		{
			Audio.StopWeatherLoop();
			raining = false;
		}

		// Thunder during a lightning storm: an occasional clap, spaced a few seconds apart.
		if ( state is { Lightning: true } && thunder.Length > 0 )
		{
			if ( Time.Now >= nextThunder )
			{
				// First sighting (nextThunder==0) just primes the timer so we don't clap instantly on load.
				if ( nextThunder > 0f )
				{
					var clip = thunder[Random.Shared.Next( thunder.Length )];
					Audio.PlaySfx( $"thunder{Random.Shared.Next( thunder.Length )}", clip );
				}
				nextThunder = Time.Now + 5f + (float)Random.Shared.NextDouble() * 5f;
			}
		}
		else
		{
			nextThunder = 0f; // re-prime when the storm passes
		}
	}
}
