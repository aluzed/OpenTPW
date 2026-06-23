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

		[OpcodeHandler( Opcode.TOUR, "Tour-ride control, multiplexed by subcommand." )]
		public static void Tour( ref RideVM vm, Operand sub, Operand arg )
		{
			// RE'd (op_53 @ 0x5542fe): a fixed 2-operand multiplexer (sub + arg) dispatching into a
			// tour-ride object class (FUN_0055a620 create / FUN_0055d3d0 destroy / per-car query+setter
			// helpers). Every helper bails when the tour subsystem isn't live, so absent an engine the
			// faithful behaviour is: queries return 0, commands no-op. Query subcommands write the result
			// register the following BRANCH reads — mirror the "return 0" path. See docs/tickets/T-007.
			var v = vm;
			RunCarMultiplexer( v, sub, arg, IsTourQuery( sub.Value ), ( s, a ) => v.Engine?.Tour( s, a ) );
		}

		[OpcodeHandler( Opcode.BUMP, "Bumper/kart-ride control, multiplexed by subcommand." )]
		public static void Bump( ref RideVM vm, Operand sub, Operand arg )
		{
			// RE'd (op_54 @ 0x5546f5): the bumper/kart sibling of TOUR — same fixed 2-operand form,
			// dispatching into the bumper-car class (1 add peep · 3 start · 4 add car · 7 open ·
			// 11 cars-on-ride · 13 set laps · 16 remove car · 17 set open). Same engine-absent semantics.
			var v = vm;
			RunCarMultiplexer( v, sub, arg, IsBumpQuery( sub.Value ), ( s, a ) => v.Engine?.Bump( s, a ) );
		}

		// The shared TOUR/BUMP/COAST behaviour: route the (sub, arg) to the car engine; for a query
		// subcommand, return 0 (set the Zero flag, and write 0 into the destination if it's a variable) —
		// exactly what the original returns when the car subsystem isn't initialised.
		private static void RunCarMultiplexer( RideVM vm, Operand sub, Operand arg, bool isQuery, Action<int, int> route )
		{
			if ( isQuery && vm.Engine == null )
			{
				if ( arg.type == Operand.Type.Variable )
					arg.Value = 0;
				vm.Flags = RideVM.VMFlags.Zero; // result 0 → Zero set (see SetResultFlags elsewhere)
			}
			route( sub.Value, arg.Value );
		}

		// Subcommands whose handler writes the VM result register (from the op_53 / op_54 decompiles).
		private static bool IsTourQuery( int sub ) => sub is 3 or 4 or 10 or 11 or 15 or 16;
		private static bool IsBumpQuery( int sub ) => sub is 1 or 2 or 4 or 5 or 11 or 12 or 16;

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
			// state the ride motion/animation engine consumes. (The sibling motion ops TOUR/BUMP are the
			// car-object multiplexers handled above.)
			=> vm.Turbo = value.Value & 0xFF;
	}
}
