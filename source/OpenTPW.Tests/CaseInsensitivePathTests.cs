using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class CaseInsensitivePathTests
{
	// Minimal single-file DWFB (.wad) archive, like WadArchiveTests.
	private static byte[] BuildWad( string name, string content )
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true );
		var nameBytes = Encoding.ASCII.GetBytes( name + "\0" );
		var dataBytes = Encoding.ASCII.GetBytes( content );
		const int headerSize = 88, entrySize = 40;
		var nameOffset = headerSize + entrySize;
		var dataOffset = nameOffset + nameBytes.Length;
		w.Write( Encoding.ASCII.GetBytes( "DWFB" ) ); w.Write( 1 ); w.Write( new byte[64] );
		w.Write( 1 ); w.Write( headerSize ); w.Write( entrySize ); w.Write( 0 );
		w.Write( 0 ); w.Write( nameOffset ); w.Write( nameBytes.Length );
		w.Write( dataOffset ); w.Write( dataBytes.Length ); w.Write( 0 ); w.Write( dataBytes.Length ); w.Write( new byte[12] );
		w.Write( nameBytes ); w.Write( dataBytes ); w.Flush();
		return ms.ToArray();
	}

	// An archive stored as UPPERCASE (FONTS.WAD) with an UPPERCASE entry must be reachable
	// via a lowercase request — both the archive filename and the inner name (T-014).
	[TestMethod]
	public void ResolvesUppercaseArchiveAndEntryFromLowercaseRequest()
	{
		var dataDir = Path.Combine( Path.GetTempPath(), "tpw-wad-" + Guid.NewGuid().ToString( "N" ) );
		Directory.CreateDirectory( dataDir );
		File.WriteAllBytes( Path.Combine( dataDir, "FONTS.WAD" ), BuildWad( "HELLO.TXT", "hi" ) );

		try
		{
			var fs = new BaseFileSystem( dataDir );
			fs.RegisterArchiveHandler<WadArchive>( ".wad" );

			// lowercase archive name AND lowercase inner file:
			Assert.AreEqual( "hi", fs.ReadAllText( "fonts/hello.txt" ) );
		}
		finally
		{
			Directory.Delete( dataDir, recursive: true );
		}
	}

	// The disc stores assets in UPPERCASE 8.3; the game requests them lowercase.
	// On a case-sensitive FS the BaseFileSystem must still resolve them (T-014).
	[TestMethod]
	public void ResolvesUppercaseAssetsFromLowercaseRequest()
	{
		var root = Path.Combine( Path.GetTempPath(), "tpw-case-" + Guid.NewGuid().ToString( "N" ) );
		var dataDir = Path.Combine( root, "Data" );
		Directory.CreateDirectory( Path.Combine( dataDir, "LEVELS", "JUNGLE" ) );
		File.WriteAllText( Path.Combine( dataDir, "CHALLENGES.SAM" ), "x" );
		File.WriteAllText( Path.Combine( dataDir, "LEVELS", "JUNGLE", "GLOBAL.SAM" ), "data" );

		try
		{
			var fs = new BaseFileSystem( dataDir );

			Assert.IsTrue( fs.FileExists( "challenges.sam" ), "top-level file, lowercase request" );
			Assert.IsTrue( fs.DirectoryExists( "levels/jungle" ), "nested directory, lowercase request" );
			Assert.AreEqual( "data", fs.ReadAllText( "levels/jungle/global.sam" ), "nested file read" );

			// A genuinely missing file still reports missing.
			Assert.IsFalse( fs.FileExists( "levels/jungle/nope.sam" ) );
		}
		finally
		{
			Directory.Delete( root, recursive: true );
		}
	}
}
