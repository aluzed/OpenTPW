using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class CaseInsensitivePathTests
{
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
