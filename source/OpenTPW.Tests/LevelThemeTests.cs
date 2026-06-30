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
	public void JungleKeepsItsCuratedRideSetForAStableDefaultPark()
	{
		// The default park must stay exactly the verified jungle set (not enumerated), so it can't drift.
		CollectionAssert.AreEqual( LevelTheme.CuratedJungleRides, LevelTheme.RideNames( "jungle" ).ToArray() );
		CollectionAssert.AreEqual( LevelTheme.CuratedJungleSideshows, LevelTheme.SideshowNames( "jungle" ).ToArray() );
		CollectionAssert.Contains( LevelTheme.RideNames( "jungle" ).ToArray(), "totem" );
	}
}
