namespace OpenTPW;

/// <summary>
/// A per-tile placement mask within a <c>Cols×Rows</c> bounding box (T-052): which tiles a placed object
/// actually occupies. A plain rectangle (every tile solid) is the common case — that's
/// <see cref="Rectangle"/>, stored without a backing array so the per-frame placement preview stays
/// allocation-free. <see cref="FromHmp"/> derives a non-rectangular mask from a decoded <see cref="HmpFile"/>
/// footprint grid, so pieces with passable cells (queue paths, fences/hoardings) reserve only their solid
/// tiles and let peeps walk the rest, instead of the old rectangular approximation.
/// </summary>
public sealed class PlacementFootprint
{
	/// <summary>Footprint width in tiles.</summary>
	public int Cols { get; }
	/// <summary>Footprint depth in tiles.</summary>
	public int Rows { get; }

	// null => every tile in the Cols×Rows box is solid (a plain rectangle).
	private readonly bool[]? mask;

	private PlacementFootprint( int cols, int rows, bool[]? mask )
	{
		Cols = cols;
		Rows = rows;
		this.mask = mask;
	}

	/// <summary>A solid <paramref name="w"/>×<paramref name="h"/> rectangle (the default footprint shape).</summary>
	public static PlacementFootprint Rectangle( int w, int h ) => new( w, h, null );

	/// <summary>
	/// Build a footprint from a decoded <see cref="HmpFile"/>: a tile is solid where the file's footprint
	/// byte is non-zero (1 = solid/occupied, 0 = passable). Falls back to a solid rectangle if the file
	/// carries no footprint grid.
	/// </summary>
	public static PlacementFootprint FromHmp( HmpFile hmp )
	{
		int count = hmp.Cols * hmp.Rows;
		if ( hmp.Footprint.Length < count )
			return Rectangle( hmp.Cols, hmp.Rows );

		var m = new bool[count];
		bool anyPassable = false;
		for ( int i = 0; i < count; i++ )
		{
			m[i] = hmp.Footprint[i] != 0;
			if ( !m[i] )
				anyPassable = true;
		}
		// A fully-solid template is just a rectangle — drop the array so it takes the cheap path.
		return anyPassable ? new PlacementFootprint( hmp.Cols, hmp.Rows, m ) : Rectangle( hmp.Cols, hmp.Rows );
	}

	/// <summary>Whether the tile at (col,row) within the box is occupied by this footprint.</summary>
	public bool IsSolid( int col, int row )
	{
		if ( col < 0 || col >= Cols || row < 0 || row >= Rows )
			return false;
		return mask == null || mask[row * Cols + col];
	}

	/// <summary>How many tiles this footprint actually occupies (≤ Cols×Rows).</summary>
	public int SolidCellCount
	{
		get
		{
			if ( mask == null )
				return Cols * Rows;
			int n = 0;
			foreach ( var b in mask )
				if ( b ) n++;
			return n;
		}
	}
}
