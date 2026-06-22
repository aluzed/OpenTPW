namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Ride light opcodes (82–85), routed to the engine like the other Batch B engine opcodes. RE'd from
	/// the executor cases op_82..op_85 (tp.exe): each resolves a ride light object by the script's id
	/// (a type-<c>0x20000</c> object), then enables/disables it or sets its intensity/colour. The
	/// brightness/colour operands are integer <b>percentages</b> that the original scales by
	/// <c>_DAT_00700fe0 = 0.01</c> (so 0..100 → 0..1) before handing them to the light setters
	/// (<c>FUN_004587e0</c> / <c>FUN_00458890</c>) — we apply the same scale here. With no engine attached
	/// (headless / unit tests) the null-conditional calls are no-ops. See docs/tickets/T-007 / T-032.
	/// </summary>
	public static class Lights
	{
		private const float PercentScale = 0.01f; // _DAT_00700fe0: script 0..100 → 0..1

		[OpcodeHandler( Opcode.ENABLELIGHT, "Turn on a ride light by id." )]
		public static void EnableLight( ref RideVM vm, Operand id )
			=> vm.Engine?.EnableLight( id.Value );

		[OpcodeHandler( Opcode.DISABLELIGHT, "Turn off a ride light by id." )]
		public static void DisableLight( ref RideVM vm, Operand id )
			=> vm.Engine?.DisableLight( id.Value );

		[OpcodeHandler( Opcode.SETLIGHT, "Set a ride light's brightness (0..100)." )]
		public static void SetLight( ref RideVM vm, Operand id, Operand brightness )
			=> vm.Engine?.SetLight( id.Value, brightness.Value * PercentScale );

		[OpcodeHandler( Opcode.COLOURLIGHT, "Set a ride light's colour (r, g, b each 0..100)." )]
		public static void ColourLight( ref RideVM vm, Operand id, Operand r, Operand g, Operand b )
			=> vm.Engine?.ColourLight( id.Value, r.Value * PercentScale, g.Value * PercentScale, b.Value * PercentScale );
	}
}
