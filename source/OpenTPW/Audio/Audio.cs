using NLayer;
using Silk.NET.OpenAL;

namespace OpenTPW;

/// <summary>
/// Game audio (T-031): decodes MPEG (.mp2) tracks with NLayer and plays them through OpenAL.
/// Currently drives a single looping background-music source. A missing audio device (headless / CI)
/// disables playback gracefully instead of throwing. The asset previewer in the ModKit uses the same
/// NLayer + OpenAL stack (see [T-003]); this is the game-side, music-oriented counterpart.
/// </summary>
internal static unsafe class Audio
{
	private static AL al = null!;
	private static ALContext alc = null!;
	private static Device* device;
	private static Context* context;

	private static uint musicSource;
	private static uint musicBuffer;

	private static bool triedInit;
	private static bool available;

	private static float musicVolume = 0.5f;

	/// <summary>Background-music gain in [0,1].</summary>
	public static float MusicVolume
	{
		get => musicVolume;
		set
		{
			musicVolume = Math.Clamp( value, 0f, 1f );
			if ( available )
				al.SetSourceProperty( musicSource, SourceFloat.Gain, musicVolume );
		}
	}

	/// <summary>
	/// Decodes an in-memory MPEG (.mp2) stream and plays it on the music source, replacing whatever
	/// was playing. Loops by default. Decode errors are logged; no device disables playback quietly.
	/// </summary>
	public static void PlayMusic( byte[] mpegData, bool loop = true )
	{
		if ( !TryDecode( mpegData, out var pcm, out var sampleRate, out var channels ) || pcm.Length == 0 )
			return;

		if ( !EnsureInitialized() )
			return;

		try
		{
			al.SourceStop( musicSource );
			al.SetSourceProperty( musicSource, SourceInteger.Buffer, 0 );
			if ( musicBuffer != 0 )
				al.DeleteBuffer( musicBuffer );

			musicBuffer = al.GenBuffer();
			var format = channels >= 2 ? BufferFormat.Stereo16 : BufferFormat.Mono16;
			fixed ( short* data = pcm )
				al.BufferData( musicBuffer, format, data, pcm.Length * sizeof( short ), sampleRate );

			al.SetSourceProperty( musicSource, SourceInteger.Buffer, (int)musicBuffer );
			al.SetSourceProperty( musicSource, SourceBoolean.Looping, loop );
			al.SetSourceProperty( musicSource, SourceFloat.Gain, musicVolume );
			al.SourcePlay( musicSource );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Music playback failed: {e.Message}" );
		}
	}

	/// <summary>Stops the music source (no-op if audio is unavailable).</summary>
	public static void StopMusic()
	{
		if ( !available )
			return;

		try { al.SourceStop( musicSource ); }
		catch ( Exception e ) { Log.Warning( $"StopMusic failed: {e.Message}" ); }
	}

	private static bool TryDecode( byte[] mpegData, out short[] pcm, out int sampleRate, out int channels )
	{
		pcm = Array.Empty<short>();
		sampleRate = 0;
		channels = 0;

		try
		{
			using var ms = new MemoryStream( mpegData );
			using var mpeg = new MpegFile( ms );

			sampleRate = mpeg.SampleRate;
			channels = mpeg.Channels;

			// NLayer yields normalized float samples (-1..1); convert to 16-bit PCM.
			var samples = new List<float>();
			var chunk = new float[16384];
			int read;
			while ( ( read = mpeg.ReadSamples( chunk, 0, chunk.Length ) ) > 0 )
			{
				for ( var i = 0; i < read; i++ )
					samples.Add( chunk[i] );
			}

			pcm = new short[samples.Count];
			for ( var i = 0; i < samples.Count; i++ )
				pcm[i] = (short)( Math.Clamp( samples[i], -1f, 1f ) * short.MaxValue );

			return true;
		}
		catch ( Exception e )
		{
			Log.Error( $"Failed to decode audio: {e.Message}" );
			return false;
		}
	}

	private static bool EnsureInitialized()
	{
		if ( triedInit )
			return available;

		triedInit = true;

		try
		{
			alc = ALContext.GetApi();
			al = AL.GetApi();

			device = alc.OpenDevice( "" );
			if ( device == null )
			{
				Log.Warning( "OpenAL: no audio device available; music disabled." );
				return false;
			}

			context = alc.CreateContext( device, null );
			alc.MakeContextCurrent( context );
			musicSource = al.GenSource();

			available = true;
			Log.Info( "OpenAL audio initialized." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"OpenAL unavailable ({e.Message}); music disabled." );
			available = false;
		}

		return available;
	}
}
