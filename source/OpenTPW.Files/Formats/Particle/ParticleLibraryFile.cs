using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// An 8-bit-per-channel colour from a particle colour ramp. Decoded from the original
/// D3DCOLOR packing (a little-endian <c>0xAARRGGBB</c> word).
/// </summary>
public readonly record struct ParticleColor( byte R, byte G, byte B, byte A );

/// <summary>A 3-component fixed-point vector from a particle effect record (the engine stores these as
/// 16.16-ish fixed-point ints; consumers shift by 4/10). Exposed raw — sign + relative magnitude are the
/// meaningful part.</summary>
public readonly record struct ParticleVec3( int X, int Y, int Z );

/// <summary>How a particle effect emits its particles (<c>short</c> at record byte 0x0a). RE'd from the
/// spawn path <c>FUN_00521930</c>: 0 releases <see cref="ParticleEffect.BurstCount"/> particles in one go,
/// 1 emits continuously.</summary>
public enum ParticleEmissionMode { Burst = 0, Continuous = 1 }

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
	/// The per-particle colour gradient over its lifetime: 16 RGBA stops at the end of the
	/// record. Decoded and verified to be semantically correct (Fire ramps dark-red→bright,
	/// Smoke is white with an alpha fade in/out, …).
	/// </summary>
	public IReadOnlyList<ParticleColor> ColorRamp { get; init; } = Array.Empty<ParticleColor>();

	/// <summary>
	/// The raw per-effect parameter block — the bytes before the <see cref="ColorRamp"/> (216 bytes on
	/// <c>Tp2.plb</c>). The engine treats the record as a <c>short[160]</c>. The typed properties below label
	/// the fields the particle *consumer* reads (RE'd from the spawn path <c>FUN_00521930</c> /
	/// <c>FUN_00520560</c> in the no-CD <c>tp.exe</c>); the remaining bytes stay raw here. See T-019.
	/// </summary>
	public byte[] Parameters { get; init; } = Array.Empty<byte>();

	// Field offsets within the record/parameter block (byte offsets; the engine reads them as short/int).
	// RE'd from FUN_00521930 (SpawnEffectInstance) + FUN_00520560 (SpawnParticle).
	private const int OffEmissionMode = 0x0a;  // short: 0 = burst, 1 = continuous
	private const int OffVelocityX = 0x40;     // int×3: initial particle velocity / emission direction
	private const int OffColorMode = 0x54;     // short: 0 = fixed colour, else cycle the ramp
	private const int OffBurstCount = 0x5c;    // int: particles released per burst
	private const int OffLifetime = 0x74;      // int: particle lifetime in ticks (±25% jitter)
	private const int OffAccelX = 0x80;        // int×3: per-tick acceleration / gravity
	private const int OffConeAngle = 0xa8;     // int: emission cone / spin angle (drives a sin/cos lookup)
	private const int OffVelocityScale = 0xac; // int: scales the spawn-velocity argument
	private const int OffChildEffect = 0xb0;   // int: index of a child effect spawned alongside (-1 = none)

	private short S16( int off )
		=> off >= 0 && off + 2 <= Parameters.Length ? BinaryPrimitives.ReadInt16LittleEndian( Parameters.AsSpan( off, 2 ) ) : (short)0;
	private int S32( int off )
		=> off >= 0 && off + 4 <= Parameters.Length ? BinaryPrimitives.ReadInt32LittleEndian( Parameters.AsSpan( off, 4 ) ) : 0;

	/// <summary>Whether this effect releases its particles in one burst or emits continuously (record byte 0x0a).</summary>
	public ParticleEmissionMode EmissionMode => (ParticleEmissionMode)S16( OffEmissionMode );

	/// <summary>How many particles a <see cref="ParticleEmissionMode.Burst"/> effect releases at once (the spawn
	/// loop count in <c>FUN_00521930</c>; record int at 0x5c).</summary>
	public int BurstCount => S32( OffBurstCount );

	/// <summary>Particle lifetime in engine ticks before it expires; the spawn jitters it by ±¼ (record int at
	/// 0x74). 0 means "use the loader's 1000-tick default".</summary>
	public int LifetimeTicks => S32( OffLifetime );

	/// <summary>Initial particle velocity / emission direction (record ints at 0x40/0x44/0x48). When all three
	/// are zero the particle inherits the emitter's own velocity.</summary>
	public ParticleVec3 EmissionVelocity => new( S32( OffVelocityX ), S32( OffVelocityX + 4 ), S32( OffVelocityX + 8 ) );

	/// <summary>Per-tick acceleration applied to each live particle — gravity/drift (record ints at 0x80/0x84/0x88).</summary>
	public ParticleVec3 Acceleration => new( S32( OffAccelX ), S32( OffAccelX + 4 ), S32( OffAccelX + 8 ) );

	/// <summary>Emission cone / spin angle that rotates the spawn velocity via the engine's sin/cos table
	/// (record int at 0xa8).</summary>
	public int EmissionConeAngle => S32( OffConeAngle );

	/// <summary>Scales the spawn-velocity argument the emitter is fired with (record int at 0xac).</summary>
	public int VelocityScale => S32( OffVelocityScale );

	/// <summary>True when this effect spawns a linked child effect; see <see cref="ChildEffect"/> for its index.</summary>
	public bool HasChildEffect => S32( OffChildEffect ) >= 0 && S32( OffChildEffect ) != 0;

	/// <summary>Index of a child effect spawned alongside this one (record int at 0xb0); negative/zero = none.</summary>
	public int ChildEffect => S32( OffChildEffect );

	/// <summary>Whether the particle keeps a fixed colour (0) or cycles the <see cref="ColorRamp"/> over its life
	/// (record short at 0x54).</summary>
	public bool CyclesColorRamp => S16( OffColorMode ) != 0;
}

