using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>The audio-catalog variant of a <c>.MAP</c> file (from its GUID).</summary>
public enum MapVariant
{
	/// <summary>Unrecognized GUID.</summary>
	Unknown,
	/// <summary>BANK catalog (named sound-bank entries).</summary>
	Bank,
	/// <summary>SFX catalog (per-sound records with category defaults).</summary>
	Sfx,
}

/// <summary>
/// Reader for Theme Park World <c>.MAP</c> files.
///
/// Despite the extension, these are <b>not</b> terrain/tile maps — every <c>.MAP</c> on
/// the disc is a <c>CAT_*</c> file under SOUND / MUSIC / SPEECH directories: an audio
/// <b>category catalog</b>. Each file begins with a 16-byte COM class GUID identifying the
/// catalog type (DirectMusic family <c>{e9612c0?-31d0-11d2-b409-00?0c993f203}</c>).
///
/// Two variants are seen in the wild, distinguished by the GUID byte at offset 11
/// (<see cref="MapVariant"/>):
/// <list type="bullet">
///   <item><b>BANK</b> (<c>…00<u>a0</u>c993f203</c>): after the 16-byte GUID and 8 reserved
///   bytes, a <c>uint32</c> entry count, then one fixed 11-byte record per entry, then a
///   trailing table of <c>count</c> length-prefixed ASCII names (e.g. "Sound\Kids"). The names
///   are decoded into <see cref="Entries"/>; each 11-byte record is an opaque field block
///   (<c>uint32</c> + a constant + 3 bytes — semantics undetermined, see T-016).</item>
///   <item><b>SFX</b> (<c>…00<u>b0</u>c993f203</c>): a single category header — a
///   <c>uint32</c> sound-entry count (<see cref="SoundEntryCount"/>) and three category
///   default parameters (<see cref="CategoryParameters"/>, observed (1.0, 2.0, 0.5) = unity
///   volume + a ±octave pitch range) — followed by a variable per-sound record list that is
///   kept raw (its layout is nested/variable, undetermined without the engine; see T-016).</item>
/// </list>
/// See docs/tickets/T-016.
/// </summary>
public sealed class MapFile : BaseFormat
{
	// BANK header: GUID (16) + reserved (8) + uint32 count.
	private const int CountOffset = 24;
	private const int EntryDataOffset = 28;

	// SFX header: GUID (16) + reserved (8) + uint32 categoryCount(=1) + uint32 soundEntryCount
	// + uint32 pad + uint32 flags, then three float default parameters.
	private const int SoundEntryCountOffset = 28;
	private const int CategoryParamsOffset = 40;
	private const int CategoryParamCount = 3;

	// The GUID byte that distinguishes the variants (0xA0 = BANK, 0xB0 = SFX).
	private const int VariantByteOffset = 11;

	// Sanity bounds for the scan that locates the trailing name table.
	private const int MaxEntries = 1024;
	private const int MaxNameLength = 256;

	/// <summary>The catalog's COM class GUID (identifies the category type).</summary>
	public Guid CategoryType { get; private set; }

	/// <summary>Which catalog variant this file is (from the GUID).</summary>
	public MapVariant Variant { get; private set; } = MapVariant.Unknown;

	/// <summary>The entry count from the BANK header (number of <see cref="Entries"/>); 0 otherwise.</summary>
	public int EntryCount { get; private set; }

	/// <summary>
	/// The decoded entry names for the BANK variant (e.g. "Sound\Kids"); empty for the SFX
	/// variant or any file without a recognizable trailing name table.
	/// </summary>
	public IReadOnlyList<string> Entries { get; private set; } = Array.Empty<string>();

	/// <summary>The number of per-sound records in the SFX variant (0 for BANK).</summary>
	public int SoundEntryCount { get; private set; }

	/// <summary>
	/// The SFX category-level default parameters (observed (1.0, 2.0, 0.5) — unity volume and a
	/// ±octave pitch range); empty for the BANK variant.
	/// </summary>
	public IReadOnlyList<float> CategoryParameters { get; private set; } = Array.Empty<float>();

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
		Variant = bytes[VariantByteOffset] switch
		{
			0xA0 => MapVariant.Bank,
			0xB0 => MapVariant.Sfx,
			_ => MapVariant.Unknown,
		};

		if ( Variant == MapVariant.Sfx )
		{
			ReadSfxHeader( bytes );
			return;
		}

		// BANK (and unknown, which the name-table scan simply leaves empty).
		if ( bytes.Length < EntryDataOffset )
			return;

		EntryCount = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( CountOffset, 4 ) );
		Entries = ReadEntryNames( bytes, EntryCount );
	}

	private void ReadSfxHeader( byte[] bytes )
	{
		if ( bytes.Length >= SoundEntryCountOffset + 4 )
			SoundEntryCount = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( SoundEntryCountOffset, 4 ) );

		if ( bytes.Length >= CategoryParamsOffset + CategoryParamCount * 4 )
		{
			var parameters = new float[CategoryParamCount];
			for ( var i = 0; i < CategoryParamCount; i++ )
				parameters[i] = BinaryPrimitives.ReadSingleLittleEndian( bytes.AsSpan( CategoryParamsOffset + i * 4, 4 ) );
			CategoryParameters = parameters;
		}
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
