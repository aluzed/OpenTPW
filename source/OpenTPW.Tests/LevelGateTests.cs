using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LevelGateTests
{
	private static SettingsFile Sam( string body ) =>
		new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( body ) ) );

	[TestMethod]
	public void ReadsEntranceGateCentreFromTheTwoTileSpan()
	{
		// The real jungle entrance: A=(47,17), B=(48,17) → centre of the span at tile (48.0, 17.5).
		var s = Sam(
			"FixedItemInfo.EntranceAPosX\t47\n" +
			"FixedItemInfo.EntranceAPosY\t17\n" +
			"FixedItemInfo.EntranceBPosX\t48\n" +
			"FixedItemInfo.EntranceBPosY\t17\n" );

		var (tx, ty) = Level.ReadEntranceTile( s );
		Assert.AreEqual( 48.0f, tx, 1e-4f );
		Assert.AreEqual( 17.5f, ty, 1e-4f );
	}

	[TestMethod]
	public void FallsBackToHeightfieldCentreWhenNoEntrance()
	{
		var s = Sam( "MapInfo.HeightfieldWidth\t95\nMapInfo.HeightfieldHeight\t84\n" );
		var (tx, ty) = Level.ReadEntranceTile( s );
		Assert.AreEqual( 47.5f, tx, 1e-4f );
		Assert.AreEqual( 42f, ty, 1e-4f );
	}
}
