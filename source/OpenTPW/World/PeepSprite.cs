using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// Loads a real peep sprite from <c>esprites.wad</c>: decodes every frame of the <see cref="TpcFile"/>
/// into its own textured billboard model, and reads the <c>.ESP</c> animation table (8 directional walk
/// cycles). Lazy, shared, fail-safe — if it can't load, <see cref="Loaded"/> is false and peeps fall
/// back to flat-colour billboards.
/// </summary>
internal static class PeepSprite
{
	private const string TpcPath = "esprites/Generic/Kids/SPR_KI.TPC";
	private const string EspPath = "esprites/Generic/Kids/SPR_KI.ESP";

	/// <summary>One directional animation: a contiguous run of frames [<see cref="Start"/>, Start+Count).</summary>
	public readonly record struct Anim( int Start, int Count );

	private static bool tried;
	private static Model[] frames = System.Array.Empty<Model>();
	private static float[] aspects = System.Array.Empty<float>();
	private static Anim[] anims = System.Array.Empty<Anim>();

	public static bool Loaded { get { Ensure(); return frames.Length > 0; } }
	public static int FrameCount => frames.Length;
	public static Model FrameModel( int i ) => frames[Math.Clamp( i, 0, frames.Length - 1 )];
	public static float FrameAspect( int i ) => aspects[Math.Clamp( i, 0, aspects.Length - 1 )];

	/// <summary>The directional walk animations (8 when the sprite ships a full set).</summary>
	public static IReadOnlyList<Anim> Anims { get { Ensure(); return anims; } }

	private static void Ensure()
	{
		if ( tried )
			return;
		tried = true;

		try
		{
			using var s = FileSystem.OpenRead( TpcPath );
			if ( s == null )
				return;

			var tpc = new TpcFile( s );
			frames = new Model[tpc.FrameCount];
			aspects = new float[tpc.FrameCount];
			for ( int i = 0; i < tpc.FrameCount; i++ )
			{
				var f = tpc.Frames[i];
				if ( f.Width == 0 || f.Height == 0 || f.Rgba.Length == 0 )
				{
					frames[i] = Billboard.Make( 0, 0, 0 ); // placeholder for an empty frame
					aspects[i] = 1f;
					continue;
				}
				frames[i] = Billboard.Make( new Texture( f.Rgba, f.Width, f.Height, TextureFlags.PointFilter ) );
				aspects[i] = (float)f.Width / f.Height;
			}

			anims = LoadAnims();
			Log.Info( $"[peep] sprite {TpcPath}: {frames.Length} frames, {anims.Length} directional anims" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[peep] sprite load failed ({TpcPath}): {e.Message}" );
			frames = System.Array.Empty<Model>();
		}
	}

	// The .ESP holds (after a 12-byte magic + 256-byte name field, then a 2-byte header) a 16-entry
	// table of [u16 startFrame][u8 frameCount][u8 flag]; the non-empty entries are the walk cycles.
	private static Anim[] LoadAnims()
	{
		try
		{
			using var s = FileSystem.OpenRead( EspPath );
			if ( s == null )
				return System.Array.Empty<Anim>();
			using var ms = new MemoryStream();
			s.CopyTo( ms );
			var d = ms.ToArray();

			int o = 12 + 256 + 2; // magic + name field + 2-byte header
			var list = new List<Anim>();
			for ( int i = 0; i < 16 && o + 4 <= d.Length; i++, o += 4 )
			{
				int start = BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( o ) );
				int count = d[o + 2];
				if ( count > 0 && start + count <= frames.Length )
					list.Add( new Anim( start, count ) );
			}
			return list.ToArray();
		}
		catch
		{
			return System.Array.Empty<Anim>();
		}
	}
}
