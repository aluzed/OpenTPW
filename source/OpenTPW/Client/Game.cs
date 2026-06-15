namespace OpenTPW;

/// <summary>
/// Handles the creation and management of various systems, including the game
/// window.
/// </summary>
internal static class Game
{
	public static void Run( string[] args )
	{
		Log = new();

		// Resolve the install path once (honors the OPENTPW_GAMEPATH override). See T-006.
		var gamePath = GameDir.GamePath;

		//
		// Check if the game data directory exists
		//
		if ( !Path.Exists( Path.Join( gamePath, "data" ) ) )
			throw new DirectoryNotFoundException(
				$"Theme Park World not found at '{gamePath}'. Set OPENTPW_GAMEPATH or the "
				+ "GamePath setting to a valid install." );

		// Register game data directory
		FileSystem = new BaseFileSystem( Path.Join( gamePath, "data" ) );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );

		//
		// Check if the save data directory exists (create if not)
		//
		var savePath = Path.Join( gamePath, "save" );
		if ( !Path.Exists( savePath ) )
			Directory.CreateDirectory( savePath );

		// Register save data directory
		SaveFileSystem = new BaseFileSystem( savePath );

		//
		// Custom OpenTPW cache directory (mainly for editor-related stuff)
		//
		CacheFileSystem = new BaseFileSystem( $"./.opentpw" );

		//
		// Init renderer
		//
		Render = new();

		//
		// Create level
		//
		var level = new Level( "jungle" );

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();
	}
}
