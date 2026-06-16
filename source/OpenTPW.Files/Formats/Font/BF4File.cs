using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World bitmap fonts (<c>.BF4</c>, magic "F4FB").
///
/// No published spec exists; the layout below was reverse-engineered from sample fonts
/// and the container structure is verified by invariants (see <c>BF4FileTests</c>):
///
/// <code>
///   0  : 4   magic "F4FB"
///   4  : 1   version
///   5  : 1   (unknown)
///   6  : 2   glyph count (little-endian uint16)
///   8  : n*4 glyph offset table (uint32 byte offset of each glyph block);
///            the table tiles exactly up to the first glyph offset
///   ...: glyph blocks. Each block is:
///          0  : 4   character code (confirmed, e.g. 42 = '*')
///          4  : 4   line height (constant 8)
///          8  : 4   bitmap byte count (= width*height/2; redundant)
///          12 : 4   constant 2
///          16 : 2   glyph width  (little-endian uint16)
///          18 : 2   glyph height (little-endian uint16)
///          20 : 1   x bearing
///          21 : 1   y bearing
///          22 : 2   x advance (cursor step)
///          24 : ... 1bpp bitmap, MSB-first, width bits per row, height rows
/// </code>
///
/// All of the above were confirmed by rendering: a real font's glyph atlas is legible,
/// and laying glyphs out by their advance produces correctly-spaced text ("GAME OVER").
/// See docs/tickets/T-008. No published spec exists; reverse-engineered from sample fonts.
/// </summary>
public sealed class BF4File : BaseFormat
{
	private const string Magic = "F4FB";
	private const int HeaderSize = 8;

	public readonly struct Glyph
	{
		/// <summary>The character this glyph represents (the block's leading uint32).</summary>
		public int CharCode { get; init; }

		/// <summary>Glyph width in pixels.</summary>
		public int Width { get; init; }

		/// <summary>Glyph height in pixels.</summary>
		public int Height { get; init; }

		/// <summary>Left side bearing (x offset before drawing the bitmap).</summary>
		public int XBearing { get; init; }

		/// <summary>Top side bearing (y offset within the line).</summary>
		public int YBearing { get; init; }

		/// <summary>Cursor advance after this glyph, in pixels (proportional spacing).</summary>
		public int Advance { get; init; }

		/// <summary>
		/// Row-major 1bpp pixel mask, <see cref="Width"/> × <see cref="Height"/>
		/// (<c>true</c> = set). Empty for zero-size glyphs (e.g. space).
		/// </summary>
		public bool[] Pixels { get; init; }

		/// <summary>The raw glyph block (for fields not yet decoded). See T-008.</summary>
		public byte[] Data { get; init; }
	}

	/// <summary>Header version byte (observed value: 2).</summary>
	public int Version { get; private set; }

	public List<Glyph> Glyphs { get; } = new();

	public BF4File( string path ) => ReadFromFile( path );

	public BF4File( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var data = ms.ToArray();

		if ( data.Length < HeaderSize )
			throw new InvalidDataException( "BF4: file is smaller than the header." );

		var magic = Encoding.ASCII.GetString( data, 0, 4 );
		if ( magic != Magic )
			throw new InvalidDataException( $"BF4: bad magic '{magic}', expected '{Magic}'." );

		Version = data[4];
		int glyphCount = BinaryPrimitives.ReadUInt16LittleEndian( data.AsSpan( 6, 2 ) );

		var tableEnd = HeaderSize + glyphCount * 4;
		if ( tableEnd > data.Length )
			throw new InvalidDataException( "BF4: glyph offset table runs past end of file." );

		var offsets = new int[glyphCount];
		for ( var i = 0; i < glyphCount; i++ )
			offsets[i] = (int)BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( HeaderSize + i * 4, 4 ) );

		for ( var i = 0; i < glyphCount; i++ )
		{
			var start = offsets[i];
			var end = i + 1 < glyphCount ? offsets[i + 1] : data.Length;

			// Each block must sit after the table and hold at least its 4-byte char code.
			if ( start < tableEnd || end > data.Length || end < start + 4 )
			{
				Log.Warning( $"BF4: glyph {i} has an out-of-range block ({start}..{end}); skipping." );
				continue;
			}

			var charCode = (int)BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( start, 4 ) );
			var block = data[start..end];

			// Width/height @16/18; bearings @20/21; advance @22; bitmap @24.
			var hasMetrics = block.Length >= 24;
			var width = hasMetrics ? BinaryPrimitives.ReadUInt16LittleEndian( block.AsSpan( 16, 2 ) ) : 0;
			var height = hasMetrics ? BinaryPrimitives.ReadUInt16LittleEndian( block.AsSpan( 18, 2 ) ) : 0;
			var xBearing = hasMetrics ? block[20] : 0;
			var yBearing = hasMetrics ? block[21] : 0;
			var advance = hasMetrics ? BinaryPrimitives.ReadUInt16LittleEndian( block.AsSpan( 22, 2 ) ) : 0;
			var pixels = DecodeBitmap( block, width, height );

			Glyphs.Add( new Glyph
			{
				CharCode = charCode,
				Width = width,
				Height = height,
				XBearing = xBearing,
				YBearing = yBearing,
				Advance = advance,
				Pixels = pixels,
				Data = block,
			} );
		}
	}

	/// <summary>
	/// Decodes the 1bpp glyph bitmap that starts at block offset 24: a continuous
	/// MSB-first bitstream, <paramref name="width"/> bits per row, <paramref name="height"/>
	/// rows. Missing bits (short block) read as 0.
	/// </summary>
	private static bool[] DecodeBitmap( byte[] block, int width, int height )
	{
		const int bitmapOffset = 24;
		var count = width * height;
		var pixels = new bool[count < 0 ? 0 : count];

		for ( var i = 0; i < pixels.Length; i++ )
		{
			var bit = i;
			var byteIndex = bitmapOffset + (bit >> 3);
			if ( byteIndex >= block.Length )
				break;

			pixels[i] = ( (block[byteIndex] >> (7 - (bit & 7))) & 1 ) != 0;
		}

		return pixels;
	}
}
