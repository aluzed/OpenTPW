using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LevelThemeTests
{
	[TestMethod]
	public void ResolveKeepsKnownThemesCaseInsensitively()
	{
		Assert.AreEqual( "jungle", LevelTheme.Resolve( "jungle" ) );
		Assert.AreEqual( "space", LevelTheme.Resolve( "SPACE" ) );
		Assert.AreEqual( "fantasy", LevelTheme.Resolve( "  Fantasy  " ) );
		Assert.AreEqual( "hallow", LevelTheme.Resolve( "hallow" ) );
	}

	[TestMethod]
	public void ResolveFallsBackToDefaultForUnknownOrEmpty()
	{
		Assert.AreEqual( LevelTheme.Default, LevelTheme.Resolve( null ) );
		Assert.AreEqual( LevelTheme.Default, LevelTheme.Resolve( "" ) );
		Assert.AreEqual( LevelTheme.Default, LevelTheme.Resolve( "   " ) );
		Assert.AreEqual( LevelTheme.Default, LevelTheme.Resolve( "atlantis" ), "an unknown theme never breaks startup" );
		Assert.AreEqual( "jungle", LevelTheme.Default );
	}

	[TestMethod]
	public void MapsThemeFoldersToTheirAuthenticDisplayNames()
	{
		// Decoded from THEMENAMES.str, index-aligned with LevelTheme.Known.
		var names = new[] { "Lost Kingdom", "Halloween World", "Wonder Land", "Space Zone" };
		Assert.AreEqual( "Lost Kingdom", LevelTheme.MapDisplayName( "jungle", names ) );
		Assert.AreEqual( "Halloween World", LevelTheme.MapDisplayName( "hallow", names ) );
		Assert.AreEqual( "Wonder Land", LevelTheme.MapDisplayName( "fantasy", names ) );
		Assert.AreEqual( "Space Zone", LevelTheme.MapDisplayName( "space", names ) );
	}

	[TestMethod]
	public void DisplayNameFallsBackToFolderWhenStringsMissingOrUnknown()
	{
		Assert.AreEqual( "jungle", LevelTheme.MapDisplayName( "jungle", System.Array.Empty<string>() ), "no strings → folder name" );
		Assert.AreEqual( "space", LevelTheme.MapDisplayName( "space", new[] { "Lost Kingdom" } ), "index out of range → folder name" );
		Assert.AreEqual( "atlantis", LevelTheme.MapDisplayName( "atlantis", new[] { "A", "B", "C", "D" } ), "unknown folder → itself" );
		Assert.AreEqual( "hallow", LevelTheme.MapDisplayName( "hallow", new[] { "X", "  ", "Y", "Z" } ), "blank entry → folder name" );
	}

	[TestMethod]
	public void JungleKeepsItsCuratedRideSetForAStableDefaultPark()
	{
		// The default park must stay exactly the verified jungle set (not enumerated), so it can't drift.
		CollectionAssert.AreEqual( LevelTheme.CuratedJungleRides, LevelTheme.RideNames( "jungle" ).ToArray() );
		CollectionAssert.AreEqual( LevelTheme.CuratedJungleSideshows, LevelTheme.SideshowNames( "jungle" ).ToArray() );
		CollectionAssert.Contains( LevelTheme.RideNames( "jungle" ).ToArray(), "totem" );
	}
}
