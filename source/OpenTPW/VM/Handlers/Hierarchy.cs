namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Cross-VM variable opcodes. Semantics recovered from the executor (FUN_00551cb0 in
	/// tp.exe): the original resolves the other VM by a handle in a global VM list (struct
	/// +0x0c = child, +0x10 = parent), bounds-checks against its variable count (+0x8c), and
	/// reads/writes its variables array (+0x1c). Modelled here via <see cref="RideVM.ActiveChild"/>
	/// / <see cref="RideVM.Parent"/>. A missing link or out-of-range index is a no-op (the
	/// original bails to its error path). See docs/tickets/T-007.
	/// </summary>
	public static class Hierarchy
	{
		[OpcodeHandler( Opcode.SPAWNCHILD, "Spawn a child ride script by name and make it the active child." )]
		public static void SpawnChild( ref RideVM vm, Operand scriptName )
		{
			// The operand is a string index naming the child .RSE (the original builds a path and
			// calls the script loader). We resolve the name, then defer the actual load to the
			// engine-supplied ChildLoader; the parent/child link is what the var opcodes use.
			if ( !vm.Strings.TryGetValue( scriptName.Value, out var name ) )
			{
				Log.Warning( $"SPAWNCHILD: no string at {scriptName.Value}; ignoring" );
				return;
			}

			var child = vm.ChildLoader?.Invoke( name );
			if ( child == null )
			{
				Log.Warning( $"SPAWNCHILD: could not spawn child '{name}' (no loader or not found)" );
				return;
			}

			child.Parent = vm;
			vm.ActiveChild = child;
			vm.Children.Add( child );
		}

		[OpcodeHandler( Opcode.SETVARINCHILD, "Set variable [index] in the active child VM to value." )]
		public static void SetVarInChild( ref RideVM vm, Operand index, Operand value )
			=> SetVar( vm.ActiveChild, index.Value, value.Value );

		[OpcodeHandler( Opcode.GETVARINCHILD, "Read variable [index] from the active child VM into dest." )]
		public static void GetVarInChild( ref RideVM vm, Operand dest, Operand index )
			=> GetVar( vm.ActiveChild, index.Value, dest );

		[OpcodeHandler( Opcode.SETVARINPARENT, "Set variable [index] in the parent VM to value." )]
		public static void SetVarInParent( ref RideVM vm, Operand index, Operand value )
			=> SetVar( vm.Parent, index.Value, value.Value );

		[OpcodeHandler( Opcode.GETVARINPARENT, "Read variable [index] from the parent VM into dest." )]
		public static void GetVarInParent( ref RideVM vm, Operand dest, Operand index )
			=> GetVar( vm.Parent, index.Value, dest );

		//
		// Cross-VM by handle (not just parent/child). RE'd from the executor cases op_90/91/92 + the
		// resolver FUN_0055a070 (walks the global VM list matching the +0x08 handle) — see RideVM.Registry.cs.
		//

		[OpcodeHandler( Opcode.GETREMOTEVAR, "Read variable [index] from the VM with the given handle into dest." )]
		public static void GetRemoteVar( ref RideVM vm, Operand dest, Operand handle, Operand index )
		{
			// The original zeroes its result register first, then overwrites it if the lookup succeeds.
			var target = RideVM.Resolve( handle.Value );
			var result = target != null && index.Value >= 0 && index.Value < target.Variables.Count
				? target.Variables[index.Value]
				: 0;
			dest.Value = result;
			SetResultFlags( ref vm, result );
		}

		[OpcodeHandler( Opcode.SETREMOTEVAR, "Set variable [index] in the VM with the given handle to value." )]
		public static void SetRemoteVar( ref RideVM vm, Operand handle, Operand index, Operand value )
		{
			var target = RideVM.Resolve( handle.Value );
			if ( target == null )
				return; // unknown handle: no-op (the original bails before touching its result register)

			if ( index.Value < 0 || index.Value >= target.Variables.Count )
			{
				Log.Warning( $"SETREMOTEVAR: illegal variable reference {index.Value} on VM {handle.Value}" );
				return;
			}

			target.Variables[index.Value] = value.Value;
			SetResultFlags( ref vm, value.Value );
		}

		[OpcodeHandler( Opcode.FINDSCRIPTRAND, "Find a random live script named by the string operand; its handle into dest (0 if none)." )]
		public static void FindScriptRand( ref RideVM vm, Operand name, Operand dest )
		{
			// The operand is a string (the wanted script name). The original counts every VM whose name
			// matches, then picks one uniformly at random and returns its handle (0 if there are none).
			var result = 0;
			if ( vm.Strings.TryGetValue( name.Value, out var wanted ) )
			{
				var matches = RideVM.MatchingByName( wanted );
				if ( matches.Count > 0 )
					result = matches[RideVM.RandomIndex( matches.Count )].Handle;
			}
			dest.Value = result;
			SetResultFlags( ref vm, result );
		}

		[OpcodeHandler( Opcode.REMOVECHILD, "Destroy the active child VM and clear the child link." )]
		public static void RemoveChild( ref RideVM vm )
		{
			// op_65: if a child is attached, destroy it (the original frees it + drops it from the global
			// list) and clear the child slot. We drop our references and unregister it.
			var child = vm.ActiveChild;
			if ( child == null )
				return;

			child.Unregister();
			child.Parent = null;
			vm.Children.Remove( child );
			vm.ActiveChild = null;
		}

		// Mirror the original's result register (+0x48) feeding the Zero/Sign flags a following branch tests.
		private static void SetResultFlags( ref RideVM vm, int result )
		{
			vm.Flags = RideVM.VMFlags.None;
			if ( result == 0 )
				vm.Flags |= RideVM.VMFlags.Zero;
			if ( result < 0 )
				vm.Flags |= RideVM.VMFlags.Sign;
		}

		private static void SetVar( RideVM? target, int index, int value )
		{
			if ( target == null || index < 0 || index >= target.Variables.Count )
			{
				Log.Warning( $"SETVARIN*: no target VM or index {index} out of range; ignoring" );
				return;
			}

			target.Variables[index] = value;
		}

		private static void GetVar( RideVM? target, int index, Operand dest )
		{
			if ( target == null || index < 0 || index >= target.Variables.Count )
			{
				Log.Warning( $"GETVARIN*: no target VM or index {index} out of range; ignoring" );
				return;
			}

			dest.Value = target.Variables[index];
		}
	}
}