/// <summary>
/// Reader for Theme Park World particle library files (<c>.PLB</c>, e.g. <c>Tp2.plb</c>).
///
/// No published spec; reverse-engineered from a real sample cross-referenced with the original
/// <c>par_lib.h</c> on the disc (the <c>P_EFFECT_*</c> names in index order) and confirmed against the
/// loader <c>FUN_0051f370</c> in the no-CD <c>tp.exe</c> (Ghidra). The loader reads:
/// <code>
///   Header (8 bytes):
///     0 : 4   effect count         (observed: 105, engine caps at 105)
///     4 : 4   record size in bytes (observed: 320)
///   Then `count` fixed-size effect records of `recordSize` bytes each (engine: a short[160]):
///     0            : recordSize-104  parameter block (raw; fields not yet labelled)
///     recordSize-104: 64             16-stop RGBA colour ramp (D3DCOLOR 0xAARRGGBB)
///     recordSize-40 : 40             null-padded ASCII effect name
///   Then a second "shared" table (decoded structurally, fields not yet labelled):
///     +0 : 4   record count2        (observed: 20)
///     +4 : 4   record size2         (observed: 104)
///     +8 : count2 * size2 bytes
///   Then two engine globals:
///     +0 : 4   particle density     (engine clamps to 10..500; observed: 33)
///     +4 : 4   total particles      (observed: 1024)
/// </code>
/// The effect names decode exactly to par_lib.h (NULL, Sparks, Smoke, …) and the ramps are
/// semantically correct (Fire ramps dark-red→bright, Smoke is white with an alpha fade-in). The
/// gameplay-meaningful per-effect fields are now labelled (<see cref="ParticleEffect.LifetimeTicks"/>,
/// <see cref="ParticleEffect.BurstCount"/>, <see cref="ParticleEffect.EmissionVelocity"/>,
/// <see cref="ParticleEffect.Acceleration"/>, <see cref="ParticleEffect.ChildEffect"/>, …), RE'd from the
/// particle spawn path (<c>FUN_00521930</c> / <c>FUN_00520560</c>) and verified against Tp2.plb (e.g. Sparks
/// = a 37-particle burst lasting 100 ticks). The shared table's 104-byte records stay raw. See T-019.
/// </summary>
public sealed class ParticleLibraryFile : BaseFormat
{
	// The loader (FUN_0051f370) reads only count + recordSize before the records — an 8-byte header.
	private const int HeaderSize = 8;

	// Within a record: the name is the final 40 bytes, the colour ramp the 64 bytes before it.
	private const int NameFieldLength = 40;
	private const int ColorRampStops = 16;
	private const int ColorRampBytes = ColorRampStops * 4;

	/// <summary>Size in bytes of a single effect record (observed: 320).</summary>
	public int RecordSize { get; private set; }

	/// <summary>The effects, in index order. Reserved slots have an empty <see cref="ParticleEffect.Name"/>.</summary>
	public IReadOnlyList<ParticleEffect> Effects { get; private set; } = Array.Empty<ParticleEffect>();

