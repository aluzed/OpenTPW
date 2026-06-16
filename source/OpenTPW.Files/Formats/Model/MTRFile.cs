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
///   36 : ... uint32 index/grouping array, up to the name offset (see Indices)
///   ...      null-terminated name, then a constant ~847-byte trailing block (kept raw)
/// </code>
/// The header, name and the uint32 array are decoded (the name-offset matches the embedded
/// string, confirming the bounds). Observed for the index array (cross-referenced with the
/// companion <c>.MD2</c>): it begins with a per-vertex ramp up to roughly the mesh's face
/// count, followed by a block of small values — i.e. a mesh-coupled grouping table whose exact
/// per-element semantics are still undetermined. Exposed faithfully as <see cref="Indices"/>
/// (and as raw <see cref="Data"/> for back-compat). See docs/tickets/T-008.
/// </summary>
public sealed class MTRFile : BaseFormat
{
	private const uint Magic = 0x2E5915AF;
	private const int ArrayOffset = 36;

	/// <summary>Header version (observed value: 6).</summary>
	public uint Version { get; private set; }

	/// <summary>The material/mesh name (e.g. "s_bkrupt").</summary>
	public string Name { get; private set; } = "";

	/// <summary>
	/// The uint32 index/grouping array between the header and the name (mesh-coupled to the
	/// companion <c>.MD2</c>; exact per-element semantics undetermined — see the type remarks).
	/// </summary>
	public uint[] Indices { get; private set; } = Array.Empty<uint>();

	/// <summary>The raw bytes of the index array (same region as <see cref="Indices"/>), for back-compat.</summary>
	public byte[] Data { get; private set; } = Array.Empty<byte>();

	/// <summary>The bytes following the name (a constant-size shared block, not yet decoded).</summary>
	public byte[] TrailingData { get; private set; } = Array.Empty<byte>();

	public MTRFile( string path ) => ReadFromFile( path );

	public MTRFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var bytes = ms.ToArray();

		if ( bytes.Length < ArrayOffset )
			throw new InvalidDataException( $"MTR: file too small ({bytes.Length} bytes)." );

		var magic = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 0, 4 ) );
		if ( magic != Magic )
			throw new InvalidDataException( $"MTR: bad magic 0x{magic:X8}, expected 0x{Magic:X8}." );

		Version = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 4, 4 ) );

		var nameOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 20, 4 ) );
		if ( nameOffset < ArrayOffset || nameOffset > bytes.Length )
			throw new InvalidDataException( $"MTR: name offset {nameOffset} out of range ({bytes.Length} bytes)." );

		// Name is a null-terminated ASCII string at nameOffset.
		var end = nameOffset;
		while ( end < bytes.Length && bytes[end] != 0 )
			end++;
		Name = Encoding.ASCII.GetString( bytes, nameOffset, end - nameOffset );

		Data = bytes[ArrayOffset..nameOffset];

		// The array region is whole uint32s; decode it faithfully.
		Indices = new uint[Data.Length / 4];
		for ( var i = 0; i < Indices.Length; i++ )
			Indices[i] = BinaryPrimitives.ReadUInt32LittleEndian( Data.AsSpan( i * 4, 4 ) );

		// Everything past the name terminator (a constant ~847-byte block on real samples).
		TrailingData = end < bytes.Length ? bytes[(end + 1)..] : Array.Empty<byte>();
	}
}
