namespace OpenTPW;

/// <summary>
/// The <b>visual</b> day/night clock (T-056). The gameplay calendar (<see cref="GameClock"/>) is heavily
/// compressed — a whole in-game day passes in a fraction of a real second — so it can't drive a watchable
/// day/night cycle. This is a separate, slow cosmetic cycle keyed off real elapsed time: one full visual day
/// every <see cref="VisualDaySeconds"/>. The park opens at midday (phase 0.5). Pure ramp maths live in
/// <see cref="DayNightTint"/>; the overlay + HUD read <see cref="Phase01"/>.
/// </summary>
public static class DayNightCycle
{
	/// <summary>Real seconds for one full visual day/night cycle.</summary>
	public const float VisualDaySeconds = 120f;

	// The real time the cycle was anchored (level load), so the park reliably opens at midday.
	private static float origin;

	/// <summary>Anchor the cycle to "now" so it starts at midday (call once when a park loads).</summary>
	public static void Reset( float now ) => origin = now;

	/// <summary>The current visual time of day, 0..1 (0 = midnight, 0.5 = noon).</summary>
	public static float Phase01
	{
		get
		{
			float f = (Time.Now - origin) / VisualDaySeconds + 0.5f; // +0.5 → opens at noon
			f -= MathF.Floor( f );
			return f;
		}
	}
}
