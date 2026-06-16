namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Time and timer opcodes. Semantics recovered from the original executor
	/// (FUN_00551cb0 in tp.exe): the date opcodes read the C runtime clock via
	/// time()/localtime() and return raw <c>tm</c> fields (year since 1900, month 0-11);
	/// GETTIME/SETTIMER/GETTIMER use the game's millisecond clock. See docs/tickets/T-007.
	/// </summary>
	public static class Time
	{
		[OpcodeHandler( Opcode.YEAR, "Current year (since 1900) into the destination." )]
		public static void Year( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Year - 1900;

		[OpcodeHandler( Opcode.MONTH, "Current month (0-11) into the destination." )]
		public static void Month( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Month - 1;

		[OpcodeHandler( Opcode.DAY, "Current day of the month (1-31) into the destination." )]
		public static void Day( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Day;

		[OpcodeHandler( Opcode.HOUR, "Current hour (0-23) into the destination." )]
		public static void Hour( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Hour;

		[OpcodeHandler( Opcode.MIN, "Current minute (0-59) into the destination." )]
		public static void Minute( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Minute;

		[OpcodeHandler( Opcode.SEC, "Current second (0-59) into the destination." )]
		public static void Second( ref RideVM vm, Operand dest ) => dest.Value = vm.WallClock().Second;

		[OpcodeHandler( Opcode.GETTIME, "Time the ride has been alive into the destination." )]
		public static void GetTime( ref RideVM vm, Operand dest ) => dest.Value = vm.GameTime;

		[OpcodeHandler( Opcode.SETTIMER, "Set the ride timer to expire `value` from now." )]
		public static void SetTimer( ref RideVM vm, Operand value ) => vm.Timer = vm.GameTime + value.Value;

		[OpcodeHandler( Opcode.GETTIMER, "Remaining time on the ride timer into the destination (never negative)." )]
		public static void GetTimer( ref RideVM vm, Operand dest ) => dest.Value = System.Math.Max( 0, vm.Timer - vm.GameTime );
	}
}
