using System;
using System.Collections.Generic;

namespace OpenTPW;

/// <summary>
/// The global VM registry that backs the cross-VM opcodes (<c>FINDSCRIPTRAND</c>, <c>GET/SETREMOTEVAR</c>).
/// The original keeps every script VM in a global linked list (<c>DAT_008791b0</c>) keyed by a per-VM
/// handle (struct <c>+0x08</c>); <c>FUN_0055a070(handle)</c> walks it to resolve a VM. We model it with a
/// static table of weak references (so finished VMs drop out without explicit cleanup) keyed by an
/// incrementing handle. Each <see cref="RideVM"/> joins on construction.
/// </summary>
public partial class RideVM
{
	/// <summary>This VM's registry handle (the original's <c>+0x08</c> id) — what cross-VM opcodes pass.</summary>
	public int Handle { get; private set; }

	/// <summary>
	/// Picks a random index in <c>[0, count)</c> — used by <c>FINDSCRIPTRAND</c>. Injectable so tests are
	/// deterministic (the original uses its own LCG, <c>FUN_00516330</c>).
	/// </summary>
	public static Func<int, int> RandomIndex { get; set; } = count => Random.Shared.Next( count );

	private static readonly object registryLock = new();
	private static readonly Dictionary<int, WeakReference<RideVM>> registry = new();
	private static int nextHandle = 1; // 0 is reserved as "no VM"

	// Assign this VM a handle and add it to the global registry; returns the handle (ctor stores it).
	private int Register()
	{
		lock ( registryLock )
		{
			var handle = nextHandle++;
			registry[handle] = new WeakReference<RideVM>( this );
			return handle;
		}
	}

	/// <summary>Resolve a VM by its <see cref="Handle"/> (the original's <c>FUN_0055a070</c>); null if gone.</summary>
	public static RideVM? Resolve( int handle )
	{
		lock ( registryLock )
		{
			if ( registry.TryGetValue( handle, out var weak ) && weak.TryGetTarget( out var vm ) )
				return vm;
			return null;
		}
	}

	/// <summary>All live VMs whose <see cref="ScriptName"/> equals <paramref name="name"/> (case-sensitive,
	/// as the original's strcmp), pruning any dead weak references it passes.</summary>
	public static List<RideVM> MatchingByName( string name )
	{
		var matches = new List<RideVM>();
		lock ( registryLock )
		{
			var dead = new List<int>();
			foreach ( var (handle, weak) in registry )
			{
				if ( !weak.TryGetTarget( out var vm ) )
					dead.Add( handle );
				else if ( vm.ScriptName == name )
					matches.Add( vm );
			}
			foreach ( var h in dead )
				registry.Remove( h );
		}
		return matches;
	}

	/// <summary>Drop a VM from the registry (e.g. when <c>REMOVECHILD</c> destroys a child).</summary>
	public void Unregister()
	{
		lock ( registryLock )
			registry.Remove( Handle );
	}
}
