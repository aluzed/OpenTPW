namespace OpenTPW;

/// <summary>
/// The day/night lighting ramp (T-056). The renderer is unlit, so instead of real lighting the park is washed
/// with a tint that strengthens toward night and fades to nothing at midday — a stand-in for the original's
/// fog/ambient ramp (<c>ThemeEngine.FogColour</c> / <c>AmbientLightLevel</c>). Pure: maps the clock's
/// <see cref="GameClock.DayFraction"/> to a 0..1 "night amount" and a short phase label; the overlay turns that
/// into a screen tint and the HUD shows the phase.
/// </summary>
public static class DayNightTint
{
	/// <summary>How "night" it is, 0..1: 1 at midnight, 0 at noon, smoothly ramping through dawn/dusk.
	/// <paramref name="dayFraction"/> is 0..1 (0 = midnight, 0.5 = noon).</summary>
	public static float NightAmount( float dayFraction )
	{
		// cos peaks (=1) at midnight (f=0/1) and troughs (=-1) at noon (f=0.5); remap to [0,1].
		float f = dayFraction - MathF.Floor( dayFraction ); // wrap into [0,1)
		return (1f + MathF.Cos( f * MathF.Tau )) * 0.5f;
	}

	/// <summary>A short label for the current part of the day, for the HUD.</summary>
	public static string Phase( float dayFraction )
	{
		float f = dayFraction - MathF.Floor( dayFraction );
		return f switch
		{
			< 0.22f or >= 0.92f => "NIGHT",
			< 0.32f => "DAWN",
			< 0.70f => "DAY",
			< 0.80f => "DUSK",
			_ => "NIGHT",
		};
	}
}
