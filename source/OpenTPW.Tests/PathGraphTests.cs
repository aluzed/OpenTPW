using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class PathGraphTests
{
	private static PlacementGrid Grid() => new( 10, 8, 16f, new Vector3( 0, 0, 0 ) );

	// The waypoints a peep would walk, mapped back to grid tiles (the start tile is dropped by FindPath).
	private static System.Collections.Generic.List<(int X, int Y)> TilePath( PathGraph g, PlacementGrid grid, (int X, int Y) start, (int X, int Y) goal )
	{
		var pts = g.FindPath( grid.TileToWorld( start.X, start.Y ), grid.TileToWorld( goal.X, goal.Y ) );
		Assert.IsNotNull( pts, "expected a route" );
		return pts.Select( p => grid.WorldToTile( p ) ).ToList();
	}

	[TestMethod]
	public void StraightLineOnOpenGround()
	{
		var grid = Grid();
		var g = new PathGraph( grid );
		var tiles = TilePath( g, grid, (0, 0), (5, 0) );
		Assert.AreEqual( (5, 0), tiles[^1], "ends on the goal tile" );
		// An open run has no detour: every step advances toward the goal (monotone in X, never leaves row 0).
		Assert.IsTrue( tiles.All( t => t.Y == 0 ) );
		Assert.AreEqual( 5, tiles.Count, "one waypoint per tile from (1,0)..(5,0)" );
	}

	[TestMethod]
	public void RoutesAroundARideFootprint()
	{
		var grid = Grid();
		// A wall blocking the direct line (column x=3, rows 0..6), leaving only row 7 open to get past.
		Assert.IsTrue( grid.TryPlace( 3, 0, 1, 7 ) );
		var g = new PathGraph( grid );

		var tiles = TilePath( g, grid, (0, 0), (6, 0) );
		Assert.AreEqual( (6, 0), tiles[^1] );
		// It must detour around the wall, never stepping onto a blocked wall tile.
		Assert.IsFalse( tiles.Any( t => t.X == 3 && t.Y <= 6 ), "walked through the ride footprint" );
		Assert.IsTrue( tiles.Any( t => t.Y == 7 ), "took the gap past the wall" );
	}

	[TestMethod]
	public void WalksThroughAMarkedQueuePathGap()
	{
		var grid = Grid();
		grid.TryPlace( 3, 0, 1, 7 );  // same wall...
		grid.MarkPath( 3, 3 );         // ...but tile (3,3) is a laid path, so peeps may cross it
		var g = new PathGraph( grid );

		var tiles = TilePath( g, grid, (0, 0), (6, 0) );
		Assert.AreEqual( (6, 0), tiles[^1] );
		Assert.IsTrue( tiles.Contains( (3, 3) ), "should cut through the walkable gap rather than around" );
		Assert.IsFalse( tiles.Any( t => t.Y == 7 ), "no need to detour to the far gap" );
	}

	[TestMethod]
	public void NoRouteWhenFullyWalledOff()
	{
		var grid = Grid();
		grid.TryPlace( 3, 0, 1, 8 ); // a full-height wall: no way across
		var g = new PathGraph( grid );
		Assert.IsNull( g.FindPath( grid.TileToWorld( 0, 0 ), grid.TileToWorld( 6, 0 ) ),
			"a walled-off goal has no route (caller falls back to a straight line)" );
	}

	[TestMethod]
	public void GoalAndStartCellsAreReachableEvenWhenBlocked()
	{
		var grid = Grid();
		// The goal sits on a ride footprint cell (e.g. an entrance stand point) — still reachable as a goal.
		grid.TryPlace( 5, 0, 2, 2 );
		var g = new PathGraph( grid );
		var pts = g.FindPath( grid.TileToWorld( 0, 0 ), grid.TileToWorld( 5, 0 ) );
		Assert.IsNotNull( pts );
		Assert.AreEqual( (5, 0), grid.WorldToTile( pts[^1] ) );
	}

	[TestMethod]
	public void DoesNotCutBlockedCorners()
	{
		var grid = Grid();
		grid.TryPlace( 1, 0, 1, 1 ); // block (1,0); the (0,0)->(1,1) diagonal would clip its corner
		var g = new PathGraph( grid );
		var tiles = TilePath( g, grid, (0, 0), (1, 1) );
		Assert.IsTrue( tiles.Contains( (0, 1) ), "must go around the corner via (0,1), not cut across it" );
		Assert.IsFalse( tiles.Contains( (1, 0) ) );
	}
}
