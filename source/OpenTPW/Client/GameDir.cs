namespace OpenTPW;

/// <summary>
/// Performs operations that allow the quick, easy access of Theme Park World game files.
/// </summary>
public static class GameDir
{
	/// <summary>
	/// Joins <see cref="Settings.Default.GamePath"/> to <param name="path">path</param>.
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static string GetPath( string path )
	{
		// Normalize to the platform's native separator so paths resolve on every OS.
		// See docs/tickets/T-001.
		return Path.Join( Settings.Default.GamePath, path )
			.Replace( '/', Path.DirectorySeparatorChar )
			.Replace( '\\', Path.DirectorySeparatorChar );
	}
}
