namespace OpenTPW;

/// <summary>
/// Loads a real peep sprite from <c>esprites.wad</c> (a decoded <see cref="TpcFile"/>) and exposes it as
/// a camera-facing billboard model. Lazy, shared, and fail-safe: if the sprite can't be loaded the peep
/// falls back to the flat-colour billboard. Picking the authentic per-direction / walk-cycle frame is a
/// follow-up; for now a single standing frame is used for the whole crowd.
/// </summary>
internal static class PeepSprite
{
	private const string SpritePath = "esprites/Generic/Kids/SPR_KI.TPC";

	private static bool tried;
	private static Model? model;

	/// <summary>The sprite billboard model, or null if it couldn't be loaded.</summary>
	public static Model? Model { get { Ensure(); return model; } }

	/// <summary>Width/height of the chosen frame, for sizing the billboard (1 if unloaded).</summary>
	public static float Aspect { get; private set; } = 1f;

	private static void Ensure()
	{
		if ( tried )
			return;
		tried = true;

		try
		{
			using var s = FileSystem.OpenRead( SpritePath );
			if ( s == null )
				return;

			var tpc = new TpcFile( s );
			// Heuristic standing pose: the tallest non-trivial frame reads as an upright peep.
			var frame = tpc.Frames
				.Where( f => f.Width > 0 && f.Height > 0 && f.Rgba.Length > 0 )
				.OrderByDescending( f => f.Height )
				.FirstOrDefault();
			if ( frame == null )
				return;

			var tex = new Texture( frame.Rgba, frame.Width, frame.Height, TextureFlags.PointFilter );
			model = Billboard.Make( tex );
			Aspect = (float)frame.Width / frame.Height;
			Log.Info( $"[peep] loaded sprite {SpritePath}: {tpc.FrameCount} frames, using {frame.Width}x{frame.Height}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[peep] sprite load failed ({SpritePath}): {e.Message}" );
		}
	}
}
