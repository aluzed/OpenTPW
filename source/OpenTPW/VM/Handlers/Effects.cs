namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Ride "effects / motion" engine opcodes: the coaster control multiplexer (<c>COAST</c>), ride
	/// event dispatch (<c>EVENT</c>/<c>EVENT_EXT</c>) and audio (<c>SETREVERB</c>/<c>DIPMUSIC</c>). Each
	/// routes to <see cref="RideVM.Engine"/> — a guarded no-op without one, like the other engine
	/// opcodes. Operand counts match docs/06-rse-vm-opcodes.md. See docs/tickets/T-032 / T-045.
	/// </summary>
	public static class Effects
	{
		[OpcodeHandler( Opcode.COAST, "Coaster control, multiplexed by subcommand." )]
		public static void Coast( ref RideVM vm, Operand sub, Operand arg )
		{
			// Subcommands 2 (can-load?) and 3 (peep-wants-off?) are queries whose result the following
			// BRANCH_Z reads via the Zero flag. With no coaster car/track engine yet (T-045's coaster is
			// build-mode and separate), report "nothing to do" (Zero set) so coaster scripts' load/unload
			// loops take their idle/yield branch instead of spinning every tick. Map RE'd from coaster1.rse.
			if ( sub.Value is 2 or 3 )
				vm.Flags = RideVM.VMFlags.Zero;
			vm.Engine?.Coast( sub.Value, arg.Value );
		}

		[OpcodeHandler( Opcode.EVENT, "Fire a ride event (type, p1, p2)." )]
		public static void Event( ref RideVM vm, Operand type, Operand p1, Operand p2 )
			=> vm.Engine?.Event( type.Value, p1.Value, p2.Value );

		[OpcodeHandler( Opcode.EVENT_EXT, "Fire a ride event with an extra parameter." )]
		public static void EventExt( ref RideVM vm, Operand type, Operand p1, Operand p2, Operand _ )
			=> vm.Engine?.Event( type.Value, p1.Value, p2.Value );

		[OpcodeHandler( Opcode.SETREVERB, "Set the audio reverb level." )]
		public static void SetReverb( ref RideVM vm, Operand level )
			=> vm.Engine?.SetReverb( level.Value );

		[OpcodeHandler( Opcode.DIPMUSIC, "Briefly duck the background music." )]
		public static void DipMusic( ref RideVM vm, Operand amount )
			=> vm.Engine?.DipMusic( amount.Value );
	}
}
