namespace OpenTPW;

/// <summary>
/// The park-wide ride research queue (T-044): the park researches <b>one ride at a time</b> — the researchers
/// concentrate on the queue head until its next level unlocks, then the next queued ride starts. A ride joins
/// when the player starts its research and leaves when it completes (or the ride is sold). This replaces every
/// researching ride advancing in parallel (which let N rides each soak up the whole research team at once).
/// </summary>
public static class ResearchQueue
{
	private static readonly FifoSet<Ride> set = new();

	/// <summary>The ride currently being researched (the queue head), or null when idle.</summary>
	public static Ride? Active => set.Active;

	/// <summary>How many rides are queued (the active one plus those waiting).</summary>
	public static int Count => set.Count;

	/// <summary>Add a ride to the back of the queue (no-op if already queued); true if newly added.</summary>
	public static bool Enqueue( Ride ride ) => set.Add( ride );

	/// <summary>Remove a ride from the queue (on completion or sell); true if it was queued.</summary>
	public static bool Dequeue( Ride ride ) => set.Remove( ride );

	/// <summary>Queue position: 0 = actively researching, ≥1 = that many rides ahead, -1 = not queued.</summary>
	public static int Position( Ride ride ) => set.IndexOf( ride );

	/// <summary>Clear the queue (a new level loads, or tests).</summary>
	public static void Reset() => set.Clear();
}
