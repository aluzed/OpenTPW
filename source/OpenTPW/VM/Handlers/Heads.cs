namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Head opcodes (56–57): mount/unmount decorative "head" objects in the ride's head-node slots. RE'd
	/// from op_56 (ADDHEAD) / op_57 (DELHEAD) — a per-VM head-slot table (pure VM bookkeeping, modelled in
	/// <see cref="RideVM"/>, see RideVM.Heads.cs). The visual head-mesh attachment at a head node needs ride
	/// head-node geometry that isn't decoded yet, so these maintain the slot table only. See docs/tickets/T-007.
	/// </summary>
	public static class Heads
	{
		[OpcodeHandler( Opcode.ADDHEAD, "Mount a head value in a random free head slot." )]
		public static void AddHead( ref RideVM vm, Operand value )
			=> vm.AddHead( value.Value );

		[OpcodeHandler( Opcode.DELHEAD, "Remove every head slot holding the value." )]
		public static void DelHead( ref RideVM vm, Operand value )
			=> vm.RemoveHead( value.Value );
	}
}
