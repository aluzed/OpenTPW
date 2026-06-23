namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Particle-effect opcodes, routed to the engine's particle system (which renders the decoded
	/// <c>.PLB</c> effects — T-019). RE'd from the executor cases op_93 (REPAIREFFECT) and op_105 (SPARK):
	/// both resolve a position and call the particle spawner <c>FUN_0051bfc0(lib, effectCode, x, y, z)</c>.
	/// REPAIREFFECT plays the <c>P_EFFECT_Repair</c> effect at the ride; SPARK the <c>P_EFFECT_Sparks</c>
	/// effect. The exact spawn count / particle-node positions need the ride's particle-node geometry
	/// (not decoded), so each fires its named effect at the ride position. See docs/tickets/T-007 / T-019.
	/// </summary>
	public static class Particles
	{
		// par_lib.h P_EFFECT_* indices (matched exactly by the decoded Tp2.plb effect names — see T-019).
		private const int EffectSparks = 1;
		private const int EffectRepair = 51;

		[OpcodeHandler( Opcode.REPAIREFFECT, "Play the repair particle effect at the ride." )]
		public static void RepairEffect( ref RideVM vm, Operand _ )
			=> vm.Engine?.SpawnParticleEffect( EffectRepair );

		[OpcodeHandler( Opcode.SPARK, "Emit sparks at the ride." )]
		public static void Spark( ref RideVM vm, Operand _, Operand __, Operand ___, Operand ____ )
			=> vm.Engine?.SpawnParticleEffect( EffectSparks );

		[OpcodeHandler( Opcode.GETCUSTPTCLCODE, "Get a custom particle code into dest — always 0 in the shipped build." )]
		public static void GetCustomParticleCode( ref RideVM vm, Operand dest, Operand arg )
		{
			// RE'd at instruction level (op_94): the second operand is read + evaluated but the value is
			// discarded (MOV EAX,ESI overwrites it). The result written to dest + the result register is
			// EDI, which the executor prologue zeroes (`XOR EDI,EDI` @ 0x551cc1) and never reassigns (EDI is
			// preserved across the eval call) — so this opcode always yields 0. The "custom particle code"
			// lookup is effectively a stub (0 = none) in this build of tp.exe; arg is ignored.
			_ = arg;
			dest.Value = 0;
			vm.Flags = RideVM.VMFlags.Zero; // result register = 0
		}
	}
}
