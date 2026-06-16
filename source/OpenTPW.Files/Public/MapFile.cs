using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World <c>.MAP</c> files.
///
/// Despite the extension, these are <b>not</b> terrain/tile maps — every <c>.MAP</c> on
/// the disc is a <c>CAT_*</c> file under SOUND / MUSIC / SPEECH directories: an audio
/// <b>category catalog</b>. Each file begins with a 16-byte COM class GUID identifying the
/// catalog type (DirectMusic family <c>{e9612c0?-31d0-11d2-b409-00?0c993f203}</c>).
///
/// Two variants are seen in the wild, distinguished by that GUID:
/// <list type="bullet">
///   <item><b>BANK</b> (<c>…00a0c993f203</c>): after the 16-byte GUID and 8 reserved bytes,
///   a <c>uint32</c> entry count, then one fixed 11-byte record per entry, then a trailing
///   table of <c>count</c> length-prefixed ASCII names (e.g. "Sound\Kids"). These names are
///   decoded into <see cref="Entries"/>.</item>
///   <item><b>SFX</b> (<c>…00b0c993f203</c>): a different binary record layout with no name
///   table; <see cref="Entries"/> is empty and the body is left raw.</item>
/// </list>
/// The leading GUID and (for BANK) the entry names are decoded; the per-entry binary records
/// are kept raw. See docs/tickets/T-012.
/// </summary>
public sealed class MapFile : BaseFormat
{
	// Layout of the BANK variant's header: GUID (16) + reserved (8) + uint32 count.
	private const int CountOffset = 24;
	private const int EntryDataOffset = 28;

	// Sanity bounds for the scan that locates the trailing name table.
	private const int MaxEntries = 1024;
	private const int MaxNameLength = 256;

	/// <summary>The catalog's COM class GUID (identifies the category type).</summary>
	public Guid CategoryType { get; private set; }

	/// <summary>The entry count from the header (0 if the file is too small to hold one).</summary>
	public int EntryCount { get; private set; }

	/// <summary>
	/// The decoded entry names for the BANK variant (e.g. "Sound\Kids"); empty for the SFX
	/// variant or any file without a recognizable trailing name table.
	/// </summary>
	public IReadOnlyList<string> Entries { get; private set; } = Array.Empty<string>();

	/// <summary>The raw bytes following the 16-byte GUID header (kept for callers that need it).</summary>
	public byte[] Data { get; private set; } = Array.Empty<byte>();

	public MapFile( string path ) => ReadFromFile( path );

	public MapFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var bytes = ms.ToArray();

		if ( bytes.Length < 16 )
			throw new InvalidDataException( $"MAP: file too small ({bytes.Length} bytes) for a category GUID." );

		CategoryType = new Guid( bytes.AsSpan( 0, 16 ) );
		Data = bytes[16..];

		if ( bytes.Length < EntryDataOffset )
			return;

		EntryCount = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( CountOffset, 4 ) );
		Entries = ReadEntryNames( bytes, EntryCount );
	}

	/// <summary>
	/// Locates the trailing table of <paramref name="count"/> length-prefixed ASCII names.
	/// The records preceding it are a fixed size we don't fully decode, so rather than hard-code
	/// it we scan for the unique start at which exactly <paramref name="count"/> valid strings
	/// consume the file to its end (self-validating; returns empty if there's no such table).
	/// </summary>
	private static IReadOnlyList<string> ReadEntryNames( byte[] bytes, int count )
	{
		if ( count <= 0 || count > MaxEntries )
			return Array.Empty<string>();

		for ( var start = EntryDataOffset; start < bytes.Length; start++ )
		{
			if ( TryReadStringTable( bytes, start, count, out var names ) )
				return names;
		}

		return Array.Empty<string>();
	}

	private static bool TryReadStringTable( byte[] bytes, int offset, int count, out IReadOnlyList<string> names )
	{
		names = Array.Empty<string>();
		var result = new string[count];

		for ( var i = 0; i < count; i++ )
		{
			if ( offset + 4 > bytes.Length )
				return false;

			var length = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( offset, 4 ) );
			offset += 4;

			if ( length < 1 || length > MaxNameLength || offset + length > bytes.Length )
				return false;

			// length includes the trailing NUL; the rest must be printable ASCII.
			if ( bytes[offset + length - 1] != 0 )
				return false;

			for ( var j = 0; j < length - 1; j++ )
			{
				if ( bytes[offset + j] < 0x20 || bytes[offset + j] > 0x7E )
					return false;
			}

			result[i] = Encoding.ASCII.GetString( bytes, offset, length - 1 );
			offset += length;
		}

		// The table must consume the file exactly — that's what makes the start unambiguous.
		if ( offset != bytes.Length )
			return false;

		names = result;
		return true;
	}
}
