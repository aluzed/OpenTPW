using System.Runtime.InteropServices;
using Silk.NET.OpenAL;

namespace OpenTPW;

/// <summary>
/// Game audio (T-031): decodes MPEG (.mp2) tracks with the bundled minimp3 native lib and plays them
/// through OpenAL. Currently drives a single looping background-music source. A missing audio device
/// or native decoder (headless / CI / unbuilt platform) disables playback gracefully instead of
/// throwing. NLayer (used by the ModKit previewer) mis-decoded the game's MPEG-2 Layer II music —
/// dropping ~12% of frames — so the game uses minimp3, which decodes it correctly.
/// </summary>
internal static unsafe class Audio
{
	// Bundled minimp3 wrapper (Audio/native/tpwmp3.c). Decodes a whole MPEG buffer to interleaved
	// int16 PCM (malloc'd; freed with tpw_mp3_free).
	[DllImport( "tpwmp3", CallingConvention = CallingConvention.Cdecl )]
	private static extern int tpw_mp3_decode( byte[] data, int size, out nint outPcm, out int samples, out int channels, out int hz );

	[DllImport( "tpwmp3", CallingConvention = CallingConvention.Cdecl )]
	private static extern void tpw_mp3_free( nint p );

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
			// minimp3 decodes MPEG-1/2 Layer I/II/III directly to interleaved int16 PCM.
			if ( tpw_mp3_decode( mpegData, mpegData.Length, out var ptr, out var samples, out var ch, out var hz ) != 0
				|| ptr == 0 || samples <= 0 )
				return false;

			try
			{
				pcm = new short[samples];
				Marshal.Copy( ptr, pcm, 0, samples );
			}
			finally
			{
				tpw_mp3_free( ptr );
			}

			sampleRate = hz;
			channels = ch;
			return true;
		}
		catch ( Exception e )
		{
			// DllNotFoundException on platforms without the native build, or any decode failure.
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
