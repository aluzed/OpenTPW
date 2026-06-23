using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class PlacementGridTests
{
	private static PlacementGrid Grid() => new( 10, 8, 16f, new Vector3( 0, 0, 0 ) );

	[TestMethod]
	public void TileToWorldIsTheTileCentre()
	{
		var g = Grid();
		// Single tile (0,0): centre is half a tile in.
		var c0 = g.TileToWorld( 0, 0 );
		Assert.AreEqual( 8f, c0.X, 1e-4f );
		Assert.AreEqual( 8f, c0.Y, 1e-4f );
		Assert.AreEqual( 0f, c0.Z, 1e-4f );

		// Tile (2,3): centre at (2.5, 3.5) tiles.
		var c = g.TileToWorld( 2, 3 );
		Assert.AreEqual( 40f, c.X, 1e-4f );
		Assert.AreEqual( 56f, c.Y, 1e-4f );

		// A 4×4 footprint at (0,0) centres at (2,2) tiles = (32,32).
		var f = g.TileToWorld( 0, 0, 4, 4 );
		Assert.AreEqual( 32f, f.X, 1e-4f );
		Assert.AreEqual( 32f, f.Y, 1e-4f );
	}

	[TestMethod]
	public void WorldToTileInvertsTileToWorld()
	{
		var g = Grid();
		for ( int ty = 0; ty < g.Height; ty++ )
			for ( int tx = 0; tx < g.Width; tx++ )
			{
				var (rx, ry) = g.WorldToTile( g.TileToWorld( tx, ty ) );
				Assert.AreEqual( tx, rx );
				Assert.AreEqual( ty, ry );
			}
	}

	[TestMethod]
	public void CenteredMapsGridCentreToWorldCentre()
	{
		var g = PlacementGrid.Centered( 10, 8, 16f, new Vector3( 100, 100, 0 ) );
		// The grid's centre corner (tile 5,4) sits at the requested world centre.
		var corner = g.Origin + new Vector3( 5 * 16f, 4 * 16f, 0 );
		Assert.AreEqual( 100f, corner.X, 1e-3f );
		Assert.AreEqual( 100f, corner.Y, 1e-3f );
	}

	[TestMethod]
	public void InBoundsRespectsFootprintAndEdges()
	{
		var g = Grid(); // 10×8
		Assert.IsTrue( g.InBounds( 0, 0, 10, 8 ) );   // exactly fills
		Assert.IsFalse( g.InBounds( 8, 0, 4, 1 ) );   // 8+4 > 10
		Assert.IsFalse( g.InBounds( 0, 6, 1, 4 ) );   // 6+4 > 8
		Assert.IsFalse( g.InBounds( -1, 0, 1, 1 ) );
	}

	[TestMethod]
	public void PlacementTracksOccupancy()
	{
		var g = Grid();
		Assert.IsTrue( g.TryPlace( 2, 2, 3, 3 ) );
		Assert.IsFalse( g.CanPlace( 4, 4, 2, 2 ), "overlaps the placed footprint" );
		Assert.IsTrue( g.CanPlace( 5, 2, 3, 3 ), "adjacent, no overlap" );
		Assert.IsFalse( g.TryPlace( 0, 0, 4, 4 ), "overlaps at (2,2)" );

		g.Clear( 2, 2, 3, 3 );
		Assert.IsTrue( g.CanPlace( 4, 4, 2, 2 ), "freed after Clear" );
	}

	[TestMethod]
	public void WaterBlocksWalkingAndPlacement()
	{
		var g = Grid();
		g.MarkWater( 4, 4 );

		Assert.IsTrue( g.IsWater( 4, 4 ) );
		Assert.IsFalse( g.IsWalkable( 4, 4 ), "peeps can't stand on water" );
		Assert.IsFalse( g.CanPlace( 4, 4, 1, 1 ), "can't build on water" );
		Assert.IsFalse( g.CanPlace( 3, 3, 2, 2 ), "a footprint overlapping water is rejected" );
		Assert.AreEqual( 1, g.WaterTileCount );
		Assert.IsTrue( g.IsWalkable( 5, 5 ), "dry ground is fine" );
	}

	[TestMethod]
	public void MarkWaterFromTerrainFlagsTilesAtOrBelowLevel()
	{
		var g = Grid();
		// Sampler: tiles with world X below 40 are "low" (height 0), the rest are high (height 100).
		int marked = g.MarkWaterFromTerrain( ( x, _ ) => x < 40f ? 0f : 100f, waterLevel: 1f );

		Assert.IsTrue( marked > 0 );
		Assert.AreEqual( marked, g.WaterTileCount );
		// Tile (0,0) centre is at X=8 (<40) → water; tile (5,0) centre at X=88 (>40) → dry.
		Assert.IsTrue( g.IsWater( 0, 0 ) );
		Assert.IsFalse( g.IsWater( 5, 0 ) );
	}
}
