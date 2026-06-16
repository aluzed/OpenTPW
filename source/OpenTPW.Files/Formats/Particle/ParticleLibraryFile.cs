using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// A single particle effect within a <see cref="ParticleLibraryFile"/>.
/// </summary>
public sealed class ParticleEffect
{
	/// <summary>Index of this effect in the library (matches the <c>P_EFFECT_*</c> ids in par_lib.h).</summary>
	public int Index { get; init; }

	/// <summary>
	/// The embedded effect name (e.g. "Sparks"). Empty for reserved/unused slots.
	/// </summary>
	public string Name { get; init; } = "";

	/// <summary>
	/// The raw per-effect parameter block (everything in the record before the name field).
	/// The individual fields (lifetime, spawn rate, colour ramp, …) are not yet decoded.
	/// </summary>
	public byte[] Parameters { get; init; } = Array.Empty<byte>();
}

/// <summary>
/// Reader for Theme Park World particle library files (<c>.PLB</c>, e.g. <c>Tp2.plb</c>).
///
/// No published spec; reverse-engineered from a real sample cross-referenced with the
/// original <c>par_lib.h</c> on the disc (which lists the <c>P_EFFECT_*</c> names in index
/// order). Layout (confirmed against <c>Tp2.plb</c>):
/// <code>
///   Header (16 bytes):
///     0  : 4   effect count        (observed: 105)
///     4  : 4   record size in bytes (observed: 320)
///     8  : 8   reserved (zero)
///   Then `count` fixed-size records of `recordSize` bytes each:
///     0            : recordSize-48  parameter block (kept raw)
///     recordSize-48: 48             null-padded ASCII effect name
///   Trailing data after the records (a shared block, kept raw).
/// </code>
/// The effect names decode exactly to par_lib.h (NULL, Sparks, Smoke, …). The per-effect
/// parameters are exposed raw, like the mesh data in <see cref="MTRFile"/>.
/// See docs/tickets/T-008.
/// </summary>
public sealed class ParticleLibraryFile : BaseFormat
{
	private const int HeaderSize = 16;

	// The effect name occupies the final 48 bytes of each record (confirmed from the sample).
	private const int NameFieldLength = 48;

	/// <summary>Size in bytes of a single effect record (observed: 320).</summary>
	public int RecordSize { get; private set; }

	/// <summary>The effects, in index order. Reserved slots have an empty <see cref="ParticleEffect.Name"/>.</summary>
	public IReadOnlyList<ParticleEffect> Effects { get; private set; } = Array.Empty<ParticleEffect>();

	/// <summary>Shared data following the effect records (not yet decoded).</summary>
	public byte[] TrailingData { get; private set; } = Array.Empty<byte>();

	public ParticleLibraryFile( string path ) => ReadFromFile( path );

	public ParticleLibraryFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var bytes = ms.ToArray();

		if ( bytes.Length < HeaderSize )
			throw new InvalidDataException( $"PLB: file too small ({bytes.Length} bytes)." );

		var count = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 0, 4 ) );
		RecordSize = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( 4, 4 ) );

		if ( RecordSize <= NameFieldLength )
			throw new InvalidDataException( $"PLB: implausible record size {RecordSize}." );

		var recordsEnd = (long)HeaderSize + (long)count * RecordSize;
		if ( count < 0 || recordsEnd > bytes.Length )
			throw new InvalidDataException(
				$"PLB: {count} records of {RecordSize} bytes exceed the file ({bytes.Length} bytes)." );

		var paramLength = RecordSize - NameFieldLength;
		var effects = new ParticleEffect[count];

		for ( var i = 0; i < count; i++ )
		{
			var recordStart = HeaderSize + i * RecordSize;
			var nameStart = recordStart + paramLength;

			effects[i] = new ParticleEffect
			{
				Index = i,
				Name = ReadFixedString( bytes, nameStart, NameFieldLength ),
				Parameters = bytes[recordStart..(recordStart + paramLength)],
			};
		}

		Effects = effects;
		TrailingData = bytes[(int)recordsEnd..];
	}

	private static string ReadFixedString( byte[] bytes, int offset, int maxLength )
	{
		var end = offset;
		var limit = offset + maxLength;
		while ( end < limit && bytes[end] != 0 )
			end++;

		return Encoding.ASCII.GetString( bytes, offset, end - offset );
	}
}
