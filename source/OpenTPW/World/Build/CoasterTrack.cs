namespace OpenTPW;

/// <summary>
/// A player-built coaster track (T-045 slice 2): a chain of grid tiles laid from the station's
/// <c>&gt;</c> connector, each rendered as an **elevated** track segment (the real <c>Trak_sec</c>
/// texture) on a support pylon — so it reads as a coaster rather than a ground path. Flat segments for
/// now; 3D curved pieces (from the <c>.hmp</c> templates) + a running car are slice 3.
/// </summary>
public sealed class CoasterTrack
{
	private const float TrackHeight = 10f; // track elevation above the ground

	private readonly PlacementGrid grid;
	private readonly ParkTerrain terrain;
	private readonly Texture trackTex;
	private readonly List<(int X, int Y)> tiles = new();
	private readonly List<ModelEntity> parts = new(); // 2 per laid segment (track quad + pylon)
	private readonly CoasterTrain train;

	public (int X, int Y) Head => tiles[^1];
	public int SegmentCount => tiles.Count - 1; // the first tile is the station connector anchor

	public CoasterTrack( Ride coaster, PlacementGrid grid, ParkTerrain terrain )
	{
		this.grid = grid;
		this.terrain = terrain;

		try { trackTex = new Texture( $"{coaster.Archive}/gtexture/Trak_sec2.wct", TextureFlags.Repeat ); }
		catch { trackTex = Texture.Missing; }

		// Anchor at the station's track-out connector (fall back to track-in / centre).
		var c = coaster.Shape.TrackOut ?? coaster.Shape.TrackIn ?? (coaster.TileW / 2, coaster.TileH / 2);
		tiles.Add( (coaster.TileX + c.X, coaster.TileY + c.Y) );

		// The shuttle train hides itself until at least one segment is laid (path has < 2 points).
		train = new CoasterTrain( this, grid.TileSize );
	}

	/// <summary>The track centre-line as elevated world points (one per laid tile) — the train's path.</summary>
	public List<Vector3> WorldPath()
	{
		var pts = new List<Vector3>( tiles.Count );
		foreach ( var (tx, ty) in tiles )
		{
			var c = grid.TileToWorld( tx, ty );
			pts.Add( c.WithZ( terrain.SampleHeight( c.X, c.Y ) + TrackHeight ) );
		}
		return pts;
	}

	/// <summary>Remove all laid segments and the train (called when the track tool is abandoned).</summary>
	public void Despawn()
	{
		foreach ( var p in parts )
			Entity.All.Remove( p );
		parts.Clear();
		train.Despawn();
	}

	/// <summary>Can the track be extended onto tile (tx,ty)? (on-grid, 4-adjacent to the head, no overlap).</summary>
	public bool CanExtend( int tx, int ty )
	{
		if ( !grid.InBounds( tx, ty ) || tiles.Contains( (tx, ty) ) )
			return false;
		var (hx, hy) = Head;
		return Math.Abs( tx - hx ) + Math.Abs( ty - hy ) == 1;
	}

	public bool Extend( int tx, int ty )
	{
		if ( !CanExtend( tx, ty ) )
			return false;
		tiles.Add( (tx, ty) );
		SpawnSegment( tx, ty );
		return true;
	}

	/// <summary>Remove the last laid segment (keeps the station anchor).</summary>
	public void Backtrack()
	{
		if ( tiles.Count <= 1 )
			return;
		tiles.RemoveAt( tiles.Count - 1 );
		for ( int k = 0; k < 2 && parts.Count > 0; k++ )
		{
			Entity.All.Remove( parts[^1] );
			parts.RemoveAt( parts.Count - 1 );
		}
	}

	private void SpawnSegment( int tx, int ty )
	{
		var c = grid.TileToWorld( tx, ty );
		float gz = terrain.SampleHeight( c.X, c.Y );

		// Elevated track quad (real track texture).
		var trackMat = new Material<ObjectUniformBuffer>( "content/shaders/3d.shader" );
		trackMat.Set( "Color", trackTex );
		parts.Add( new ModelEntity
		{
			Model = Primitives.Plane.GenerateModel( trackMat ),
			Position = c.WithZ( gz + TrackHeight ),
			Scale = new Vector3( grid.TileSize / 2f ),
		} );

		// Support pylon (a slim grey pillar from the ground up to the track).
		var pylonMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		pylonMat.Set( "Color", new Texture( [90, 90, 105, 255], 1, 1 ) );
		parts.Add( new ModelEntity
		{
			Model = Primitives.Cube.GenerateModel( pylonMat ),
			Position = c.WithZ( gz + TrackHeight / 2f ),
			Scale = new Vector3( 1.5f, 1.5f, TrackHeight / 2f ),
		} );
	}
}
