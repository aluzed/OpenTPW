using System.Collections.Generic;

namespace OpenTPW;

/// <summary>
/// The ride-script "head" subsystem: a per-VM table of head slots (the original VM struct's <c>+0x30</c>
/// array, count <c>+0x4c</c>). Ride scripts mount decorative "head" objects (e.g. trophy/totem heads) into
/// the ride's head-node display positions. Reversed from the executor cases op_56 (ADDHEAD) / op_57
/// (DELHEAD): <c>ADDHEAD value</c> drops <paramref name="value"/> into a <b>random free</b> slot and shows
/// the head; <c>DELHEAD value</c> clears every slot holding that value and hides those heads.
///
/// <para>This models the slot table (pure VM bookkeeping, like the limbo/walk tables) so the add/remove
/// behaviour is correct and unit-testable. Two things need ride geometry we don't decode yet, so they're
/// deferred (documented): the table's real <b>capacity</b> is the ride's head-node count (the original
/// probes type-<c>0x80</c> objects at spawn), and the <b>visual</b> head-mesh attachment at a head node.
/// We use a fixed capacity stand-in. See docs/tickets/T-007.</para>
/// </summary>
public partial class RideVM
{
	/// <summary>Stand-in head-slot count; the real value is the ride's head-node count (not decoded).</summary>
	public const int DefaultHeadCapacity = 8;

	private int[]? headSlots; // 0 = empty slot (the original's sentinel)
	private int[] HeadTable => headSlots ??= new int[DefaultHeadCapacity];

	/// <summary>The head slots (0 = empty), read-only.</summary>
	public IReadOnlyList<int> HeadSlots => HeadTable;

	/// <summary>How many head slots are currently occupied.</summary>
	public int HeadCount
	{
		get
		{
			var n = 0;
			foreach ( var h in HeadTable )
				if ( h != 0 )
					n++;
			return n;
		}
	}

	/// <summary>ADDHEAD: put <paramref name="value"/> in a random free slot; false if the table is full.
	/// (Slot choice uses the injectable <see cref="RandomIndex"/>, as the original picks a random slot.)</summary>
	public bool AddHead( int value )
	{
		var heads = HeadTable;
		var free = new List<int>();
		for ( var i = 0; i < heads.Length; i++ )
			if ( heads[i] == 0 )
				free.Add( i );

		if ( free.Count == 0 )
			return false;

		heads[free[RandomIndex( free.Count )]] = value;
		return true;
	}

	/// <summary>DELHEAD: clear every slot holding <paramref name="value"/>; returns how many were removed.</summary>
	public int RemoveHead( int value )
	{
		var heads = HeadTable;
		var removed = 0;
		for ( var i = 0; i < heads.Length; i++ )
		{
			if ( heads[i] != value )
				continue;
			heads[i] = 0;
			removed++;
		}
		return removed;
	}
}
