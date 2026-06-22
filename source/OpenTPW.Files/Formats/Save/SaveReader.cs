using System.Buffers.Binary;
using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

/// <summary>
/// Reader/writer for Theme Park World save files (<c>.TPWS</c> offline, <c>upload.LAYS</c> online). The
/// <b>container</b> format is reverse-engineered from the no-CD <c>tp.exe</c> loader (Ghidra:
/// <c>FUN_00414d40</c> → header <c>FUN_00416240</c>):
/// <code>
///   u32  version          (LE; current = 500. The loader rejects version &gt; 500 as "future").
///   u8   headerByte
///   1280 legalText        (the copyright/legal block; the loader checksums it)
///   256  headerStruct     (a header/config struct; first 32 bytes copied to a global)
///   u32  fileType         (BIG-endian / ntohl; current = 0x00012219, checked against a constant)
///   u32  headerFlag       (0 = no embedded header, 1 = header present / online)
///   BILZ block            "BILZ", u32 rawSize, u32 packedSize, 16 bytes, then a zlib stream
/// </code>
/// The previous reader mistook the leading <c>F4 01 00 00</c> for a magic — it is the little-endian
/// version <b>500</b>. The decompressed <see cref="Payload"/> is the concatenation of the engine's
/// <c>SAD_*</c> module chunks (UI, Advisor, Coasters, Ridesystem, Particles, …); decoding those
/// individual modules is out of scope here and the payload is kept opaque. See docs/tickets/T-017.
/// </summary>
public class SaveReader : BaseFormat
{
	/// <summary>The current/maximum save version the game accepts (loader rejects anything higher).</summary>
	public const uint CurrentVersion = 500;

	/// <summary>The expected big-endian <c>fileType</c> tag (<c>00 01 22 19</c>).</summary>
	public const uint OfflineFileType = 0x00012219;

	private const int LegalTextLength = 0x500;   // 1280
	private const int HeaderStructLength = 0x100; // 256
	private const int HeaderEnd = 4 + 1 + LegalTextLength + HeaderStructLength + 4 + 4; // 1549, where BILZ starts
	private const int BilzSideBytes = 16;

	public byte[] buffer = null!;

	/// <summary>Save-format version (LE u32 at offset 0). Current saves are <see cref="CurrentVersion"/>.</summary>
	public uint Version { get; private set; }
	public byte HeaderByte { get; private set; }
	public byte[] LegalText { get; private set; } = new byte[LegalTextLength];
	public byte[] HeaderStruct { get; private set; } = new byte[HeaderStructLength];
	/// <summary>The big-endian <c>fileType</c> tag (offline saves: <see cref="OfflineFileType"/>).</summary>
	public uint FileType { get; private set; }
	/// <summary>Header/online flag (1 = an embedded header is present / online save).</summary>
	public uint HeaderFlag { get; private set; }

	/// <summary>The decompressed module data (the <c>SAD_*</c> chunk stream). Opaque — see the class note.</summary>
	public byte[] Payload { get; private set; } = Array.Empty<byte>();

