namespace OpenTPW;

/// <summary>
/// Helpers for building a ride car's loop path from <b>authored footprint data</b> (T-048). The ride's
/// real car track is <i>simulation output</i> — the original animates a bone rig and reads the car/seat
/// node positions off it each frame; that rig isn't decoded (and there is no static path in the asset
/// files — only the player-laid coaster carries one). So the faithful-but-bounded improvement is to make
/// the loop follow the ride's <b>actual footprint shape</b> (the <c>.sam</c> <c>Info.Shape</c> grid) and
/// pass its <b>entrance</b>, instead of a generic centred ellipse: it adapts to non-rectangular rides and
/// routes the car past the boarding tile. Still procedural — a stand-in for the authored track shape until
/// the motion sim / skeleton is decoded — but grounded in real authored data. Pure + unit-tested.
/// </summary>
public static class RidePath
{
	private static readonly (int X, int Y)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };

	/// <summary>
	/// The footprint's perimeter tiles as a closed ring, ordered angularly around the footprint centroid and
	/// rotated to start at the tile nearest <paramref name="entrance"/> (so the loop passes the boarding
	/// point). A perimeter tile is an occupied cell touching the grid edge or an empty cell. Returns an empty
	/// list for a degenerate footprint (either dimension &lt; 2, or &lt; 3 perimeter tiles) — the caller then
	/// falls back to the procedural ellipse.
	/// </summary>
	public static List<(int X, int Y)> FootprintRing( bool[,] cells, (int X, int Y)? entrance )
	{
		int w = cells.GetLength( 0 ), h = cells.GetLength( 1 );
		if ( w < 2 || h < 2 )
			return new List<(int, int)>();

		var occupied = new List<(int X, int Y)>();
		for ( int y = 0; y < h; y++ )
			for ( int x = 0; x < w; x++ )
				if ( cells[x, y] )
					occupied.Add( (x, y) );
		if ( occupied.Count < 3 )
			return new List<(int, int)>();

		float cx = 0f, cy = 0f;
		foreach ( var (x, y) in occupied ) { cx += x; cy += y; }
		cx /= occupied.Count; cy /= occupied.Count;

		bool IsPerimeter( int x, int y )
		{
			foreach ( var (dx, dy) in Neighbours )
			{
				int nx = x + dx, ny = y + dy;
				if ( nx < 0 || ny < 0 || nx >= w || ny >= h || !cells[nx, ny] )
					return true;
			}
			return false;
		}

		var perim = occupied.Where( c => IsPerimeter( c.X, c.Y ) ).ToList();
		if ( perim.Count < 3 )
			return new List<(int, int)>();

		// Order the perimeter into a ring by angle around the centroid (a clean loop for convex-ish
		// footprints; a concave shape may cut a corner — acceptable for a procedural stand-in).
		perim.Sort( ( a, b ) =>
			MathF.Atan2( a.Y - cy, a.X - cx ).CompareTo( MathF.Atan2( b.Y - cy, b.X - cx ) ) );

		// Rotate so the ring starts at the perimeter tile nearest the entrance.
		if ( entrance is { } e )
		{
			int best = 0;
			float bestD = float.MaxValue;
			for ( int i = 0; i < perim.Count; i++ )
			{
				float dx = perim[i].X - e.X, dy = perim[i].Y - e.Y;
				float d = dx * dx + dy * dy;
				if ( d < bestD ) { bestD = d; best = i; }
			}
			if ( best > 0 )
			{
				var rotated = new List<(int X, int Y)>( perim.Count );
				for ( int i = 0; i < perim.Count; i++ )
					rotated.Add( perim[(best + i) % perim.Count] );
				perim = rotated;
			}
		}

		return perim;
	}

	/// <summary>
	/// A Catmull-Rom smoothing of control points into a denser polyline (the same curve the coaster uses).
	/// Each segment is subdivided into <paramref name="sub"/> points; a closed path wraps its neighbours and
	/// is closed back to the first point so the result is a ready-to-loop ring.
	/// </summary>
	public static List<Vector3> Smooth( IReadOnlyList<Vector3> p, bool closed, int sub )
	{
		int n = p.Count;
		if ( n < 3 )
			return new List<Vector3>( p );

		var outp = new List<Vector3>( (closed ? n : n - 1) * sub + 1 );
		int segs = closed ? n : n - 1;
		for ( int i = 0; i < segs; i++ )
		{
			var p0 = p[closed ? (i - 1 + n) % n : Math.Max( i - 1, 0 )];
			var p1 = p[i % n];
			var p2 = p[(i + 1) % n];
			var p3 = p[closed ? (i + 2) % n : Math.Min( i + 2, n - 1 )];
			for ( int s = 0; s < sub; s++ )
				outp.Add( CatmullRom( p0, p1, p2, p3, (float)s / sub ) );
		}
		// Close (loop) or finish (open) by re-adding the first/last control point.
		outp.Add( closed ? outp[0] : p[n - 1] );
		return outp;
	}

	private static Vector3 CatmullRom( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t )
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (p1 * 2f
			+ (p2 - p0) * t
			+ (p0 * 2f - p1 * 5f + p2 * 4f - p3) * t2
			+ (p1 * 3f - p0 - p2 * 3f + p3) * t3);
	}
}
