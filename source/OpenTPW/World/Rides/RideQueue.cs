namespace OpenTPW;

/// <summary>
/// A ride's queue + boarding info for peeps: the walk waypoints leading from the queue's outer end up
/// to the entrance, the world point where riders reappear at the exit, how long a ride lasts, and how
/// many peeps it can hold at once. <see cref="Riders"/> is the live occupancy (peeps board only while
/// it is below <see cref="Capacity"/>, so a queue forms at the entrance when the ride is full).
/// </summary>
public sealed class RideQueue
{
	public IReadOnlyList<Vector3> Waypoints { get; }
	public Vector3 ExitPoint { get; }
	public float RideDuration { get; }
	public int Capacity { get; }

	/// <summary>How many peeps are currently on the ride.</summary>
	public int Riders { get; set; }

	public RideQueue( IReadOnlyList<Vector3> waypoints, Vector3 exitPoint, float rideDuration, int capacity )
	{
		Waypoints = waypoints;
		ExitPoint = exitPoint;
		RideDuration = rideDuration;
		Capacity = capacity;
	}

	public bool HasFreeSlot => Riders < Capacity;
}