	public SaveReader( string path )
	{
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public SaveReader( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		buffer = ms.ToArray();
	}

	/// <summary>Parses the container header and returns the decompressed module payload.</summary>
	public byte[] ReadFile()
	{
		if ( buffer.Length < HeaderEnd + 4 )
			throw new InvalidDataException( $"TPWS: file too small ({buffer.Length} bytes)." );

		Version = BinaryPrimitives.ReadUInt32LittleEndian( buffer.AsSpan( 0, 4 ) );
		if ( Version > CurrentVersion )
			throw new InvalidDataException( $"TPWS: future-version save ({Version} > {CurrentVersion}); needs a newer game." );

		HeaderByte = buffer[4];
		LegalText = buffer[5..(5 + LegalTextLength)];
		var structStart = 5 + LegalTextLength;
		HeaderStruct = buffer[structStart..(structStart + HeaderStructLength)];

		// fileType is stored big-endian (the loader runs it through ntohl before comparing).
		FileType = BinaryPrimitives.ReadUInt32BigEndian( buffer.AsSpan( structStart + HeaderStructLength, 4 ) );
		HeaderFlag = BinaryPrimitives.ReadUInt32LittleEndian( buffer.AsSpan( structStart + HeaderStructLength + 4, 4 ) );

		// BILZ block: magic + raw/packed sizes + 16 bytes, then a zlib stream to EOF.
		var p = HeaderEnd;
		var magic = Encoding.ASCII.GetString( buffer, p, 4 );
		if ( magic != "BILZ" )
			throw new InvalidDataException( $"TPWS: expected BILZ block, found '{magic}'." );
		var rawSize = (int)BinaryPrimitives.ReadUInt32LittleEndian( buffer.AsSpan( p + 4, 4 ) );
		p += 4 + 4 + 4 + BilzSideBytes; // magic + rawSize + packedSize + 16 side bytes

		using var compressed = new MemoryStream( buffer, p, buffer.Length - p );
		using var inflater = new InflaterInputStream( compressed );
		using var outStream = new MemoryStream( rawSize > 0 ? rawSize : 0 );
		inflater.CopyTo( outStream );
		Payload = outStream.ToArray();
		return Payload;
	}

	/// <summary>
	/// Serialises the container back to bytes (header + BILZ + zlib(<see cref="Payload"/>)). This
	/// round-trips a loaded save: <c>Read → Write → Read</c> yields the same header fields and payload.
	/// The inner module data is written verbatim from <see cref="Payload"/> (it is not re-modelled).
	/// </summary>
	public byte[] Serialize() =>
		Build( Version, FileType, HeaderFlag, HeaderByte, LegalText, HeaderStruct, Payload );

	/// <summary>Builds a complete <c>.TPWS</c> container from its parts (used by <see cref="Serialize"/>).</summary>
	public static byte[] Build( uint version, uint fileType, uint headerFlag, byte headerByte,
		byte[] legalText, byte[] headerStruct, byte[] payload )
	{
		if ( legalText.Length != LegalTextLength )
			throw new ArgumentException( $"legalText must be {LegalTextLength} bytes.", nameof( legalText ) );
		if ( headerStruct.Length != HeaderStructLength )
			throw new ArgumentException( $"headerStruct must be {HeaderStructLength} bytes.", nameof( headerStruct ) );

		var packed = ZlibCompress( payload );

		using var ms = new MemoryStream();
		using var bw = new BinaryWriter( ms );
		Span<byte> u32 = stackalloc byte[4];

		BinaryPrimitives.WriteUInt32LittleEndian( u32, version ); bw.Write( u32 );
		bw.Write( headerByte );
		bw.Write( legalText );
		bw.Write( headerStruct );
		BinaryPrimitives.WriteUInt32BigEndian( u32, fileType ); bw.Write( u32 );
		BinaryPrimitives.WriteUInt32LittleEndian( u32, headerFlag ); bw.Write( u32 );

		bw.Write( Encoding.ASCII.GetBytes( "BILZ" ) );
		BinaryPrimitives.WriteUInt32LittleEndian( u32, (uint)payload.Length ); bw.Write( u32 ); // rawSize
		BinaryPrimitives.WriteUInt32LittleEndian( u32, (uint)packed.Length ); bw.Write( u32 );  // packedSize
		bw.Write( new byte[BilzSideBytes] );
		bw.Write( packed );

		bw.Flush();
		return ms.ToArray();
	}

	private static byte[] ZlibCompress( byte[] data )
	{
		using var ms = new MemoryStream();
		using ( var deflater = new DeflaterOutputStream( ms, new Deflater( Deflater.BEST_COMPRESSION ) ) { IsStreamOwner = false } )
			deflater.Write( data, 0, data.Length );
		return ms.ToArray();
	}

	/// <summary>The decompressed save as an ASCII string with NULs stripped (debug aid).</summary>
	public string FileToString() => Encoding.ASCII.GetString( ReadFile() ).Replace( "\0", string.Empty );
}
