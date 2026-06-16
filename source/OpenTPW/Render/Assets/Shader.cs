using System.Collections.Concurrent;
using Veldrid;
using Vortice.Direct3D11;
using Vortice.Win32;

namespace OpenTPW;

public class Shader : Asset
{
	// Shaders flagged dirty by the hot-reload FileSystemWatcher (which fires on a worker thread).
	// The render thread drains this each frame instead of scanning all loaded assets. See T-028.
	public static readonly ConcurrentQueue<Shader> DirtyShaders = new();

	private ShaderInfo shaderInfo;

	public VertexElementDescription[] VertexElements => shaderInfo.Reflection.VertexElements;
	public ResourceLayoutDescription[] ResourceLayouts => shaderInfo.Reflection.ResourceLayouts;
	public Veldrid.Shader[] ShaderProgram => shaderInfo.ShaderProgram;
	public bool IsDirty { get; private set; }
	public Action OnRecompile { get; set; } = null!;

	private FileSystemWatcher watcher;

	internal Shader( string path )
	{
		Path = path;
		All.Add( this );

		Recompile();

		var directoryName = System.IO.Path.GetDirectoryName( Path );
		var fileName = System.IO.Path.GetFileName( Path );

		watcher = new FileSystemWatcher( directoryName!, fileName );

		// Only react to actual edits (write/size). Crucially NOT LastAccess: Recompile() opens the
		// shader file for reading, which updates LastAccess — watching it would re-fire OnWatcherChanged
		// after every recompile and re-enqueue the shader forever (an infinite recompile loop). See T-028.
		watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;

		watcher.EnableRaisingEvents = true;
		watcher.Changed += OnWatcherChanged;
	}

	public static bool IsFileReady( string path )
	{
		try
		{
			using ( FileStream inputStream = File.OpenRead( path ) )
				return inputStream.Length > 0;
		}
		catch ( Exception )
		{
			return false;
		}
	}

	private void OnWatcherChanged( object sender, FileSystemEventArgs e )
	{
		// Best-effort de-dup: only enqueue once per dirty episode (Recompile clears IsDirty). A
		// rare race just causes a harmless extra Recompile.
		if ( IsDirty )
			return;

		IsDirty = true;
		DirtyShaders.Enqueue( this );
	}

	public void Recompile()
	{
		if ( !IsFileReady( Path ) )
			return;

		shaderInfo = ShaderCompiler.CompileShader( Path );
		OnRecompile?.Invoke();
		IsDirty = false;
	}
}
