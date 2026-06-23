using System.Collections.Generic;

namespace OpenTPW;

/// <summary>
/// The ride-script "walk" subsystem: a per-VM table of walk slots (the original VM struct's <c>+0x2c</c>
/// array, count <c>+0x7c</c> = config <c>WalkSize</c>) plus a "walk float" timer (<c>+0x80</c>/<c>+0x84</c>).
/// Ride scripts use it to move peeps between walk nodes — onto a ride and back off — over time. Reversed
/// from the executor cases op_76..op_81 + the helpers <c>FUN_00556f40</c> (add), <c>FUN_005571a0</c>
/// (release), <c>FUN_00557110</c> (retrieve), <c>FUN_00557160</c> (float).
///
/// <para>This models the slot <b>lifecycle + timing</b> (pure VM bookkeeping, like the limbo table) so a
/// script's WALKON/WALKOFF/WALKGET sequence behaves correctly and is unit-testable. The <b>visual</b> part
/// — actually gliding the peep along the walk-node world positions, with the facing direction the original
/// computes by <c>atan2</c> of node coords — needs the ride's walk-node geometry, which isn't decoded yet;
/// that's a follow-up. See docs/tickets/T-007.</para>
/// </summary>
public partial class RideVM
{
	/// <summary>A walk slot's lifecycle state (the original's per-entry state field; values RE'd:
	/// 1 = walking on, 3 = walking off, 4 = finished — retrieved by WALKGET).</summary>
	public enum WalkState
	{
		Free = 0,
		WalkingOn = 1,
		Arrived = 2,    // reached the ride (WalkingOn completed)
		Releasing = 3,  // WALKOFF: walking back off
		Done = 4,       // finished walking off — WALKGET hands the peep back to the world
	}

	/// <summary>One peep walking between two nodes over <see cref="StartTime"/>..<see cref="EndTime"/>.</summary>
	public sealed class WalkSlot
	{
		public WalkState State;
		public int PeepId;
		public int StartNode, EndNode;
		public int Type;          // op param_7: 4 = head node, else a normal walk node
		public int Extra5, Extra6, Extra8; // the other WALKON params (roles not fully RE'd)
		public int StartTime, EndTime;     // GameTime units
		public int Direction;     // 0..7 facing; 0 until walk-node geometry is decoded
	}

	// Real walk durations come from the node distance (the original: ftol(dist)*100, min 100ms). Without
	// walk-node geometry we use a fixed placeholder so the lifecycle still advances; documented above.
	public const int DefaultWalkDuration = 1000; // GameTime units (ms)
	private const int DefaultWalkCapacity = 32;  // when a script declares no WalkSize

	private WalkSlot[]? walkSlots;
	private WalkSlot[] Slots => walkSlots ??= BuildWalkTable();

	/// <summary>The walk slots (read-only view) — capacity is the script's <c>WalkSize</c> config.</summary>
	public IReadOnlyList<WalkSlot> WalkSlots => Slots;

	/// <summary>Live count of slots currently carrying a walking peep (not <see cref="WalkState.Free"/>).</summary>
	public int ActiveWalkCount
	{
		get
		{
			var n = 0;
			foreach ( var s in Slots )
				if ( s.State != WalkState.Free )
					n++;
			return n;
		}
	}

	// Walk-float timer (+0x80 start, +0x84 value, +0x88/+0x8a params). A per-VM single-slot timed value.
	public int WalkFloatValue { get; private set; }     // +0x84 (0 = inactive)
	public int WalkFloatStartTime { get; private set; } // +0x80
	public int WalkFloatExtra3 { get; private set; }
	public int WalkFloatExtra4 { get; private set; }

	private WalkSlot[] BuildWalkTable()
	{
		var cap = Config.WalkSize > 0 ? Config.WalkSize : DefaultWalkCapacity;
		var table = new WalkSlot[cap];
		for ( var i = 0; i < cap; i++ )
			table[i] = new WalkSlot();
		return table;
	}

	/// <summary>WALKON: put <paramref name="peep"/> in a free slot, walking <paramref name="startNode"/> →
	/// <paramref name="endNode"/>. Returns false if the table is full (the original logs and bails).</summary>
	public bool WalkAdd( int peep, int startNode, int endNode, int type, int extra5, int extra6, int extra8 )
	{
		foreach ( var s in Slots )
		{
			if ( s.State != WalkState.Free )
				continue;
			s.State = WalkState.WalkingOn;
			s.PeepId = peep;
			s.StartNode = startNode;
			s.EndNode = endNode;
			s.Type = type;
			s.Extra5 = extra5;
			s.Extra6 = extra6;
			s.Extra8 = extra8;
			s.StartTime = GameTime;
			s.EndTime = GameTime + DefaultWalkDuration;
			s.Direction = 0;
			return true;
		}
		Log.Warning( "WALKON: walk table full; could not add peep" );
		return false;
	}

	/// <summary>WALKOFF: start <paramref name="peep"/> walking back off (reverse path). No-op if not found.</summary>
	public bool WalkRelease( int peep )
	{
		foreach ( var s in Slots )
		{
			if ( s.State is not (WalkState.WalkingOn or WalkState.Arrived) || s.PeepId != peep )
				continue;
			(s.StartNode, s.EndNode) = (s.EndNode, s.StartNode); // walk back the way it came
			s.State = WalkState.Releasing;
			s.StartTime = GameTime;
			s.EndTime = GameTime + DefaultWalkDuration;
			return true;
		}
		Log.Warning( $"WALKOFF: tried to release peep {peep} that isn't walking" );
		return false;
	}

	/// <summary>WALKGET: hand back the first peep that finished walking off, freeing its slot; 0 if none.</summary>
	public int WalkRetrieve()
	{
		foreach ( var s in Slots )
		{
			if ( s.State != WalkState.Done )
				continue;
			var peep = s.PeepId;
			s.State = WalkState.Free;
			s.PeepId = 0;
			return peep;
		}
		return 0;
	}

	/// <summary>Per-tick progression: a walking-on peep that reaches its end time has Arrived; a releasing
	/// one becomes Done (retrievable by WALKGET). Called from <see cref="Update"/>.</summary>
	public void WalkAdvance()
	{
		if ( walkSlots == null )
			return;
		foreach ( var s in walkSlots )
		{
			if ( s.State == WalkState.WalkingOn && GameTime >= s.EndTime )
				s.State = WalkState.Arrived;
			else if ( s.State == WalkState.Releasing && GameTime >= s.EndTime )
				s.State = WalkState.Done;
		}
	}

	/// <summary>WALKST_FLOAT: start the walk-float timer with a value + two params.</summary>
	public void WalkFloatBegin( int value, int extra3, int extra4 )
	{
		WalkFloatStartTime = GameTime;
		WalkFloatValue = value;
		WalkFloatExtra3 = extra3;
		WalkFloatExtra4 = extra4;
	}

	/// <summary>WALKFLOATSTOP: finalise the walk-float timer (the original stamps a fixed value + elapsed).</summary>
	public void WalkFloatStop()
	{
		if ( WalkFloatValue != 0 )
			WalkFloatValue = 1000; // matches the original's +0x84 = 1000 finalise
	}
}
