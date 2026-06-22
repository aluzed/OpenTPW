using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// A loaded peep/staff sprite from <c>esprites.wad</c>: every <see cref="TpcFile"/> frame as its own
/// textured billboard model, plus the directional walk cycles read from the <c>.ESP</c> animation table.
/// Cached by path and fail-safe (returns null if it can't load, so callers fall back to flat billboards).
/// </summary>
internal sealed class SpriteSheet
{
	/// <summary>One directional animation: frames [<see cref="Start"/>, Start+Count).</summary>
	public readonly record struct Anim( int Start, int Count );

	private readonly Model[] frames;
	private readonly Anim[] anims;

	/// <summary>The tallest frame's pixel height — the reference a caller scales by (one pixels-to-world
	/// factor for the whole sheet), so different-sized frames keep correct relative size with no jitter.</summary>
	public float RefHeight { get; }

	private SpriteSheet( Model[] frames, Anim[] anims, float refHeight )
	{
		this.frames = frames;
		this.anims = anims;
		RefHeight = refHeight;
	}

	public int FrameCount => frames.Length;
	public IReadOnlyList<Anim> Anims => anims;
	public Model FrameModel( int i ) => frames[Math.Clamp( i, 0, frames.Length - 1 )];

	private static readonly Dictionary<string, SpriteSheet?> cache = new();

	/// <summary>Loads (and caches) the sprite at <paramref name="dir"/>/<paramref name="name"/> (e.g.
	/// <c>esprites/Generic/Kids</c>, <c>SPR_KI</c>); null if it can't be decoded.</summary>
	public static SpriteSheet? Load( string dir, string name )
	{
		string key = $"{dir}/{name}";
		if ( cache.TryGetValue( key, out var cached ) )
			return cached;
		var sheet = Build( dir, name, key );
		cache[key] = sheet;
		return sheet;
	}

	private static SpriteSheet? Build( string dir, string name, string key )
	{
		try
		{
			using var s = FileSystem.OpenRead( $"{dir}/{name}.TPC" );
			if ( s == null )
				return null;

			var tpc = new TpcFile( s );
			var frames = new Model[tpc.FrameCount];
			float refHeight = 1f;
			for ( int i = 0; i < tpc.FrameCount; i++ )
				refHeight = MathF.Max( refHeight, tpc.Frames[i].Height );

			for ( int i = 0; i < tpc.FrameCount; i++ )
			{
				var f = tpc.Frames[i];
				if ( f.Width == 0 || f.Height == 0 || f.Rgba.Length == 0 )
				{
					frames[i] = Billboard.Make( 0, 0, 0 );
					continue;
				}
				// Hotspot-anchored, pixel-unit quad: feet planted + no per-frame width jitter (T-035).
				var tex = new Texture( f.Rgba, f.Width, f.Height, TextureFlags.PointFilter );
				frames[i] = Billboard.MakeSprite( tex, f.Width, f.Height, f.HotspotX, f.HotspotY );
			}

			var anims = LoadAnims( dir, name, frames.Length );
			Log.Info( $"[sprite] {key}: {frames.Length} frames, {anims.Length} directional anims, refH {refHeight:0}" );
			return new SpriteSheet( frames, anims, refHeight );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[sprite] {key} failed: {e.Message}" );
			return null;
		}
	}

	// The .ESP holds (after the 12-byte magic + 256-byte name field + a 2-byte header) a 16-entry table
	// of [u16 startFrame][u8 frameCount][u8 flag]; the non-empty entries are the directional walk cycles.
	private static Anim[] LoadAnims( string dir, string name, int frameCount )
	{
		try
		{
			using var s = FileSystem.OpenRead( $"{dir}/{name}.ESP" );
			if ( s == null )
				return System.Array.Empty<Anim>();
			using var ms = new MemoryStream();
			s.CopyTo( ms );
			var d = ms.ToArray();

			int o = 12 + 256 + 2;
			var list = new List<Anim>();
			for ( int i = 0; i < 16 && o + 4 <= d.Length; i++, o += 4 )
			{
				int start = BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( o ) );
				int count = d[o + 2];
				if ( count > 0 && start + count <= frameCount )
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
