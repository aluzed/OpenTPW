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
