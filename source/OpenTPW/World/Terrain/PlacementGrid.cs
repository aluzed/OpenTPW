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

	public PlacementGrid( int width, int height, float tileSize, Vector3 origin )
	{
		Width = width;
		Height = height;
		TileSize = tileSize;
		Origin = origin;
		occupied = new bool[width, height];
	}

	/// <summary>A grid whose centre maps to <paramref name="worldCenter"/> (handy until real park siting exists).</summary>
	public static PlacementGrid Centered( int width, int height, float tileSize, Vector3 worldCenter ) =>
		new( width, height, tileSize, worldCenter - new Vector3( width * tileSize / 2f, height * tileSize / 2f, 0 ) );

	/// <summary>World position of the centre of the footprint anchored at tile (<paramref name="tx"/>,<paramref name="ty"/>), size fw×fh.</summary>
	public Vector3 TileToWorld( int tx, int ty, int fw = 1, int fh = 1 ) =>
		Origin + new Vector3( (tx + fw / 2f) * TileSize, (ty + fh / 2f) * TileSize, 0 );

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
				if ( occupied[x, y] )
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
					occupied[x, y] = false;
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
