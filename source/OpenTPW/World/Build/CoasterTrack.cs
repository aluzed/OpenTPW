namespace OpenTPW;

/// <summary>
/// A player-built coaster track (T-045): a chain of grid tiles laid from the station's <c>&gt;</c>
/// connector, which can be closed into a circuit at the <c>&lt;</c> entry. The ridden centre-line is a
/// Catmull-Rom spline through the tile centres (so the train glides), and the track is **rendered** as a
/// smooth elevated ribbon (the real <c>Trak_sec</c> texture) following that same spline, on support
/// pylons. 3D curved piece *meshes* from the <c>.hmp</c> templates + peep boarding are slice 3b.
/// </summary>
public sealed class CoasterTrack
{
	private const float TrackHeight = 10f; // track elevation above the ground

	private readonly PlacementGrid grid;
	private readonly ParkTerrain terrain;
	private readonly Ride coaster;
	private readonly Texture trackTex;
	private readonly Material<ObjectUniformBuffer> ribbonMat;
	private readonly List<(int X, int Y)> tiles = new();
	private readonly List<float> rise = new();          // per-tile height offset above the base track height (STACKUP/DOWN)
	private readonly List<ModelEntity> pylons = new(); // one support per laid segment
	private readonly CoasterTrain train;
	private readonly (int X, int Y)? trackInTile; // the station's '<' entry connector, where the loop closes
	private ModelEntity? ribbon;                  // the smooth track surface (regenerated on every change)

	public (int X, int Y) Head => tiles[^1];
	public int SegmentCount => tiles.Count - 1; // the first tile is the station connector anchor

	/// <summary>True once the track has been laid back to the station's entry connector — a full circuit
	/// (the train then runs a continuous loop instead of shuttling).</summary>
	public bool IsClosed { get; private set; }

	/// <summary>Live rider count of the parent coaster — the train carries this many and runs while &gt; 0.</summary>
	public int Riders => coaster.Riders;

	/// <summary>The coaster this track belongs to (the train uses it to start/stop the rider scream).</summary>
	public Ride Coaster => coaster;

	public CoasterTrack( Ride coaster, PlacementGrid grid, ParkTerrain terrain )
	{
		this.grid = grid;
		this.terrain = terrain;
		this.coaster = coaster;

		try { trackTex = new Texture( $"{coaster.Archive}/gtexture/Trak_sec2.wct", TextureFlags.Repeat ); }
		catch { trackTex = Texture.Missing; }
		// Unlit (not 3d.shader): unlit samples a single Color texture at the vertex UVs, whereas 3d.shader
		// expects a per-vertex-indexed texture *array* this hand-built ribbon mesh doesn't supply.
		ribbonMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
		ribbonMat.Set( "Color", trackTex );

		// Anchor at the station's track-out connector (fall back to track-in / centre).
		var c = coaster.Shape.TrackOut ?? coaster.Shape.TrackIn ?? (coaster.TileW / 2, coaster.TileH / 2);
		tiles.Add( (coaster.TileX + c.X, coaster.TileY + c.Y) );
		rise.Add( 0f ); // the station connector stays at the base track height

		// The entry connector (if any, and distinct from the anchor) is where laying back closes the loop.
		if ( coaster.Shape.TrackIn is { } ti )
		{
			var inTile = (coaster.TileX + ti.X, coaster.TileY + ti.Y);
			if ( inTile != tiles[0] )
				trackInTile = inTile;
		}

		// The train hides itself until at least one segment is laid (path has < 2 points).
		train = new CoasterTrain( this, grid.TileSize, coaster.Archive );
		coaster.Train = train; // peeps boarding this coaster ride the train in view (T-045 3b)
		coaster.Track = this;  // so selling the coaster tears the track down too (T-041)
	}

	/// <summary>The track centre-line as elevated world points (one per laid tile) — the raw control path.</summary>
	public List<Vector3> WorldPath()
	{
		var pts = new List<Vector3>( tiles.Count );
		for ( int i = 0; i < tiles.Count; i++ )
		{
			var (tx, ty) = tiles[i];
			var c = grid.TileToWorld( tx, ty );
			pts.Add( c.WithZ( terrain.SampleHeight( c.X, c.Y ) + TrackHeight + rise[i] ) );
		}
		return pts;
	}

	/// <summary>The ridden centre-line: a Catmull-Rom spline through the control points (closed into a
	/// ring when the circuit is complete). Both the train and the rendered ribbon follow this.</summary>
	public List<Vector3> SmoothedPath()
	{
		var ctrl = WorldPath();
		if ( ctrl.Count < 2 )
			return ctrl;
		var path = Smooth( ctrl, IsClosed, 8 );
		if ( IsClosed )
			path.Add( path[0] ); // close the ring so the last edge wraps back to the start
		return path;
	}