	/// <summary>Record size of the shared table that follows the effects (observed: 104).</summary>
	public int SharedRecordSize { get; private set; }

	/// <summary>The shared table after the effect records (observed: 20 records of 104 bytes). Decoded
	/// structurally from the loader; the per-record fields are not yet labelled. Was the old "trailing
	/// data" blob.</summary>
	public IReadOnlyList<byte[]> SharedRecords { get; private set; } = Array.Empty<byte[]>();

	/// <summary>Global particle density (the engine clamps it to 10..500; observed: 33).</summary>
	public int ParticleDensity { get; private set; }

	/// <summary>Global total-particle budget (observed: 1024).</summary>
	public int TotalParticles { get; private set; }

	/// <summary>Any bytes left after the two globals (expected empty on a well-formed file).</summary>
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

		if ( RecordSize <= NameFieldLength + ColorRampBytes )
			throw new InvalidDataException( $"PLB: implausible record size {RecordSize}." );

		var recordsEnd = (long)HeaderSize + (long)count * RecordSize;
		if ( count < 0 || recordsEnd > bytes.Length )
			throw new InvalidDataException(
				$"PLB: {count} records of {RecordSize} bytes exceed the file ({bytes.Length} bytes)." );

		var paramLength = RecordSize - NameFieldLength - ColorRampBytes;
		var effects = new ParticleEffect[count];

		for ( var i = 0; i < count; i++ )
		{
			var recordStart = HeaderSize + i * RecordSize;
			var rampStart = recordStart + paramLength;
			var nameStart = rampStart + ColorRampBytes;

			effects[i] = new ParticleEffect
			{
				Index = i,
				Name = ReadFixedString( bytes, nameStart, NameFieldLength ),
				ColorRamp = ReadColorRamp( bytes, rampStart ),
				Parameters = bytes[recordStart..rampStart],
			};
		}

		Effects = effects;
		ReadSharedTableAndGlobals( bytes, (int)recordsEnd );
	}

	// The second table + the two trailing globals, as the loader reads them after the effect records.
	// Parsed defensively: a file without them (e.g. a minimal synthetic one) just leaves the fields empty.
	private void ReadSharedTableAndGlobals( byte[] bytes, int offset )
	{
		if ( offset + 8 > bytes.Length )
		{
			TrailingData = bytes[offset..];
			return;
		}

		var count2 = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( offset, 4 ) );
		SharedRecordSize = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( offset + 4, 4 ) );
		var tableStart = offset + 8;
		var tableEnd = (long)tableStart + (long)count2 * SharedRecordSize;

		if ( count2 < 0 || SharedRecordSize <= 0 || tableEnd > bytes.Length )
		{
			// Not the expected shared-table shape — keep the remainder raw rather than mis-parse.
			SharedRecordSize = 0;
			TrailingData = bytes[offset..];
			return;
		}

		var shared = new byte[count2][];
		for ( var i = 0; i < count2; i++ )
		{
			var s = tableStart + i * SharedRecordSize;
			shared[i] = bytes[s..(s + SharedRecordSize)];
		}
		SharedRecords = shared;

		var globals = (int)tableEnd;
		if ( globals + 8 <= bytes.Length )
		{
			ParticleDensity = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( globals, 4 ) );
			TotalParticles = (int)BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( globals + 4, 4 ) );
			TrailingData = bytes[(globals + 8)..];
		}
		else
		{
			TrailingData = bytes[globals..];
		}
	}

	/// <summary>
	/// Reads the 16-stop colour ramp at <paramref name="start"/>. Each stop is a little-endian D3DCOLOR
	/// word (<c>0xAARRGGBB</c>). Returns empty if it would run past the buffer.
	/// </summary>
	private static IReadOnlyList<ParticleColor> ReadColorRamp( byte[] bytes, int start )
	{
		if ( start + ColorRampBytes > bytes.Length )
			return Array.Empty<ParticleColor>();

		var ramp = new ParticleColor[ColorRampStops];
		for ( var i = 0; i < ColorRampStops; i++ )
		{
			var argb = BinaryPrimitives.ReadUInt32LittleEndian( bytes.AsSpan( start + i * 4, 4 ) );
			ramp[i] = new ParticleColor(
				R: (byte)(argb >> 16),
				G: (byte)(argb >> 8),
				B: (byte)argb,
				A: (byte)(argb >> 24) );
		}

		return ramp;
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
