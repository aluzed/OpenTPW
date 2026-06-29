using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// Screen-space day/night wash (T-056): tints the park toward <see cref="NightColor"/> as the in-game clock
/// runs into the night and fades to nothing at midday. A stand-in for the original's fog/ambient ramp on an
/// unlit renderer. The ramp maths are the pure <see cref="DayNightTint"/>; drawn under the rest of the HUD so
/// the stats text stays readable, and composed beneath the weather wash. Tint textures are cached per alpha
/// bucket so nothing is allocated per frame (cf. T-026/T-027).
/// </summary>
internal sealed class DayNightOverlay : HudPanel
{
	/// <summary>The colour the park drifts toward at night — set from the level's <c>ThemeEngine.AmbientLightLevel</c>
	/// (a dim blue-grey by default). Setting it resets the cached tint textures.</summary>
	public static (byte R, byte G, byte B) NightColor
	{
		get => nightColor;
		set { nightColor = value; Array.Clear( byAlpha ); }
	}
	private static (byte R, byte G, byte B) nightColor = (12, 18, 45);

	private const float MaxNightAlpha = 140f; // darkest deep-night wash
	private const int Buckets = 32;           // alpha quantised into this many cached textures
	private static readonly Texture?[] byAlpha = new Texture?[Buckets + 1];

	private static Texture TintFor( byte alpha )
	{
		int b = alpha * Buckets / 255;
		return byAlpha[b] ??= Solid( nightColor.R, nightColor.G, nightColor.B, (byte)(b * 255 / Buckets) );
	}

	protected override void OnRender()
	{
		float night = DayNightTint.NightAmount( DayNightCycle.Phase01 );
		byte alpha = (byte)Math.Clamp( night * MaxNightAlpha, 0f, 255f );
		if ( alpha <= 2 )
			return; // broad daylight — nothing to draw

		var mat = Material.UI;
		mat.Set( "Color", TintFor( alpha ) );
		Graphics.Quad( new Rectangle( 0f, 0f, 1280f, 720f ), mat );
	}
}
