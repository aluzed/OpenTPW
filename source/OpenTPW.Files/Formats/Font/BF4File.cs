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
///   ...: glyph blocks, each starting with a uint32 character code (confirmed,
///        e.g. 42 = '*'); the remaining per-glyph fields (metrics + bitmap) are not
///        yet fully decoded and are exposed here as raw bytes.
/// </code>
///
/// See docs/tickets/T-008 for the full reverse-engineering notes.
/// </summary>
public sealed class BF4File : BaseFormat
{
	private const string Magic = "F4FB";
	private const int HeaderSize = 8;

	public readonly struct Glyph
	{
		/// <summary>The character this glyph represents (the block's leading uint32).</summary>
		public int CharCode { get; init; }

		/// <summary>
		/// The raw glyph block (metrics + bitmap). Its inner layout is not yet decoded —
		/// see the class summary / T-008.
		/// </summary>
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
			Glyphs.Add( new Glyph { CharCode = charCode, Data = data[start..end] } );
		}
	}
}
