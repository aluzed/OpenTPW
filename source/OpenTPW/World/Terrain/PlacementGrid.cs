namespace OpenTPW;

/// <summary>
/// A Theme Park World park's tile placement grid. TPW parks are a fixed heightfield of tiles — the
/// jungle level is 95×84 (<c>MapInfo.HeightfieldWidth</c>/<c>HeightfieldHeight</c> in the level's
/// <c>Standard.sam</c>) — and rides, shops and fixed items (gates, ticket booths) are placed at integer
/// tile coordinates, each occupying a rectangular footprint. This maps tile coordinates ↔ world
/// positions and tracks occupancy so placed objects don't overlap.
///
/// <para>This is the placement/coordinate layer; rendering the authentic terrain mesh from the level's
/// heightfield (<c>terrain.wad</c> / <c>2dmap.tga</c>) is a separate, larger effort. The grid sits on
/// the ground plane (world Z = <see cref="Origin"/>.Z), with tiles laid out along world X/Y.</para>
/// </summary>
public sealed class PlacementGrid
{
	public int Width { get; }
	public int Height { get; }
	public float TileSize { get; }

	/// <summary>World position of tile corner (0,0). Z is the ground height the grid sits on.</summary>
	public Vector3 Origin { get; }

	private readonly bool[,] occupied;
	private readonly bool[,] path; // tiles laid as walkable path (occupied for placement, but peeps may cross)
	private readonly bool[,] water; // tiles under water: peeps can't walk them, nothing can be built on them

	public PlacementGrid( int width, int height, float tileSize, Vector3 origin )
	{
		Width = width;
		Height = height;
		TileSize = tileSize;
		Origin = origin;
		occupied = new bool[width, height];
		path = new bool[width, height];
		water = new bool[width, height];
	}

	/// <summary>A grid whose centre maps to <paramref name="worldCenter"/> (handy until real park siting exists).</summary>
	public static PlacementGrid Centered( int width, int height, float tileSize, Vector3 worldCenter ) =>
		new( width, height, tileSize, worldCenter - new Vector3( width * tileSize / 2f, height * tileSize / 2f, 0 ) );

	/// <summary>World position of the centre of the footprint anchored at tile (<paramref name="tx"/>,<paramref name="ty"/>), size fw×fh.</summary>
	public Vector3 TileToWorld( int tx, int ty, int fw = 1, int fh = 1 ) =>
		Origin + new Vector3( (tx + fw / 2f) * TileSize, (ty + fh / 2f) * TileSize, 0 );

	/// <summary>World position of a fractional tile coordinate (e.g. a sub-tile entrance/exit stand point).</summary>
	public Vector3 PointToWorld( float tileX, float tileY ) =>
		Origin + new Vector3( tileX * TileSize, tileY * TileSize, 0 );

	/// <summary>The tile a world position falls in (floored; may be out of bounds).</summary>
	public (int tx, int ty) WorldToTile( Vector3 world )
	{
		var rel = world - Origin;
		return ((int)MathF.Floor( rel.X / TileSize ), (int)MathF.Floor( rel.Y / TileSize ));
	}

	/// <summary>Whether the fw×fh footprint at (tx,ty) lies fully inside the grid.</summary>
	public bool InBounds( int tx, int ty, int fw = 1, int fh = 1 ) =>
		fw > 0 && fh > 0 && tx >= 0 && ty >= 0 && tx + fw <= Width && ty + fh <= Height;

	/// <summary>Whether the fw×fh footprint at (tx,ty) is in bounds and unoccupied.</summary>
	public bool CanPlace( int tx, int ty, int fw, int fh )
	{
		if ( !InBounds( tx, ty, fw, fh ) )
			return false;

		for ( int y = ty; y < ty + fh; y++ )
			for ( int x = tx; x < tx + fw; x++ )
				if ( occupied[x, y] || water[x, y] ) // can't build on the water (T-050)
					return false;
		return true;
	}

	/// <summary>Marks the fw×fh footprint at (tx,ty) occupied if it can be placed; returns success.</summary>
	public bool TryPlace( int tx, int ty, int fw, int fh )
	{
		if ( !CanPlace( tx, ty, fw, fh ) )
			return false;

		for ( int y = ty; y < ty + fh; y++ )
			for ( int x = tx; x < tx + fw; x++ )
				occupied[x, y] = true;
		return true;
	}

	/// <summary>Frees the fw×fh footprint at (tx,ty) (e.g. when a ride is removed).</summary>
	public void Clear( int tx, int ty, int fw, int fh )
	{
		for ( int y = ty; y < ty + fh; y++ )
			for ( int x = tx; x < tx + fw; x++ )
				if ( InBounds( x, y ) )
				{
					occupied[x, y] = false;
					path[x, y] = false;
				}
	}

	// ── Masked placement (T-052) ───────────────────────────────────────────────────────────────────
	// A PlacementFootprint reserves only its *solid* tiles, so a piece with passable cells (e.g. an
	// .hmp-derived queue path or fence) leaves those tiles walkable/buildable. The bounding box must lie
	// in bounds; only solid cells need to be free + dry. A solid-rectangle footprint behaves exactly like
	// the int-overloads above.

