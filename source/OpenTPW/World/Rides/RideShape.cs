namespace OpenTPW;

/// <summary>
/// A ride's tile footprint, parsed from the <c>Info.Shape</c> block of its <c>.sam</c> — an ASCII grid
/// between <c>---</c> delimiters where each character is a tile of the ride's footprint
/// (<c>*</c> = plain occupied tile; letters/digits like <c>S</c>/<c>2</c>/<c>N</c> mark special cells
/// such as the entrance/exit; space = not part of the footprint). Width × Height is the bounding
/// footprint the placement grid reserves. Example (monkey, 4×4):
/// <code>
/// **S*
/// ****
/// ****
/// *2**
/// </code>
/// </summary>
public sealed class RideShape
{
	public int Width { get; }
	public int Height { get; }

	/// <summary><c>[x,y]</c> — whether that tile is part of the footprint (for non-rectangular rides).</summary>
	public bool[,] Cells { get; }

	/// <summary>The entrance tile (where the queue connects / peeps enter) — the <c>S</c>, <c>N</c> or <c>E</c> cell, if any.</summary>
	public (int X, int Y)? Entrance { get; }

	/// <summary>The exit tile (where peeps leave) — the <c>2</c> cell, if any.</summary>
	public (int X, int Y)? Exit { get; }

	/// <summary>Coaster track connectors — the <c>&lt;</c> (in) and <c>&gt;</c> (out) cells, where a
	/// player-built track attaches to the station (T-045). Null for non-coaster rides.</summary>
	public (int X, int Y)? TrackIn { get; }
	public (int X, int Y)? TrackOut { get; }

	/// <summary>True if this ride is a coaster (its shape has track connectors).</summary>
	public bool HasTrack => TrackOut != null || TrackIn != null;

	private RideShape( int width, int height, bool[,] cells, (int, int)? entrance, (int, int)? exit,
		(int, int)? trackIn = null, (int, int)? trackOut = null )
	{
		Width = width;
		Height = height;
		Cells = cells;
		Entrance = entrance;
		Exit = exit;
		TrackIn = trackIn;
		TrackOut = trackOut;
	}

	/// <summary>
	/// This footprint rotated by <paramref name="turns"/> 90° clockwise steps (0–3). Odd turns swap
	/// width/height; cells, entrance, exit and track connectors all move with it. Used by the build tool
	/// to place a ride at an orientation (T-041). A point (x,y) maps 90° CW to (Height-1-y, x).
	/// </summary>
	public RideShape Rotated( int turns )
	{
		turns = ((turns % 4) + 4) % 4;
		if ( turns == 0 )
			return this;

		int nw = turns % 2 == 0 ? Width : Height;
		int nh = turns % 2 == 0 ? Height : Width;

		(int X, int Y) Rot( int x, int y ) => turns switch
		{
			1 => (Height - 1 - y, x),
			2 => (Width - 1 - x, Height - 1 - y),
			3 => (y, Width - 1 - x),
			_ => (x, y),
		};
		(int, int)? RotN( (int X, int Y)? p ) => p is { } q ? Rot( q.X, q.Y ) : null;

		var cells = new bool[nw, nh];
		for ( int y = 0; y < Height; y++ )
			for ( int x = 0; x < Width; x++ )
				if ( Cells[x, y] )
				{
					var (nx, ny) = Rot( x, y );
					cells[nx, ny] = true;
				}

		return new RideShape( nw, nh, cells, RotN( Entrance ), RotN( Exit ), RotN( TrackIn ), RotN( TrackOut ) );
	}

	/// <summary>The default 4×4 footprint, used when a ride ships no parseable shape.</summary>
	public static RideShape Default { get; } = Rect( 4, 4 );

	private static RideShape Rect( int w, int h )
	{
		var cells = new bool[w, h];
		for ( int y = 0; y < h; y++ )
			for ( int x = 0; x < w; x++ )
				cells[x, y] = true;
		return new RideShape( w, h, cells, null, null );
	}

	/// <summary>Parses the <c>Info.Shape</c> grid out of a ride's <c>.sam</c> text (falls back to 4×4).</summary>
	public static RideShape Parse( string samText )
	{
		if ( string.IsNullOrEmpty( samText ) )
			return Default;

		var lines = samText.Replace( "\r", "" ).Split( '\n' );

		int i = 0;
		for ( ; i < lines.Length; i++ )
			if ( lines[i].TrimStart().StartsWith( "Info.Shape" ) )
				break;
		if ( i >= lines.Length )
			return Default;

		// Advance to the opening '---', then collect rows until the closing '---'.
		for ( i++; i < lines.Length && lines[i].Trim() != "---"; i++ ) { }
		i++;

		var rows = new List<string>();
		for ( ; i < lines.Length && lines[i].Trim() != "---"; i++ )
			rows.Add( lines[i].TrimEnd() );

		while ( rows.Count > 0 && rows[^1].Length == 0 )
			rows.RemoveAt( rows.Count - 1 );
		if ( rows.Count == 0 )
			return Default;

		int width = rows.Max( r => r.Length );
		int height = rows.Count;
		if ( width <= 0 || width > 64 || height > 64 )
			return Default;

		var cells = new bool[width, height];
		(int, int)? entrance = null, exit = null, trackIn = null, trackOut = null;
		for ( int y = 0; y < height; y++ )
			for ( int x = 0; x < rows[y].Length; x++ )
			{
				char ch = rows[y][x];
				// '.' and space are gaps, not footprint tiles; everything else (incl. S/N/2/E/<>) is a tile.
				cells[x, y] = !char.IsWhiteSpace( ch ) && ch != '.';

				// Entrance markers: S (station), N (eNtrance), E. Exit marker: 2. Track connectors: < >.
				if ( entrance == null && (ch is 'S' or 'N' or 'E') )
					entrance = (x, y);
				else if ( exit == null && ch == '2' )
					exit = (x, y);
				if ( ch == '<' ) trackIn = (x, y);
				else if ( ch == '>' ) trackOut = (x, y);
			}

		return new RideShape( width, height, cells, entrance, exit, trackIn, trackOut );
	}

	/// <summary>Loads and parses a ride's footprint from its WAD <c>.sam</c> (via the VFS).</summary>
	public static RideShape Load( string rideArchive, string rideName )
	{
		try
		{
			using var s = FileSystem.OpenRead( $"{rideArchive}/{rideName}.sam" );
			if ( s == null )
				return Default;
			using var reader = new StreamReader( s );
			return Parse( reader.ReadToEnd() );
		}
		catch
		{
			return Default;
		}
	}
}
