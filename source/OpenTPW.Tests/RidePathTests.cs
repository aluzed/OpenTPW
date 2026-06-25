using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RidePathTests
{
	// T-048: the car loop traces the ride's authored footprint (its .sam shape) and passes the entrance,
	// instead of a generic ellipse. (The exact authored track is sim output, not file data — this is a
	// footprint-shaped stand-in.) These cover the pure ring tracer + the Catmull-Rom smoother.

	private static bool[,] Full( int w, int h )
	{
		var c = new bool[w, h];
		for ( int y = 0; y < h; y++ )
			for ( int x = 0; x < w; x++ )
				c[x, y] = true;
		return c;
	}

	private static int Chebyshev( (int X, int Y) a, (int X, int Y) b )
		=> Math.Max( Math.Abs( a.X - b.X ), Math.Abs( a.Y - b.Y ) );

	[TestMethod]
	public void RingIsTheFootprintPerimeterInLoopOrder()
	{
		// A full 3×3: the 8 border tiles are the perimeter (the centre is interior), ordered as a ring so
		// each consecutive tile (incl. wrap) is adjacent.
		var ring = RidePath.FootprintRing( Full( 3, 3 ), entrance: null );

		Assert.AreEqual( 8, ring.Count, "all border tiles, the centre excluded" );
		Assert.IsFalse( ring.Contains( (1, 1) ), "the interior tile is not on the ring" );
		CollectionAssert.AllItemsAreUnique( ring );

		for ( int i = 0; i < ring.Count; i++ )
			Assert.AreEqual( 1, Chebyshev( ring[i], ring[(i + 1) % ring.Count] ),
				"consecutive ring tiles are adjacent (a proper loop)" );
	}

	[TestMethod]
	public void RingStartsAtTheEntrance()
	{
		var ring = RidePath.FootprintRing( Full( 3, 3 ), entrance: (2, 2) );
		Assert.AreEqual( (2, 2), ring[0], "the loop is rotated to start at the boarding tile" );
		Assert.AreEqual( 8, ring.Count );
	}

	[TestMethod]
	public void NonRectangularFootprintTracesItsShape()
	{
		// An L-shape (top-left 2×2 missing): 12 occupied tiles, one of which — (2,2) — has all four
		// neighbours occupied, so it's interior; the other 11 form the traced perimeter ring.
		var c = Full( 4, 4 );
		c[0, 0] = c[1, 0] = c[0, 1] = c[1, 1] = false;
		var ring = RidePath.FootprintRing( c, null );

		Assert.AreEqual( 11, ring.Count, "12 occupied − 1 interior tile (2,2)" );
		Assert.IsFalse( ring.Contains( (0, 0) ), "removed tile is not on the ring" );
		Assert.IsFalse( ring.Contains( (2, 2) ), "the interior tile is excluded" );
	}

	[TestMethod]
	public void DegenerateFootprintsFallBack()
	{
		Assert.AreEqual( 0, RidePath.FootprintRing( Full( 1, 4 ), null ).Count, "a 1-wide strip is degenerate" );
		Assert.AreEqual( 0, RidePath.FootprintRing( Full( 4, 1 ), null ).Count, "a 1-tall strip is degenerate" );

		var sparse = new bool[3, 3];
		sparse[0, 0] = sparse[2, 2] = true; // 2 occupied tiles
		Assert.AreEqual( 0, RidePath.FootprintRing( sparse, null ).Count, "fewer than 3 tiles → fall back" );
	}

	[TestMethod]
	public void SmoothClosesTheLoopAndSubdivides()
	{
		var square = new List<Vector3>
		{
			new( 0, 0, 0 ), new( 10, 0, 0 ), new( 10, 10, 0 ), new( 0, 10, 0 ),
		};
		var path = RidePath.Smooth( square, closed: true, sub: 4 );

		Assert.AreEqual( 4 * 4 + 1, path.Count, "4 segments × 4 subdivisions + the closing point" );
		Assert.AreEqual( path[0].X, path[^1].X, 1e-4f, "closed: last point returns to the first" );
		Assert.AreEqual( path[0].Y, path[^1].Y, 1e-4f );
	}

	[TestMethod]
	public void SmoothKeepsTooFewPointsStraight()
	{
		var two = new List<Vector3> { new( 0, 0, 0 ), new( 5, 0, 0 ) };
		var path = RidePath.Smooth( two, closed: true, sub: 4 );
		Assert.AreEqual( 2, path.Count, "fewer than 3 points can't curve — returned as-is" );
	}
}
