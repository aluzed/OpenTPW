namespace OpenTPW;

/// <summary>
/// A ride's queue + boarding info for peeps: the walk waypoints leading from the queue's outer end up
/// to the entrance, the world point where riders reappear at the exit, how long a ride lasts, and how
/// many peeps it can hold at once. <see cref="Riders"/> is the live occupancy (peeps board only while
/// it is below <see cref="Capacity"/>, so a queue forms at the entrance when the ride is full).
/// </summary>
public sealed class RideQueue
{
	public Ride Ride { get; }
	public IReadOnlyList<Vector3> Waypoints { get; }
	public Vector3 ExitPoint { get; }
	public float RideDuration { get; }
	public int Capacity { get; }

	private int riders;

	public RideQueue( Ride ride, IReadOnlyList<Vector3> waypoints, Vector3 exitPoint, float rideDuration, int capacity )
	{
		Ride = ride;
		Waypoints = waypoints;
		ExitPoint = exitPoint;
		RideDuration = rideDuration;
		Capacity = capacity;
	}

	/// <summary>How many peeps are currently on the ride.</summary>
	public int Riders => riders;
	public bool HasFreeSlot => riders < Capacity;

	/// <summary>A peep boards: take a slot, starting the ride's boarding cycle on the first rider.</summary>
	public void Board()
	{
		bool wasEmpty = riders == 0;
		riders++;
		if ( wasEmpty )
			Ride?.SetActive( true ); // edge 0 -> 1: kick off the load/start/run cycle
	}

	/// <summary>A peep's ride finishes: free its slot, idling the ride once the last rider leaves.</summary>
	public void Leave()
	{
		riders = Math.Max( 0, riders - 1 );
		if ( riders == 0 )
			Ride?.SetActive( false ); // edge 1 -> 0: run the end/unload cycle, then idle
	}
}
