namespace OpenTPW;

partial class OpcodeHandlers
{
	public static class Math
	{
		[OpcodeHandler( Opcode.ADD, "Add two values together" )]
		public static void Add( ref RideVM vm, Operand dest, Operand value )
		{
			dest.Value = dest.Value + value.Value;
		}

		[OpcodeHandler( Opcode.SUB, "Subtract one value from another" )]
		public static void Sub( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
		{
			dest.Value = valueA.Value - valueB.Value;
		}

		[OpcodeHandler( Opcode.MULT, "Multiply two values together" )]
		public static void Mult( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
		{
			dest.Value = valueA.Value * valueB.Value;
			SetArithmeticFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.DIV, "Divide one value by another" )]
		public static void Div( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
		{
			// Guard against divide-by-zero; the original VM has no exception machinery.
			dest.Value = valueB.Value != 0 ? valueA.Value / valueB.Value : 0;
			SetArithmeticFlags( ref vm, dest.Value );
		}

		[OpcodeHandler( Opcode.MOD, "Get the remainder of dividing one value by another" )]
		public static void Mod( ref RideVM vm, Operand valueA, Operand valueB, Operand dest )
		{
			dest.Value = valueB.Value != 0 ? valueA.Value % valueB.Value : 0;
			SetArithmeticFlags( ref vm, dest.Value );
		}

		/// <summary>
		/// Sets the Zero / Sign flags from an arithmetic result, as the VM does after a
		/// calculation (see the instruction set docs).
		/// </summary>
		private static void SetArithmeticFlags( ref RideVM vm, int result )
		{
			vm.Flags = RideVM.VMFlags.None;

			if ( result == 0 )
				vm.Flags |= RideVM.VMFlags.Zero;

			if ( result < 0 )
				vm.Flags |= RideVM.VMFlags.Sign;
		}

		[OpcodeHandler( Opcode.TEST, "Set flags depending on the value given." )]
		public static void Test( ref RideVM vm, Operand value )
		{
			vm.Flags = RideVM.VMFlags.None;

			if ( value.Value == 0 )
				vm.Flags |= RideVM.VMFlags.Zero;

			if ( value.Value < 0 )
				vm.Flags |= RideVM.VMFlags.Sign;
		}

		[OpcodeHandler( Opcode.CMP, "Compare two values and set any flags according to the result." )]
		public static void Compare( ref RideVM vm, Operand valueA, Operand valueB )
		{
			vm.Flags = RideVM.VMFlags.None;

			if ( valueA.Value == valueB.Value )
				vm.Flags |= RideVM.VMFlags.Zero;

			if ( valueA.Value < valueB.Value )
				vm.Flags |= RideVM.VMFlags.Sign;
		}
	}
}