	/// <summary>Whether <paramref name="fp"/> anchored at (tx,ty) fits: box in bounds, solid cells free + dry.</summary>
	public bool CanPlace( int tx, int ty, PlacementFootprint fp )
	{
		if ( !InBounds( tx, ty, fp.Cols, fp.Rows ) )
			return false;

		for ( int row = 0; row < fp.Rows; row++ )
			for ( int col = 0; col < fp.Cols; col++ )
				if ( fp.IsSolid( col, row ) && (occupied[tx + col, ty + row] || water[tx + col, ty + row]) )
					return false;
		return true;
	}

	/// <summary>Marks <paramref name="fp"/>'s solid tiles occupied if it can be placed; returns success.</summary>
	public bool TryPlace( int tx, int ty, PlacementFootprint fp )
	{
		if ( !CanPlace( tx, ty, fp ) )
			return false;

		for ( int row = 0; row < fp.Rows; row++ )
			for ( int col = 0; col < fp.Cols; col++ )
				if ( fp.IsSolid( col, row ) )
					occupied[tx + col, ty + row] = true;
		return true;
	}

	/// <summary>Frees the solid tiles of <paramref name="fp"/> anchored at (tx,ty).</summary>
	public void Clear( int tx, int ty, PlacementFootprint fp )
	{
		for ( int row = 0; row < fp.Rows; row++ )
			for ( int col = 0; col < fp.Cols; col++ )
				if ( fp.IsSolid( col, row ) && InBounds( tx + col, ty + row ) )
				{
					occupied[tx + col, ty + row] = false;
					path[tx + col, ty + row] = false;
				}
	}

	/// <summary>Marks a reserved tile as a walkable path (e.g. a queue path): it still blocks placement,
	/// but peeps may route across it (see <see cref="IsWalkable"/> / T-036 pathfinding).</summary>
	public void MarkPath( int tx, int ty )
	{
		if ( InBounds( tx, ty ) )
			path[tx, ty] = true;
	}

	/// <summary>Whether a peep may stand on tile (tx,ty): in bounds, not under water, and either free
	/// ground or a laid path (a ride/shop footprint blocks, a queue path does not). Water is impassable
	/// even where a path would otherwise cross (T-036/T-050).</summary>
	public bool IsWalkable( int tx, int ty ) =>
		InBounds( tx, ty ) && !water[tx, ty] && (!occupied[tx, ty] || path[tx, ty]);

	/// <summary>Marks tile (tx,ty) as under water — impassable to peeps and unbuildable (T-050).</summary>
	public void MarkWater( int tx, int ty )
	{
		if ( InBounds( tx, ty ) )
			water[tx, ty] = true;
	}

	/// <summary>Whether tile (tx,ty) is under water.</summary>
	public bool IsWater( int tx, int ty ) => InBounds( tx, ty ) && water[tx, ty];

	/// <summary>Number of water tiles (diagnostics).</summary>
	public int WaterTileCount
	{
		get
		{
			int n = 0;
			for ( int y = 0; y < Height; y++ )
				for ( int x = 0; x < Width; x++ )
					if ( water[x, y] ) n++;
			return n;
		}
	}

	/// <summary>
	/// Flags tiles whose terrain height (sampled at the tile centre) is at or below <paramref name="waterLevel"/>
	/// as water — so peeps route around lakes/moats and nothing is built on them (T-050). <paramref name="sampleHeight"/>
	/// is the terrain height sampler (world Z) for a world XY (e.g. <c>ParkTerrain.SampleHeight</c>).
	/// </summary>
	public int MarkWaterFromTerrain( Func<float, float, float> sampleHeight, float waterLevel )
	{
		int marked = 0;
		for ( int y = 0; y < Height; y++ )
			for ( int x = 0; x < Width; x++ )
			{
				var c = TileToWorld( x, y );
				if ( sampleHeight( c.X, c.Y ) <= waterLevel )
				{
					water[x, y] = true;
					marked++;
				}
			}
		return marked;
	}

	/// <summary>
	/// Builds a grid from a level's <c>Standard.sam</c> (<c>MapInfo.HeightfieldWidth</c>/<c>Height</c>),
	/// centred on <paramref name="worldCenter"/>. Falls back to the jungle defaults (95×84) if the keys
	/// are missing.
	/// </summary>
	public static PlacementGrid FromLevelSettings( SettingsFile standard, float tileSize, Vector3 worldCenter )
	{
		int w = ReadInt( standard, "MapInfo.HeightfieldWidth", 95 );
		int h = ReadInt( standard, "MapInfo.HeightfieldHeight", 84 );
		return Centered( w, h, tileSize, worldCenter );
	}

	private static int ReadInt( SettingsFile s, string key, int fallback ) =>
		int.TryParse( s[key], out var v ) ? v : fallback;
}
