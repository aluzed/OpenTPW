using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// A screen-space weather overlay (T-056): when <see cref="WeatherSim.Current"/> reports rain/snow it draws a
/// colour tint plus animated falling precipitation over the whole park, and a brief white flash on a lightning
/// storm. Purely cosmetic — it reads the sim and renders, holding no state of its own. Drawn under the rest of
/// the HUD so the stats text stays readable. The drop stream is deterministic (a per-index hash + the clock),
/// so it needs no per-frame randomness.
/// </summary>
internal sealed class WeatherOverlay : HudPanel
{
	private const int DropCount = 140; // particles spread across the 1280×720 base space

	private static Texture? rainTint, snowTint, rainDrop, snowFlake, flash;
	private static Texture RainTint => rainTint ??= Solid( 40, 60, 90, 70 );    // cool blue-grey wash
	private static Texture SnowTint => snowTint ??= Solid( 200, 210, 225, 55 ); // pale overcast wash
	private static Texture RainDrop => rainDrop ??= Solid( 170, 195, 225, 150 );
	private static Texture SnowFlake => snowFlake ??= Solid( 245, 248, 255, 210 );
	private static Texture Flash => flash ??= Solid( 255, 255, 255, 255 );

	// A cheap, stable hash → [0,1) per drop index (no per-frame Random; the stream is reproducible each frame).
	private static float Hash( int n )
	{
		uint h = (uint)n * 2654435761u;
		h ^= h >> 15;
		return (h & 0xFFFFFF) / (float)0x1000000;
	}

	protected override void OnRender()
	{
		if ( WeatherSim.Current is not { } sim || sim.State.IsClear )
			return;

		bool snow = sim.State.Kind == WeatherKind.Snow;
		var mat = Material.UI;

		// Whole-screen tint wash.
		mat.Set( "Color", snow ? SnowTint : RainTint );
		Graphics.Quad( new Rectangle( 0f, 0f, 1280f, 720f ), mat );

		// Falling precipitation. Each drop wraps continuously down the screen; rain falls fast and straight as
		// thin streaks, snow drifts slower with a gentle sideways sway.
		float t = Time.Now;
		var dropTex = snow ? SnowFlake : RainDrop;
		mat.Set( "Color", dropTex );
		float fallSpeed = snow ? 90f : 900f;
		for ( int i = 0; i < DropCount; i++ )
		{
			float baseX = Hash( i ) * 1280f;
			float phase = Hash( i + 7919 );                 // a different stream for vertical offset
			float y = 720f - ((t * fallSpeed + phase * 720f) % 720f);
			if ( snow )
			{
				float x = baseX + MathF.Sin( t * 1.5f + phase * 6.283f ) * 14f; // drift
				Graphics.Quad( new Rectangle( x, y, 4f, 4f ), mat );
			}
			else
			{
				Graphics.Quad( new Rectangle( baseX, y, 2f, 18f ), mat ); // vertical streak
			}
		}

		// Lightning: a short bright flash a couple of times per change interval. Derive the flash from the
		// clock so it doesn't need its own timer; ~0.08 s on, then dark for ~3 s.
		if ( sim.State.Lightning )
		{
			float cycle = t % 3.2f;
			if ( cycle < 0.08f )
			{
				mat.Set( "Color", Flash );
				Graphics.Quad( new Rectangle( 0f, 0f, 1280f, 720f ), mat );
			}
		}
	}
}
