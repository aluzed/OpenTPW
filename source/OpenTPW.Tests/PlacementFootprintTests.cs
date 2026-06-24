using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class PlacementFootprintTests
{
	// Footprint masks (T-052): a PlacementFootprint reserves only its solid tiles, so .hmp pieces with
	// passable cells (queue paths / fences) leave those tiles free instead of the old rectangle.

	private static PlacementGrid Grid() => new( 10, 10, 16f, new Vector3( 0, 0, 0 ) );

	// A synthetic cols×rows .HMP carrying an explicit footprint grid (1 = solid, 0 = passable).
	private static HmpFile Hmp( int cols, int rows, byte[] footprint )
	{
		int dataOff = 0x30, codeOff = dataOff + cols * rows * 25, footOff = codeOff + cols * rows;
		var d = new byte[footOff + cols * rows];
		void U16( int o, ushort v ) => BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( o, 2 ), v );
		void U32( int o, uint v ) => BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), v );

		U16( 0x00, 3 );
		U32( 0x02, HmpFile.Magic );
		U16( 0x06, 100 );
		U16( 0x08, (ushort)cols );
		U16( 0x0a, (ushort)rows );
		U32( 0x0c, (uint)dataOff );
		U32( 0x10, (uint)codeOff );
		U32( 0x14, (uint)footOff );
		Array.Copy( footprint, 0, d, footOff, footprint.Length );
		return new HmpFile( new MemoryStream( d ) );
	}

	[TestMethod]
	public void RectangleIsFullySolid()
	{
		var fp = PlacementFootprint.Rectangle( 3, 2 );
		Assert.AreEqual( 3, fp.Cols );
		Assert.AreEqual( 2, fp.Rows );
		Assert.AreEqual( 6, fp.SolidCellCount );
		Assert.IsTrue( fp.IsSolid( 2, 1 ) );
		Assert.IsFalse( fp.IsSolid( 3, 0 ), "out of the box is not solid" );
	}

	[TestMethod]
	public void FromHmpMarksPassableCells()
	{
		// 2×2 with the diagonal solid: (0,0) & (1,1) solid, (1,0) & (0,1) passable.
		var fp = PlacementFootprint.FromHmp( Hmp( 2, 2, new byte[] { 1, 0, 0, 1 } ) );
		Assert.AreEqual( 2, fp.SolidCellCount );
		Assert.IsTrue( fp.IsSolid( 0, 0 ) );
		Assert.IsFalse( fp.IsSolid( 1, 0 ) );
		Assert.IsFalse( fp.IsSolid( 0, 1 ) );
		Assert.IsTrue( fp.IsSolid( 1, 1 ) );
	}

	[TestMethod]
	public void FullySolidHmpEqualsARectangle()
	{
		var fp = PlacementFootprint.FromHmp( Hmp( 2, 3, new byte[] { 1, 1, 1, 1, 1, 1 } ) );
		Assert.AreEqual( 6, fp.SolidCellCount ); // all six tiles, like a 2×3 rectangle
	}

	[TestMethod]
	public void MaskedPlaceReservesOnlySolidTiles()
	{
		var g = Grid();
		var fp = PlacementFootprint.FromHmp( Hmp( 2, 2, new byte[] { 1, 0, 0, 1 } ) );

		Assert.IsTrue( g.TryPlace( 2, 2, fp ) );

		// Solid cells (2,2) and (3,3) are now occupied; the passable cells stay free + walkable.
		Assert.IsFalse( g.CanPlace( 2, 2, 1, 1 ), "solid cell is occupied" );
		Assert.IsFalse( g.CanPlace( 3, 3, 1, 1 ), "solid cell is occupied" );
		Assert.IsTrue( g.CanPlace( 3, 2, 1, 1 ), "passable cell stays buildable" );
		Assert.IsTrue( g.CanPlace( 2, 3, 1, 1 ), "passable cell stays buildable" );
		Assert.IsTrue( g.IsWalkable( 3, 2 ), "passable cell stays walkable" );
		Assert.IsFalse( g.IsWalkable( 2, 2 ), "solid cell blocks peeps" );
	}

	[TestMethod]
	public void MaskedClearFreesSolidTiles()
	{
		var g = Grid();
		var fp = PlacementFootprint.FromHmp( Hmp( 2, 2, new byte[] { 1, 0, 0, 1 } ) );
		g.TryPlace( 2, 2, fp );

		g.Clear( 2, 2, fp );
		Assert.IsTrue( g.CanPlace( 2, 2, 1, 1 ) );
		Assert.IsTrue( g.CanPlace( 3, 3, 1, 1 ) );
	}

	[TestMethod]
	public void CannotPlaceWhenASolidTileIsBlockedOrOffGrid()
	{
		var g = Grid();
		var fp = PlacementFootprint.FromHmp( Hmp( 2, 2, new byte[] { 1, 0, 0, 1 } ) );

		// Out of bounds: the 2×2 box runs off the 10×10 grid at (9,9).
		Assert.IsFalse( g.CanPlace( 9, 9, fp ) );

		// Water under a solid cell blocks it; water under a passable cell does not.
		g.MarkWater( 2, 2 ); // solid cell of the footprint placed at (2,2)
		Assert.IsFalse( g.CanPlace( 2, 2, fp ) );

		var g2 = Grid();
		g2.MarkWater( 3, 2 ); // a passable cell of the same footprint → still placeable
		Assert.IsTrue( g2.CanPlace( 2, 2, fp ) );
	}
}
