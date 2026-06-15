global using static OpenTPW.Common.GlobalNamespace;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class FileSystemTests
{
	private static string dataPath = "";

	public FileSystemTests()
	{
		Init();
	}

	private static void Init()
	{
		Log = new();

		// Integration tests: they need a real Theme Park World install.
		// Point OPENTPW_GAMEPATH at the game root (the folder containing "Data").
		// See docs/tickets/T-002 and T-006.
		var gamePath = Environment.GetEnvironmentVariable( "OPENTPW_GAMEPATH" )
			?? "C:\\Program Files (x86)\\Bullfrog\\Theme Park World";
		dataPath = Path.Combine( gamePath, "Data" );

		// Don't construct the file system if the data is missing: its constructor would
		// create a bogus directory. Tests become Inconclusive instead (see RequireGameData).
		if ( !Directory.Exists( dataPath ) )
			return;

		FileSystem = new BaseFileSystem( dataPath );
		FileSystem.RegisterArchiveHandler<WadArchive>( ".wad" );
		FileSystem.RegisterArchiveHandler<SdtArchive>( ".sdt" );
	}

	private static void RequireGameData()
	{
		if ( FileSystem == null || !Directory.Exists( dataPath ) )
			Assert.Inconclusive(
				$"Game data not found at '{dataPath}'. Set OPENTPW_GAMEPATH to a Theme Park "
				+ "World install to run these integration tests." );
	}

	[TestMethod]
	public void TestRead()
	{
		RequireGameData();
		Assert.IsTrue( FileSystem.ReadAllText( "Challenges.sam" ).Length > 0 );
	}

	[TestMethod]
	public void TestReadArchive()
	{
		RequireGameData();
		Assert.IsTrue( FileSystem.ReadAllText( "levels/jungle/terrain/qickload.txt" ).Length > 0 );
	}

	[TestMethod]
	public void EnumerateFiles()
	{
		RequireGameData();
		var files = FileSystem.GetFiles( "/levels" );
		var directories = FileSystem.GetDirectories( "/levels" );

		foreach ( var directory in directories )
		{
			Console.WriteLine( $"{directory}/" );
		}

		foreach ( var file in files )
		{
			Console.WriteLine( $"{file}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( directories.Length > 0 );
	}

	[TestMethod]
	public void EnumerateFilesWADArchive()
	{
		RequireGameData();
		var files = FileSystem.GetFiles( "/fonts" );

		foreach ( var item in files )
		{
			Console.WriteLine( $"{item}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( files.Any( x => x.EndsWith( "TTF" ) ) );
	}

	[TestMethod]
	public void LoadFromArchiveDirectory()
	{
		RequireGameData();
		var file = FileSystem.ReadAllBytes( "/levels/jungle/terrain/textures/jgr_bas1.wct" );

		Assert.IsTrue( file.Length > 0 );
	}

	[TestMethod]
	public void EnumerateFilesSDTArchive()
	{
		RequireGameData();
		var files = FileSystem.GetFiles( "/global/sound/AmbientHD" );

		foreach ( var item in files )
		{
			Console.WriteLine( $"{item}" );
		}

		Assert.IsTrue( files.Length > 0 );
		Assert.IsTrue( files.Any( x => x.EndsWith( "mp2" ) ) );
	}
}
