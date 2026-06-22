namespace OpenTPW;

/// <summary>
/// A* routing over the park's <see cref="PlacementGrid"/> (T-036). A peep asks for a path between two
/// world points and gets a list of waypoints that steps around obstacles — ride and shop footprints
/// block, free ground and laid queue paths are walkable (<see cref="PlacementGrid.IsWalkable"/>) — so
/// visitors route around rides instead of walking straight through them.
///
/// <para>Walkability is grid-obstacle based: the demo park terrain carries no water mask yet, so
/// avoiding water has to wait until the real level heightfield is loaded (a separate terrain effort);
/// for now "blocked" means a placed ride/shop. The search is 8-connected with no diagonal corner-cutting
/// through a blocked corner, and bounded by <see cref="MaxExpansions"/> (it falls back to a straight line
/// for an unreachable / very distant goal).</para>
/// </summary>
public sealed class PathGraph
{
	private const int MaxExpansions = 6000;
	private const float Sqrt2 = 1.41421356f;

	private static readonly (int dx, int dy)[] Neighbours =
	{
		(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
	};

	private readonly PlacementGrid grid;

	public PathGraph( PlacementGrid grid ) => this.grid = grid;

	/// <summary>The grid tile a world point falls in — used to key a peep's cached path to its goal tile.</summary>
	public (int X, int Y) TileOf( Vector3 world ) => grid.WorldToTile( world );

	/// <summary>
	/// Waypoints from <paramref name="startWorld"/> to <paramref name="goalWorld"/> routed around
	/// obstacles, or null when there's no route (the caller then walks straight). The start tile is
	/// dropped (the peep is already there) and the final waypoint is the exact goal, not its tile centre;
	/// the start and goal tiles are always traversable so a peep can leave/enter a footprint cell (a ride
	/// exit, a shop, an entrance stand point). Z is irrelevant — peeps move in XY and drop to the ground.
	/// </summary>
	public List<Vector3>? FindPath( Vector3 startWorld, Vector3 goalWorld )
	{
		var (sx, sy) = grid.WorldToTile( startWorld );
		var (gx, gy) = grid.WorldToTile( goalWorld );

		var tiles = AStar( sx, sy, gx, gy );
		if ( tiles == null )
			return null;

		var pts = new List<Vector3>( tiles.Count );
		foreach ( var (x, y) in tiles )
			pts.Add( grid.TileToWorld( x, y ) );

		// End on the exact goal (not its tile centre); drop the start tile so we head for the next node.
		if ( pts.Count == 0 )
			pts.Add( goalWorld );
		else
			pts[^1] = goalWorld;
		if ( pts.Count > 1 )
			pts.RemoveAt( 0 );
		return pts;
	}

	// A tile may be stepped on if it's walkable, or it's the route's start/goal (so a peep can step off a
	// ride exit cell or onto a shop / entrance cell, which are themselves blocked footprint tiles).
	private bool Passable( int x, int y, int sx, int sy, int gx, int gy ) =>
		grid.IsWalkable( x, y ) || (x == sx && y == sy) || (x == gx && y == gy);

	private List<(int x, int y)>? AStar( int sx, int sy, int gx, int gy )
	{
		if ( !grid.InBounds( sx, sy ) || !grid.InBounds( gx, gy ) )
			return null;

		var open = new PriorityQueue<(int x, int y), float>();
		var came = new Dictionary<(int, int), (int, int)>();
		var cost = new Dictionary<(int, int), float> { [(sx, sy)] = 0f };
		var closed = new HashSet<(int, int)>();
		open.Enqueue( (sx, sy), Heuristic( sx, sy, gx, gy ) );

		int expanded = 0;
		while ( open.TryDequeue( out var cur, out _ ) )
		{
			if ( cur.x == gx && cur.y == gy )
				return Reconstruct( came, cur );
			if ( !closed.Add( cur ) )
				continue; // stale duplicate (no decrease-key) — already finalised
			if ( ++expanded > MaxExpansions )
				return null;

			float curCost = cost[cur];
			foreach ( var (dx, dy) in Neighbours )
			{
				int nx = cur.x + dx, ny = cur.y + dy;
				if ( !grid.InBounds( nx, ny ) || !Passable( nx, ny, sx, sy, gx, gy ) )
					continue;

				// Don't cut a diagonal across a blocked corner (between two non-passable orthogonal tiles).
				if ( dx != 0 && dy != 0
					&& (!Passable( cur.x + dx, cur.y, sx, sy, gx, gy ) || !Passable( cur.x, cur.y + dy, sx, sy, gx, gy )) )
					continue;

				float next = curCost + (dx != 0 && dy != 0 ? Sqrt2 : 1f);
				if ( cost.TryGetValue( (nx, ny), out var prev ) && next >= prev )
					continue;
				cost[(nx, ny)] = next;
				came[(nx, ny)] = cur;
				open.Enqueue( (nx, ny), next + Heuristic( nx, ny, gx, gy ) );
			}
		}
		return null;
	}

	// Octile distance — admissible for 8-connected movement (straight cost 1, diagonal Sqrt2).
	private static float Heuristic( int x, int y, int gx, int gy )
	{
		int dx = Math.Abs( x - gx ), dy = Math.Abs( y - gy );
		return dx + dy + (Sqrt2 - 2f) * Math.Min( dx, dy );
	}

	private static List<(int x, int y)> Reconstruct( Dictionary<(int, int), (int, int)> came, (int x, int y) cur )
	{
		var path = new List<(int x, int y)> { cur };
		while ( came.TryGetValue( cur, out var prev ) )
		{
			cur = prev;
			path.Add( cur );
		}
		path.Reverse();
		return path;
	}
}
