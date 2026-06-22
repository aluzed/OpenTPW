namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// The "limbo" opcodes — a per-VM timed queue of values (the original VM struct's <c>+0x24</c> table,
	/// count <c>+0x60</c>, capacity <c>+0x58</c>), recovered from the executor <c>FUN_00551cb0</c> cases
	/// 58–62 in tp.exe. <c>LIMBO</c> parks a value with an expiry; <c>UNLIMBO</c> releases the first
	/// <em>expired</em> entry; <c>FORCEUNLIMBO</c> the first regardless; <c>INLIMBO</c> reads the count;
	/// <c>LIMBOSPACE</c> the free slots. Despite the "engine" tag in older notes these touch only VM state
	/// (no world side-effect), so they run + unit-test headless. Each sets the VM flags from its result —
	/// the original writes the per-op result register (<c>+0x48</c>) that conditional branches test, so
	/// e.g. <c>UNLIMBO x; JZ …</c> branches when nothing was released. See docs/tickets/T-007.
	/// </summary>
	public static class Limbo
	{
		[OpcodeHandler( Opcode.LIMBO, "Park `value` in the limbo list, expiring `secs` from now (result 1 if added, 0 if full)." )]
		public static void Park( ref RideVM vm, Operand value, Operand secs )
		{
			int result;
			if ( vm.Limbo.Count >= RideVM.LimboCapacity )
			{
				result = 0; // no free slot
			}
			else
			{
				// The original stores expiry = now_ms + secs*1000 against its millisecond clock; we use the
				// VM's GameTime as that clock (same base WAIT/timers use), so units stay consistent + testable.
				vm.Limbo.Add( (value.Value, vm.GameTime + secs.Value * 1000) );
				result = 1;
			}
			SetResultFlags( ref vm, result );
		}

		[OpcodeHandler( Opcode.UNLIMBO, "Release the first expired limbo value into dest (0 if none expired)." )]
		public static void Release( ref RideVM vm, Operand dest )
		{
			var now = vm.GameTime; // can't capture `ref vm` in the lambda
			var idx = vm.Limbo.FindIndex( e => e.Expiry < now );
			dest.Value = RemoveAt( vm, idx );
			SetResultFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.FORCEUNLIMBO, "Release the first limbo value into dest regardless of expiry (0 if empty)." )]
		public static void ForceRelease( ref RideVM vm, Operand dest )
		{
			dest.Value = RemoveAt( vm, vm.Limbo.Count > 0 ? 0 : -1 );
			SetResultFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.INLIMBO, "Read the number of values currently in limbo into dest." )]
		public static void Count( ref RideVM vm, Operand dest )
		{
			dest.Value = vm.Limbo.Count;
			SetResultFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.LIMBOSPACE, "Read the number of free limbo slots into dest." )]
		public static void Space( ref RideVM vm, Operand dest )
		{
			dest.Value = RideVM.LimboCapacity - vm.Limbo.Count;
			SetResultFlags( ref vm, dest.Value );
		}

		// Remove the limbo entry at idx (if valid), returning its value (0 when idx is out of range).
		private static int RemoveAt( RideVM vm, int idx )
		{
			if ( idx < 0 || idx >= vm.Limbo.Count )
				return 0;
			var value = vm.Limbo[idx].Value;
			vm.Limbo.RemoveAt( idx );
			return value;
		}

		// Mirror the original's result register feeding the Zero/Sign flags a following branch tests.
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
