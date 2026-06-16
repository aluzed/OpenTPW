using Veldrid;

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
		// Show a loading screen while the level loads synchronously (otherwise the window is
		// black and the WM marks it "not responding"). A title font draws "LOADING…" over a
		// sky-blue clear; if the font can't be loaded we still show the colour, never black.
		//
		Font? loadingFont = null;
		try { loadingFont = new Font( "Language/English/GAME12.bf4" ); }
		catch ( Exception e ) { Log.Warning( $"Loading-screen font unavailable: {e.Message}" ); }

		Render.ClearColor = new RgbaFloat( 0.35f, 0.72f, 0.92f, 1f );

		void DrawLoading()
		{
			if ( loadingFont != null )
				Graphics.DrawText( loadingFont, "LOADING...", Screen.Size.X / 2f, Screen.Size.Y / 2f, TextAlign.Center, scale: 4f );
		}

		// Show the loading screen for a brief minimum so it's actually visible (and keep the
		// window responsive by re-presenting/pumping each iteration).
		var loadingStart = System.Diagnostics.Stopwatch.StartNew();
		do
		{
			Render.RenderLoadingScreen( DrawLoading );
		}
		while ( loadingStart.ElapsedMilliseconds < 1500 );

		//
		// Create level
		//
		var level = new Level( "jungle" );
		Render.ClearColor = RgbaFloat.Black;

		//
		// Run game loop
		//
		Render.OnUpdate += level.Update;
		Render.OnRender += level.Render;
		Render.Run();
	}
}
