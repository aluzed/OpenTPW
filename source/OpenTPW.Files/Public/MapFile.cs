using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// One per-sound record from an SFX <c>.MAP</c> catalog (a fixed 20-byte entry). <see cref="SoundId"/>
/// is the sound's index within the category's banks (see the matching BANK catalog's name list);
/// <see cref="VariationCount"/> is how many alternate samples that sound has (usually 1). <c>Param</c>
/// and <c>Flags</c> are per-sound mixing values (e.g. 3200/2300/700 across categories) whose exact
/// units await the engine. See docs/tickets/T-016.
/// </summary>
public readonly record struct MapSoundEntry( int SoundId, int VariationCount, int Param, int Flags );

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
///   (the real catalog data) are decoded into <see cref="Entries"/>. The 11-byte records are
///   <b>serialized object state</b>, not catalog data: each is a <c>uint32</c> + the constant
///   <c>0x0066F22C</c> + 3 marker bytes, and those <c>uint32</c>s are stale <c>.text</c> code
///   pointers (verified in Ghidra) baked in when the catalog was saved — so they carry no
///   meaning at load. See T-016.</item>
///   <item><b>SFX</b> (<c>…00<u>b0</u>c993f203</c>): a single category header — a
///   <c>uint32</c> sound-entry count (<see cref="SoundEntryCount"/>) and three category
///   default parameters (<see cref="CategoryParameters"/>, observed (1.0, 2.0, 0.5) = unity
///   volume + a ±octave pitch range) — followed by <see cref="SoundEntryCount"/> fixed 20-byte
///   per-sound records, decoded into <see cref="SoundEntries"/> (<see cref="MapSoundEntry"/>:
///   sound id + variation count + mixing params). A trailing serialized blob (the mixing-curve
///   objects, with embedded pointers like the BANK records) follows and is kept raw. See T-016.</item>
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

	// After the category header comes a table of `soundEntryCount` fixed 20-byte per-sound records
	// (verified: record count == soundEntryCount across every cat_*SFX sample). A trailing serialized
	// blob (mixing-curve objects with embedded pointers) follows it and is kept raw — see T-016.
	private const int SfxRecordsOffset = CategoryParamsOffset + CategoryParamCount * 4; // 52
	private const int SfxRecordStride = 20;

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

	/// <summary>The decoded SFX per-sound records (<see cref="SoundEntryCount"/> of them); empty for
	/// BANK or a malformed file. See <see cref="MapSoundEntry"/>.</summary>
	public IReadOnlyList<MapSoundEntry> SoundEntries { get; private set; } = Array.Empty<MapSoundEntry>();

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

		// The per-sound record table: SoundEntryCount fixed 20-byte records. Only parse the full table
		// if it fits (otherwise leave SoundEntries empty rather than read a partial/garbage record).
		if ( SoundEntryCount > 0 && SoundEntryCount <= MaxEntries
			&& bytes.Length >= SfxRecordsOffset + SoundEntryCount * SfxRecordStride )
		{
			var entries = new MapSoundEntry[SoundEntryCount];
			for ( var i = 0; i < SoundEntryCount; i++ )
			{
				var r = bytes.AsSpan( SfxRecordsOffset + i * SfxRecordStride, SfxRecordStride );
				entries[i] = new MapSoundEntry(
					SoundId: (int)BinaryPrimitives.ReadUInt32LittleEndian( r[..4] ),
					VariationCount: (int)BinaryPrimitives.ReadUInt32LittleEndian( r[4..8] ),
					// r[8..12] is always 0 (reserved).
					Param: (int)BinaryPrimitives.ReadUInt32LittleEndian( r[12..16] ),
					Flags: (int)BinaryPrimitives.ReadUInt32LittleEndian( r[16..20] ) );
			}
			SoundEntries = entries;
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
