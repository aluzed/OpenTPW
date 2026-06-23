namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Walk opcodes (76–81): the ride-script peep-walking subsystem. RE'd from op_76..op_81 + their helpers
	/// (<c>FUN_00556f40</c>/<c>005571a0</c>/<c>00557110</c>/<c>00557160</c>). They drive a per-VM walk-slot
	/// table + a "walk float" timer — pure VM bookkeeping (like the limbo family), modelled in
	/// <see cref="RideVM"/> (see RideVM.Walk.cs). WALKGET / WALKFLOATSTAT write a result into <c>dest</c> and
	/// set the Zero/Sign flags (the original's <c>+0x48</c> result register).
	///
	/// <para>The WALKON operand order follows <c>FUN_00556f40</c>'s parameter order
	/// (peep, startNode, endNode, …, type, …); it is unverified against a real script because no shipped
	/// <c>.rse</c> uses the walk opcodes (they're driven by scenery/ride authoring we don't have a sample
	/// of). See docs/tickets/T-007.</para>
	/// </summary>
	public static class Walk
	{
		[OpcodeHandler( Opcode.WALKON, "Send a peep walking between two walk nodes (peep, start, end, …, type, …)." )]
		public static void WalkOn( ref RideVM vm, Operand peep, Operand startNode, Operand endNode,
			Operand extra5, Operand extra6, Operand type, Operand extra8 )
			=> vm.WalkAdd( peep.Value, startNode.Value, endNode.Value, type.Value, extra5.Value, extra6.Value, extra8.Value );

		[OpcodeHandler( Opcode.WALKOFF, "Start a walking peep heading back off the ride." )]
		public static void WalkOff( ref RideVM vm, Operand peep )
			=> vm.WalkRelease( peep.Value );

		[OpcodeHandler( Opcode.WALKGET, "Retrieve a peep that finished walking off into dest (0 if none)." )]
		public static void WalkGet( ref RideVM vm, Operand dest )
		{
			dest.Value = vm.WalkRetrieve();
			SetResultFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.WALKST_FLOAT, "Start the walk-float timer (value, p3, p4)." )]
		public static void WalkStartFloat( ref RideVM vm, Operand value, Operand extra3, Operand extra4 )
			=> vm.WalkFloatBegin( value.Value, extra3.Value, extra4.Value );

		[OpcodeHandler( Opcode.WALKFLOATSTAT, "Read the walk-float value into dest." )]
		public static void WalkFloatStat( ref RideVM vm, Operand dest )
		{
			dest.Value = vm.WalkFloatValue;
			SetResultFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.WALKFLOATSTOP, "Finalise the walk-float timer." )]
		public static void WalkFloatStop( ref RideVM vm )
			=> vm.WalkFloatStop();

		// Mirror the original's result register (+0x48) feeding the Zero/Sign flags a following branch tests.
		private static void SetResultFlags( ref RideVM vm, int result )
		{
			vm.Flags = RideVM.VMFlags.None;
			if ( result == 0 )
				vm.Flags |= RideVM.VMFlags.Zero;
			if ( result < 0 )
				vm.Flags |= RideVM.VMFlags.Sign;
		}
	}
}
