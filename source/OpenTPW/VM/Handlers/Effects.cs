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
			// COAST is a coaster-object interface (RE'd: handler FUN_00554a5a switches on the subcommand
			// and calls into a coaster class FUN_0043bXXX). Subcommands 2 (can-load?) and 3 (peep-wants-
			// off?) are queries whose result the following BRANCH_Z reads via the Zero flag. We have no
			// coaster-object engine yet (T-045's coaster is build-mode + separate), so:
			//   2 → clear Zero ("a car is free"): the script's load loop then falls through to its
			//       TEST(VAR_LETMEON) gate, so the queue→VAR_LETMEON bridge (Ride.NotifyBoarding) drives
			//       loading exactly as for the other rides — one rider per boarding signal.
			//   3 → set Zero ("nobody wants off"): no scripted unload until the car/timing engine exists.
			if ( sub.Value == 2 )
				vm.Flags = RideVM.VMFlags.None;
			else if ( sub.Value == 3 )
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

		[OpcodeHandler( Opcode.TURBO, "Set the ride's turbo flag (the motion engine reads it to speed up)." )]
		public static void Turbo( ref RideVM vm, Operand value )
			// RE'd (op_51): the operand's low byte is stored straight into the VM (struct +0xb8). Pure VM
			// state; the ride motion/animation engine would consume it. (The sibling motion ops TOUR/BUMP
			// need their own engine subsystems — tour-object / bumper-car physics — and remain.)
			=> vm.Turbo = value.Value & 0xFF;
	}
}
