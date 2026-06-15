using ImGuiNET;
#if WINDOWS
using NAudio.Wave;
#endif
using Veldrid;

namespace OpenTPW.ModKit;

[HandlesExtension( ".mp2" )]
internal class SoundViewer : IFileViewer
{
	private SoundFile soundFile;

	public SoundViewer( string fileName )
	{
		soundFile = new SoundFile( fileName );
	}

#if WINDOWS
	private void PlaySound()
	{
		using var stream = new MemoryStream( soundFile.buffer );
		var audioStream = new StreamMediaFoundationReader( stream );
		var waveOut = new WaveOutEvent();

		waveOut.Init( audioStream );
		audioStream.Seek( 0, SeekOrigin.Begin );
		waveOut.Play();
	}
#endif

	public void DrawPreview()
	{
#if WINDOWS
		if ( ImGui.Button( "Play" ))
		{
			PlaySound();
		}
#else
		// Audio playback uses NAudio + Media Foundation, both Windows-only.
		// Cross-platform output is tracked in docs/tickets/T-003-naudio-not-portable.md.
		ImGui.TextDisabled( "Audio playback is only supported on Windows." );
#endif
	}

	public TextureView GetIcon()
	{
		throw new NotImplementedException();
	}
}
