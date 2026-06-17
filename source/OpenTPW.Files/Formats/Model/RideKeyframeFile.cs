using System.Numerics;

// The OpenTPW namespace has a custom Vector3 (Common/Utils) registered as a global using alias, so the
// bare name resolves to it. This is geometry math, so Vector3 is fully qualified to System.Numerics
// below. (Quaternion has no custom shadow and resolves to System.Numerics.)
using SVector3 = System.Numerics.Vector3;

namespace OpenTPW;

/// <summary>
/// Parses a Theme Park World ride <b>animation keyframe</b> file — a thin sibling <c>.md2</c> such as
/// <c>monkeym1.md2</c> (see <c>docs/08-ghidra-animation.md</c>, T-033). These files are <i>not</i>
/// standalone meshes (the engine's <see cref="ModelFile"/> rejects them): they share the base model's
/// header but carry time-indexed animation <b>tracks</b> keyed to the base model's surfaces.
///
/// <para>Layout (reverse-engineered from <c>FUN_0046d6d0</c> / <c>FUN_00470b60</c> /
/// <c>FUN_00471860</c>): the dword at <c>0x98</c> points to a per-surface relink trailer — a count at
/// <c>+0x12</c> and an array of <c>0x40</c>-byte records at <c>+0x2c</c>. Each record names a base
/// surface (<c>+0x10</c>), a flags word (<c>+0x04</c>) selecting which tracks run, and file offsets to
/// its translation (<c>+0x18</c>), rotation (<c>+0x1c</c>) and scale (<c>+0x20</c>) tracks. A track is
/// a list of keyframes; each key starts with a <c>0xFFFF</c>-tagged dword whose low <c>u16</c> is the
/// keyframe time, followed by the value (a 4-float <c>(w,x,y,z)</c> quaternion for rotation, a 3-float
/// vec3 for translation/scale). Vertex-morph tracks (flag <c>0x1000</c>) are not yet decoded here.</para>
/// </summary>
public sealed class RideKeyframeFile
{
	private const uint Magic = 0x1CD15D46;

	// Flags in a surface record (record + 0x04) selecting which tracks are present.
	public const uint FlagVertexMorph = 0x1000;
	public const uint FlagRotation = 0x8;
	public const uint FlagScale = 0x80;
	public const uint FlagTranslation = 0x200 | 0x1;

	/// <summary>Animation tracks for one base-model surface (mesh).</summary>
	public sealed class SurfaceAnim
	{
		/// <summary>Index into the base model's surface list (== <see cref="ModelFile.Meshes"/> index for surfaces below the mesh count).</summary>
		public int SurfaceIndex { get; init; }
		public uint Flags { get; init; }

		public List<(float Time, Quaternion Value)> Rotation { get; } = new();
		public List<(float Time, SVector3 Value)> Translation { get; } = new();
		public List<(float Time, SVector3 Value)> Scale { get; } = new();

		public bool HasAnimation => Rotation.Count > 0 || Translation.Count > 0 || Scale.Count > 0;
	}

	/// <summary>The per-surface animation tracks found in the file (only surfaces that actually animate).</summary>
	public IReadOnlyList<SurfaceAnim> Surfaces => surfaces;
	private readonly List<SurfaceAnim> surfaces = new();

	/// <summary>The longest track's last keyframe time — the animation's loop length (0 if none).</summary>
	public float Duration { get; private set; }

	public RideKeyframeFile( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		Parse( ms.ToArray() );
	}

	public RideKeyframeFile( byte[] data ) => Parse( data );

	private void Parse( byte[] d )
	{
		// Bounds-checked little-endian readers — a malformed/short file yields no animation, never throws.
		ushort U16( int o ) => o + 2 <= d.Length ? (ushort)(d[o] | (d[o + 1] << 8)) : (ushort)0;
		uint U32( int o ) => o + 4 <= d.Length ? (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24)) : 0u;
		float F32( int o ) => o + 4 <= d.Length ? BitConverter.ToSingle( d, o ) : 0f;

		if ( d.Length < 0x9c || U32( 0 ) != Magic )
			return;

		// The base model has a zero anim pointer (it is the static bind pose); only keyframe files
		// carry one. It points to the per-surface relink trailer.
		int trailer = (int)U32( 0x98 );
		if ( trailer <= 0 || trailer + 0x30 > d.Length )
			return;

