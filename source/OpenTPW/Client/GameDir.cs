namespace OpenTPW;

/// <summary>
/// Performs operations that allow the quick, easy access of Theme Park World game files.
/// </summary>
public static class GameDir
{
	/// <summary>
	/// The resolved Theme Park World install path. The <c>OPENTPW_GAMEPATH</c>
	/// environment variable takes precedence over the persisted
	/// <see cref="Settings.Default.GamePath"/> setting, so the game can be pointed at any
	/// install (Linux / CI / Wine prefix) without editing config. See docs/tickets/T-006.
	/// </summary>
	public static string GamePath
	{
		get
		{
			var env = Environment.GetEnvironmentVariable( "OPENTPW_GAMEPATH" );
			return string.IsNullOrWhiteSpace( env ) ? Settings.Default.GamePath : env;
		}
	}

	/// <summary>
	/// Joins <see cref="GamePath"/> to <param name="path">path</param>.
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static string GetPath( string path )
	{
		// Normalize to the platform's native separator so paths resolve on every OS.
		// See docs/tickets/T-001.
		return Path.Join( GamePath, path )
			.Replace( '/', Path.DirectorySeparatorChar )
			.Replace( '\\', Path.DirectorySeparatorChar );
	}
}