	/// <summary>Can the track be extended onto tile (tx,ty)? (on-grid, 4-adjacent to the head, no overlap,
	/// and not already a closed circuit). Laying onto the station's entry connector is allowed — it closes
	/// the loop.</summary>
	public bool CanExtend( int tx, int ty )
	{
		if ( IsClosed || !grid.InBounds( tx, ty ) || tiles.Contains( (tx, ty) ) )
			return false;
		var (hx, hy) = Head;
		return Math.Abs( tx - hx ) + Math.Abs( ty - hy ) == 1;
	}

	public bool Extend( int tx, int ty )
	{
		if ( !CanExtend( tx, ty ) )
			return false;
		tiles.Add( (tx, ty) );
		rise.Add( rise[^1] ); // a new segment continues at the current head's height (build a slope by raising first)
		if ( (tx, ty) == trackInTile )
		{
			IsClosed = true;  // laid back to the station entry — the circuit is complete
			rise[^1] = 0f;     // the entry connector meets the station at the base height
		}
		SpawnPylon( tx, ty, rise[^1] );
		RebuildRibbon();
		return true;
	}

	/// <summary>Raise (<paramref name="dir"/> &gt; 0) or lower the head segment a step (T-045 3b
	/// STACKUP/STACKDOWN), so the player can build hills. Clamped so the track stays above ground; a closed
	/// circuit's closing tile is fixed at the station height and can't be moved.</summary>
	public bool StackHead( int dir )
	{
		if ( tiles.Count <= 1 || IsClosed )
			return false;
		float step = TrackHeight * 0.5f;
		float next = Math.Clamp( rise[^1] + dir * step, -TrackHeight * 0.6f, TrackHeight * 4f );
		if ( MathF.Abs( next - rise[^1] ) < 1e-3f )
			return false;
		rise[^1] = next;
		if ( pylons.Count > 0 )
			PositionPylon( pylons[^1], Head.X, Head.Y, rise[^1] );
		RebuildRibbon();
		return true;
	}

	/// <summary>Remove the last laid segment (keeps the station anchor).</summary>
	public void Backtrack()
	{
		if ( tiles.Count <= 1 )
			return;
		IsClosed = false; // removing the head reopens a closed circuit (the head was the closing tile)
		tiles.RemoveAt( tiles.Count - 1 );
		rise.RemoveAt( rise.Count - 1 );
		if ( pylons.Count > 0 )
		{
			Entity.All.Remove( pylons[^1] );
			pylons.RemoveAt( pylons.Count - 1 );
		}
		RebuildRibbon();
	}

	/// <summary>Remove all laid segments, the ribbon and the train (called when the track is abandoned).</summary>
	public void Despawn()
	{
		foreach ( var p in pylons )
			Entity.All.Remove( p );
		pylons.Clear();
		if ( ribbon != null )
			Entity.All.Remove( ribbon );
		ribbon = null;
		train.Despawn();
		if ( coaster.Train == train )
			coaster.Train = null;
	}

	// A slim grey pillar from the ground up to the (possibly raised) track, under the laid tile's centre.
	private void SpawnPylon( int tx, int ty, float riseVal )
	{
		var pylonMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		pylonMat.Set( "Color", new Texture( [90, 90, 105, 255], 1, 1 ) );
		var e = new ModelEntity { Model = Primitives.Cube.GenerateModel( pylonMat ) };
		PositionPylon( e, tx, ty, riseVal );
		pylons.Add( e );
	}

	// Size + place a pylon so it spans the ground up to the track top at this tile's height.
	private void PositionPylon( ModelEntity e, int tx, int ty, float riseVal )
	{
		var c = grid.TileToWorld( tx, ty );
		float gz = terrain.SampleHeight( c.X, c.Y );
		float h = MathF.Max( 2f, TrackHeight + riseVal );
		e.Position = c.WithZ( gz + h / 2f );
		e.Scale = new Vector3( 1.5f, 1.5f, h / 2f );
	}

	// Authentic-ish track profile (T-045 slice 3b): a continuous track bed carrying two raised running rails
	// along the ridden centre-line with periodic cross-ties (sleepers), replacing the old flat textured
	// strip — so a built track reads as a real coaster track. The train still runs the centre-line.
	private const float RailGauge = 0.16f;   // ×TileSize: half-distance between the two rails
	private const float RailHalfWidth = 0.03f; // ×TileSize: half-width of a rail strip
	private const float RailRise = 0.05f;      // ×TileSize: rails sit a little above the tie deck
	private const float TieHalfLength = 0.22f; // ×TileSize: cross-tie reach along travel
	private const int TieEvery = 4;            // a cross-tie every N spline points (~2 ties / tile at sub=8)

