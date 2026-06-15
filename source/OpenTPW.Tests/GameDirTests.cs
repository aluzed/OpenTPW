using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class GameDirTests
{
	[TestMethod]
	public void EnvironmentVariableOverridesGamePath()
	{
		var previous = Environment.GetEnvironmentVariable( "OPENTPW_GAMEPATH" );
		try
		{
			Environment.SetEnvironmentVariable( "OPENTPW_GAMEPATH", "/tmp/tpw-test-install" );

			// The env var takes precedence over the persisted setting (T-006).
			Assert.AreEqual( "/tmp/tpw-test-install", GameDir.GamePath );

			// GetPath joins onto it and uses only the platform's native separator (T-001).
			var joined = GameDir.GetPath( "levels/jungle/global.sam" );
			var foreign = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
			Assert.IsFalse( joined.Contains( foreign ), "path should only use the native separator" );
			StringAssert.Contains( joined, "global.sam" );
		}
		finally
		{
			Environment.SetEnvironmentVariable( "OPENTPW_GAMEPATH", previous );
		}
	}
}
