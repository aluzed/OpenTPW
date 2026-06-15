using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World material files (<c>.MTR</c>).
///
/// No published spec exists; reverse-engineered from samples. <c>.MTR</c> is the material
/// companion to a same-named <c>.MD2</c> mesh (e.g. <c>BANKRUPT.MTR</c> ↔ <c>BANKRUPT.MD2</c>).
/// Layout (confirmed):
/// <code>
///   0  : 4   magic 0x2E5915AF
///   4  : 4   version (observed: 6)
///   8  : 12  fields (1, 1, 0 — not fully decoded)
///   20 : 4   name offset (points to a null-terminated ASCII name, e.g. "s_bkrupt")
///   24 : 12  fields
///   36 : ... per-mesh material/index data, up to the name offset (kept raw)
/// </code>
/// Only the header + name are decoded (verified by the name-offset matching the embedded
/// string); the data array is mesh-coupled and exposed as raw <see cref="Data"/>.
/// See docs/tickets/T-008.
/// </summary>
public sealed class MTRFile : BaseFormat
{
	private const uint Magic = 0x2E5915AF;

	/// <summary>Header version (observed value: 6).</summary>
	public uint Version { get; private set; }

	/// <summary>The material/mesh name (e.g. "s_bkrupt").</summary>
	public string Name { get; private set; } = "";

	/// <summary>Raw material/index data between the header and the name (not yet decoded).</summary>
	public byte[] Data { get; private set; } = Array.Empty<byte>();

	public MTRFile( string path ) => ReadFromFile( path );

	public MTRFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var bytes = ms.ToArray();

		if ( bytes.Length < 36 )
			throw new InvalidDataException( $"MTR: file too small ({bytes.Length} bytes)." );

		var magic = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 0, 4 ) );
		if ( magic != Magic )
			throw new InvalidDataException( $"MTR: bad magic 0x{magic:X8}, expected 0x{Magic:X8}." );

		Version = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 4, 4 ) );

		var nameOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 20, 4 ) );
		if ( nameOffset < 36 || nameOffset >= bytes.Length )
			throw new InvalidDataException( $"MTR: name offset {nameOffset} out of range ({bytes.Length} bytes)." );

		// Name is a null-terminated ASCII string at nameOffset.
		var end = nameOffset;
		while ( end < bytes.Length && bytes[end] != 0 )
			end++;
		Name = Encoding.ASCII.GetString( bytes, nameOffset, end - nameOffset );

		Data = bytes[36..nameOffset];
	}
}