	private void RebuildRibbon()
	{
		var pts = SmoothedPath();
		if ( pts.Count < 2 )
		{
			if ( ribbon != null ) ribbon.Model = null;
			return;
		}

		float ts = grid.TileSize;
		float gauge = ts * RailGauge, railHalf = ts * RailHalfWidth, rise = ts * RailRise, tieLen = ts * TieHalfLength;

		// A horizontal frame (position + perpendicular-to-travel) at each centre-line point.
		var pos = new Vector3[pts.Count];
		var perp = new Vector3[pts.Count];
		var tan = new Vector3[pts.Count];
		for ( int i = 0; i < pts.Count; i++ )
		{
			var prev = pts[Math.Max( i - 1, 0 )];
			var next = pts[Math.Min( i + 1, pts.Count - 1 )];
			var dir = (next - prev).Normal;
			pos[i] = pts[i];
			perp[i] = new Vector3( -dir.Y, dir.X, 0f ).Normal;
			tan[i] = dir;
		}

		var verts = new List<Vertex>( pts.Count * 6 + pts.Count / TieEvery * 4 );
		var idx = new List<uint>();

		// A strip centred `offset` from the centre-line, `halfWidth` wide, raised `lift` — used for the
		// continuous track bed (offset 0) and for the two running rails (offset ±gauge, lifted onto the bed).
		void AddStrip( float offset, float halfWidth, float lift )
		{
			uint baseIdx = (uint)verts.Count;
			float cum = 0f;
			for ( int i = 0; i < pts.Count; i++ )
			{
				if ( i > 0 ) cum += pts[i].Distance( pts[i - 1] );
				float v = cum / ts;
				var c = pos[i] + perp[i] * offset + Vector3.Up * lift;
				verts.Add( new Vertex { Position = c + perp[i] * halfWidth, Normal = Vector3.Up, TexCoords = new Vector2( 0f, v ) } );
				verts.Add( new Vertex { Position = c - perp[i] * halfWidth, Normal = Vector3.Up, TexCoords = new Vector2( 1f, v ) } );
			}
			for ( int i = 0; i < pts.Count - 1; i++ )
			{
				uint a = baseIdx + (uint)(2 * i), b = a + 1, c = a + 2, d = a + 3;
				idx.Add( a ); idx.Add( b ); idx.Add( c );
				idx.Add( b ); idx.Add( d ); idx.Add( c );
			}
		}
		float tieReach = gauge + railHalf * 2f;
		AddStrip( 0f, tieReach, 0f );      // continuous track bed (reads as solid from a distance)
		AddStrip( -gauge, railHalf, rise ); // left running rail, lifted onto the bed
		AddStrip( +gauge, railHalf, rise ); // right running rail

		// Cross-ties: a short quad spanning the bed every few points (the "sleepers"), just above the bed.
		float tieLift = rise * 0.5f;
		for ( int i = 0; i < pts.Count; i += TieEvery )
		{
			var step = tan[i] * tieLen;
			var l = pos[i] + perp[i] * (-tieReach) + Vector3.Up * tieLift;
			var r = pos[i] + perp[i] * tieReach + Vector3.Up * tieLift;
			uint bi = (uint)verts.Count;
			verts.Add( new Vertex { Position = l - step, Normal = Vector3.Up, TexCoords = new Vector2( 0f, 0f ) } );
			verts.Add( new Vertex { Position = r - step, Normal = Vector3.Up, TexCoords = new Vector2( 1f, 0f ) } );
			verts.Add( new Vertex { Position = l + step, Normal = Vector3.Up, TexCoords = new Vector2( 0f, 1f ) } );
			verts.Add( new Vertex { Position = r + step, Normal = Vector3.Up, TexCoords = new Vector2( 1f, 1f ) } );
			idx.Add( bi ); idx.Add( bi + 1 ); idx.Add( bi + 2 );
			idx.Add( bi + 1 ); idx.Add( bi + 3 ); idx.Add( bi + 2 );
		}

		var model = new Model( verts.ToArray(), idx.ToArray(), ribbonMat );
		if ( ribbon == null )
			ribbon = new ModelEntity { Model = model };
		else
			ribbon.Model = model;
	}

	// Catmull-Rom through the control points → a dense, curved centre-line. For a closed ring the
	// neighbours wrap; for an open track the endpoints are clamped. Each control segment is subdivided
	// into <sub> points (start-inclusive, end-exclusive); the open case appends the final endpoint.
	private static List<Vector3> Smooth( IReadOnlyList<Vector3> p, bool closed, int sub )
	{
		int n = p.Count;
		if ( n < 3 )
			return new List<Vector3>( p ); // too few points to curve — keep it straight

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
		if ( !closed )
			outp.Add( p[n - 1] );
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
