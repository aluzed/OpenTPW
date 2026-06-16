using ImGuiNET;
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

	public void DrawPreview()
	{
		// Cross-platform playback via AudioPlayer (NLayer + OpenAL). See docs/tickets/T-003.
		if ( ImGui.Button( "Play" ) )
		{
			AudioPlayer.Play( soundFile.buffer );
		}
	}

	public TextureView GetIcon()
	{
		throw new NotImplementedException();
	}
}
