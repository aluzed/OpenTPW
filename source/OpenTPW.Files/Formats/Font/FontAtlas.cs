namespace OpenTPW;

/// <summary>Horizontal anchoring of a text line relative to the layout origin.</summary>
public enum TextAlign
{
	/// <summary>The line starts at the origin x.</summary>
	Left,
	/// <summary>The line is centred on the origin x.</summary>
	Center,
	/// <summary>The line ends at the origin x.</summary>
	Right,
}

/// <summary>
/// Packs a decoded <see cref="BF4File"/> into a single RGBA atlas (white pixels, alpha = the
/// glyph mask) plus a per-character glyph table, and lays text out using the glyph metrics.
///
/// This is the CPU-side bridge between the font format and the renderer: it is engine-agnostic
/// (no GPU types) so it can be unit-tested, then uploaded as a texture and drawn quad-per-glyph.
/// </summary>
public sealed class FontAtlas
{
	/// <summary>A glyph packed into the atlas, with its atlas rect, normalized UVs and metrics.</summary>
	public readonly record struct Glyph(
		int CharCode,
		int X, int Y, int Width, int Height,
		float U0, float V0, float U1, float V1,
		int XBearing, int YBearing, int Advance );

	/// <summary>A glyph placed by <see cref="Layout"/>: a destination rect (pixels) + its atlas UVs.</summary>
	public readonly record struct PlacedGlyph(
		float X, float Y, int Width, int Height,
		float U0, float V0, float U1, float V1 );

	/// <summary>Atlas width in pixels.</summary>
	public int Width { get; }

	/// <summary>Atlas height in pixels.</summary>
	public int Height { get; }

	/// <summary>RGBA8 atlas pixels (row-major): white with alpha = the glyph coverage mask.</summary>
	public byte[] Pixels { get; }

	/// <summary>Vertical advance between text lines (the tallest glyph height); used for newlines.</summary>
	public int LineHeight { get; private set; }

	/// <summary>Packed glyphs keyed by character code (first occurrence wins for duplicates).</summary>
	public IReadOnlyDictionary<int, Glyph> Glyphs => glyphs;

	private readonly Dictionary<int, Glyph> glyphs = new();

	public FontAtlas( BF4File font, int atlasWidth = 256, int padding = 1 )
	{
		if ( atlasWidth <= 0 )
			throw new ArgumentOutOfRangeException( nameof( atlasWidth ) );

		// First pass: shelf-pack the (deduplicated) glyphs to compute positions and the height.
		var placements = new List<(BF4File.Glyph src, int x, int y)>();
		var seen = new HashSet<int>();
		int penX = 0, penY = 0, shelfHeight = 0;

		foreach ( var g in font.Glyphs )
		{
			if ( !seen.Add( g.CharCode ) )
				continue; // duplicate char code (real fonts ship several for e.g. space)

			if ( g.Width <= 0 || g.Height <= 0 )
			{
				// No bitmap (e.g. space): record metrics only, no atlas footprint.
				glyphs[g.CharCode] = new Glyph( g.CharCode, 0, 0, 0, 0, 0, 0, 0, 0,
					g.XBearing, g.YBearing, g.Advance );
				continue;
			}

			if ( penX + g.Width > atlasWidth && penX > 0 )
			{
				// Next shelf.
				penY += shelfHeight + padding;
				penX = 0;
				shelfHeight = 0;
			}

			placements.Add( (g, penX, penY) );
			penX += g.Width + padding;
			shelfHeight = Math.Max( shelfHeight, g.Height );
		}

		Width = atlasWidth;
		Height = Math.Max( 1, penY + shelfHeight );
		Pixels = new byte[Width * Height * 4];

		// Second pass: blit each glyph's mask and record its UVs.
		foreach ( var (src, x, y) in placements )
		{
			for ( var row = 0; row < src.Height; row++ )
			{
				for ( var col = 0; col < src.Width; col++ )
				{
					if ( !src.Pixels[row * src.Width + col] )
						continue;

					var p = ((y + row) * Width + (x + col)) * 4;
					Pixels[p + 0] = 255;
					Pixels[p + 1] = 255;
					Pixels[p + 2] = 255;
					Pixels[p + 3] = 255;
				}
			}

			glyphs[src.CharCode] = new Glyph(
				src.CharCode, x, y, src.Width, src.Height,
				(float)x / Width, (float)y / Height,
				(float)(x + src.Width) / Width, (float)(y + src.Height) / Height,
				src.XBearing, src.YBearing, src.Advance );
		}

		LineHeight = glyphs.Values.Count == 0 ? 0 : glyphs.Values.Max( g => g.Height );
	}

	/// <summary>Total advance width of <paramref name="text"/> in pixels (characters with no glyph are skipped).</summary>
	public int Measure( string text )
	{
		var width = 0;
		foreach ( var c in text )
		{
			if ( glyphs.TryGetValue( c, out var g ) )
				width += g.Advance;
		}

		return width;
	}

	/// <summary>
	/// Lays <paramref name="text"/> out from a pen origin, returning one <see cref="PlacedGlyph"/>
	/// per drawable character (its destination rect uses the bearings; the pen advances by Advance).
	/// Honors <c>\n</c> line breaks (each line drops by <see cref="LineHeight"/>) and
	/// <paramref name="align"/> anchors each line horizontally at <paramref name="originX"/>.
	/// </summary>
	public IReadOnlyList<PlacedGlyph> Layout( string text, float originX = 0, float originY = 0,
		TextAlign align = TextAlign.Left )
	{
		var placed = new List<PlacedGlyph>();
		var lineY = originY;

		foreach ( var line in text.Replace( "\r", "" ).Split( '\n' ) )
		{
			var penX = originX;
			if ( align != TextAlign.Left )
			{
				var width = Measure( line );
				penX = align == TextAlign.Center ? originX - width / 2f : originX - width;
			}

			foreach ( var c in line )
			{
				if ( !glyphs.TryGetValue( c, out var g ) )
					continue;

				if ( g.Width > 0 && g.Height > 0 )
				{
					placed.Add( new PlacedGlyph(
						penX + g.XBearing, lineY + g.YBearing, g.Width, g.Height,
						g.U0, g.V0, g.U1, g.V1 ) );
				}

				penX += g.Advance;
			}

			lineY += LineHeight;
		}

		return placed;
	}
}