		int count = U16( trailer + 0x12 );
		int records = (int)U32( trailer + 0x2c );
		if ( records <= 0 || count <= 0 || count > 4096 )
			return;

		for ( int i = 0; i < count; i++ )
		{
			int rec = records + i * 0x40;
			if ( rec + 0x40 > d.Length )
				break;

			var anim = new SurfaceAnim { SurfaceIndex = U16( rec + 0x10 ), Flags = U32( rec + 0x04 ) };

			ReadTrack( d, (int)U32( rec + 0x18 ), 3, U32, F32, ( t, v ) => anim.Translation.Add( (t, new SVector3( v[0], v[1], v[2] )) ) );
			// Rotation quaternion is stored (w, x, y, z); System.Numerics.Quaternion is (x, y, z, w).
			ReadTrack( d, (int)U32( rec + 0x1c ), 4, U32, F32, ( t, v ) => anim.Rotation.Add( (t, Quaternion.Normalize( new Quaternion( v[1], v[2], v[3], v[0] ) )) ) );
			ReadTrack( d, (int)U32( rec + 0x20 ), 3, U32, F32, ( t, v ) => anim.Scale.Add( (t, new SVector3( v[0], v[1], v[2] )) ) );

			if ( anim.HasAnimation )
			{
				surfaces.Add( anim );
				foreach ( var k in anim.Rotation ) Duration = MathF.Max( Duration, k.Time );
				foreach ( var k in anim.Translation ) Duration = MathF.Max( Duration, k.Time );
				foreach ( var k in anim.Scale ) Duration = MathF.Max( Duration, k.Time );
			}
		}
	}

	// Reads keyframes starting at <paramref name="off"/>: each key is a 0xFFFF-tagged time dword
	// (low u16 = time) followed by <paramref name="floats"/> float components. Keys run until the
	// 0xFFFF sentinel is absent or the time stops increasing (the next track begins / data ends).
	private static void ReadTrack( byte[] d, int off, int floats, Func<int, uint> U32, Func<int, float> F32, Action<float, float[]> emit )
	{
		if ( off <= 0 )
			return;

		int stride = 4 + floats * 4;
		float prev = -1f;
		for ( int o = off; o + stride <= d.Length; o += stride )
		{
			uint hdr = U32( o );
			if ( (hdr >> 16) != 0xFFFF )
				break;

			float t = hdr & 0xFFFF;
			if ( t <= prev )
				break;
			prev = t;

			var v = new float[floats];
			for ( int j = 0; j < floats; j++ )
				v[j] = F32( o + 4 + j * 4 );
			emit( t, v );
		}
	}

	/// <summary>Sample a rotation track at <paramref name="time"/> (slerp between bracketing keys; clamped to the track range).</summary>
	public static Quaternion SampleRotation( IReadOnlyList<(float Time, Quaternion Value)> keys, float time )
	{
		if ( keys.Count == 0 )
			return Quaternion.Identity;
		if ( time <= keys[0].Time )
			return keys[0].Value;
		if ( time >= keys[^1].Time )
			return keys[^1].Value;

		for ( int i = 0; i < keys.Count - 1; i++ )
		{
			if ( time <= keys[i + 1].Time )
			{
				float span = keys[i + 1].Time - keys[i].Time;
				float f = span > 0 ? (time - keys[i].Time) / span : 0f;
				return Quaternion.Slerp( keys[i].Value, keys[i + 1].Value, f );
			}
		}
		return keys[^1].Value;
	}

	/// <summary>Sample a vec3 track (translation/scale) at <paramref name="time"/> (lerp; <paramref name="fallback"/> if empty).</summary>
	public static SVector3 SampleVector( IReadOnlyList<(float Time, SVector3 Value)> keys, float time, SVector3 fallback )
	{
		if ( keys.Count == 0 )
			return fallback;
		if ( time <= keys[0].Time )
			return keys[0].Value;
		if ( time >= keys[^1].Time )
			return keys[^1].Value;

		for ( int i = 0; i < keys.Count - 1; i++ )
		{
			if ( time <= keys[i + 1].Time )
			{
				float span = keys[i + 1].Time - keys[i].Time;
				float f = span > 0 ? (time - keys[i].Time) / span : 0f;
				return SVector3.Lerp( keys[i].Value, keys[i + 1].Value, f );
			}
		}
		return keys[^1].Value;
	}
}
