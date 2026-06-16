namespace OpenTPW;

/// <summary>
/// A tiny channel between the (synchronous, main-thread) level load and the loading screen. Load
/// code calls <see cref="Report"/> at checkpoints to update the status line + progress fraction;
/// whoever owns the window (see <c>Game.Run</c>) wires <see cref="OnReport"/> to pump events and
/// re-present a loading frame, so the user sees a progress bar advancing ("Loading island: Jungle…
/// (20/25)") instead of a frozen screen. See T-030.
/// </summary>
internal static class LoadProgress
{
	/// <summary>The current load status, shown on the loading screen.</summary>
	public static string Status { get; private set; } = "";

	/// <summary>Overall load progress in [0,1], drawn as the loading bar fill.</summary>
	public static float Progress { get; private set; }

	/// <summary>Presents a loading frame (pump events + draw). Set by the window owner during a load.</summary>
	public static Action? OnReport;

	// The fraction range of the phase currently being reported via ReportSub (e.g. one island).
	private static float _phaseStart;
	private static float _phaseEnd;

	/// <summary>Sets the status + absolute progress and presents a loading frame (keeps the window responsive).</summary>
	public static void Report( string status, float progress )
	{
		Status = status;
		Progress = Math.Clamp( progress, 0f, 1f );
		OnReport?.Invoke();
	}

	/// <summary>Marks a phase that spans <paramref name="start"/>..<paramref name="end"/> of the bar; sub-steps use <see cref="ReportSub"/>.</summary>
	public static void BeginPhase( float start, float end )
	{
		_phaseStart = start;
		_phaseEnd = end;
	}

	/// <summary>Reports a sub-step <paramref name="t"/> in [0,1] within the current phase's range.</summary>
	public static void ReportSub( string status, float t )
	{
		Report( status, _phaseStart + (_phaseEnd - _phaseStart) * Math.Clamp( t, 0f, 1f ) );
	}

	/// <summary>Clears the status and detaches the presenter once loading is done.</summary>
	public static void Done()
	{
		Status = "";
		Progress = 0f;
		OnReport = null;
	}
}
