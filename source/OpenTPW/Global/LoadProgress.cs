namespace OpenTPW;

/// <summary>
/// A tiny channel between the (synchronous, main-thread) level load and the loading screen. Load
/// code calls <see cref="Report"/> at major checkpoints to update the status line; whoever owns the
/// window (see <c>Game.Run</c>) wires <see cref="OnReport"/> to pump events and re-present a loading
/// frame, so the user sees progress ("Loading island: Jungle…") instead of a frozen screen. See T-030.
/// </summary>
internal static class LoadProgress
{
	/// <summary>The current load status, shown at the bottom of the loading screen.</summary>
	public static string Status { get; private set; } = "";

	/// <summary>Presents a loading frame (pump events + draw). Set by the window owner during a load.</summary>
	public static Action? OnReport;

	/// <summary>Sets the status and presents a loading frame so the window updates and stays responsive.</summary>
	public static void Report( string status )
	{
		Status = status;
		OnReport?.Invoke();
	}

	/// <summary>Clears the status and detaches the presenter once loading is done.</summary>
	public static void Done()
	{
		Status = "";
		OnReport = null;
	}
}
