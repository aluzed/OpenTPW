namespace OpenTPW;

/// <summary>
/// The park's in-game calendar (T-053): real elapsed seconds → in-game day / month / year at a fixed cadence,
/// the single time source the progression systems key off (finances settle, challenges count down in days,
/// seasons roll). One <see cref="Current"/> per level. The cadence matches the economy's old month length
/// (<see cref="SecondsPerMonth"/> = 8 s), with a month divided into <see cref="DaysPerMonth"/> days.
/// <para>Subscribe to <see cref="OnNewDay"/> / <see cref="OnNewMonth"/> for boundary work; both fire once per
/// crossed boundary even if a big <c>dt</c> skips several at once.</para>
/// </summary>
public sealed class GameClock
{
	/// <summary>The active level's clock (null before a level loads).</summary>
	public static GameClock? Current { get; set; }

	public const float SecondsPerMonth = 8f; // matches the economy's month (kept so loan balancing is unchanged)
	public const int DaysPerMonth = 30;
	public const int MonthsPerYear = 12;
	private const float SecondsPerDay = SecondsPerMonth / DaysPerMonth;

	private float seconds;     // total elapsed in-game seconds
	private int firedDays;     // in-game days already announced via OnNewDay
	private int firedMonths;   // in-game months already announced via OnNewMonth

	/// <summary>Total elapsed in-game seconds.</summary>
	public float ElapsedSeconds => seconds;
	/// <summary>Total whole in-game days elapsed since the level started.</summary>
	public int TotalDays => (int)(seconds / SecondsPerDay);
	/// <summary>Total whole in-game months elapsed.</summary>
	public int TotalMonths => TotalDays / DaysPerMonth;

	/// <summary>Day of the month, 1..<see cref="DaysPerMonth"/>.</summary>
	public int Day => TotalDays % DaysPerMonth + 1;
	/// <summary>Month of the year, 1..<see cref="MonthsPerYear"/>.</summary>
	public int Month => TotalMonths % MonthsPerYear + 1;
	/// <summary>Year, starting at 1.</summary>
	public int Year => TotalMonths / MonthsPerYear + 1;

	/// <summary>Fired once for each in-game day crossed (challenges / seasons count down on this).</summary>
	public event Action? OnNewDay;
	/// <summary>Fired once for each in-game month crossed (finances settle on this).</summary>
	public event Action? OnNewMonth;

	/// <summary>Advance the clock by <paramref name="dt"/> real seconds, firing day/month boundaries crossed.</summary>
	public void Tick( float dt )
	{
		if ( dt <= 0f )
			return;
		seconds += dt;

		int days = TotalDays;
		while ( firedDays < days ) { firedDays++; OnNewDay?.Invoke(); }

		int months = TotalMonths;
		while ( firedMonths < months ) { firedMonths++; OnNewMonth?.Invoke(); }
	}
}
