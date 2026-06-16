namespace OpenTPW;

partial class OpcodeHandlers
{
	/// <summary>
	/// Engine "object" opcodes — they don't change VM state, they ask the ride engine to do something
	/// in the world (spawn objects, play sounds, …). Each routes to <see cref="RideVM.Engine"/>; when
	/// no engine is attached (unit tests / headless) the null-conditional call is a no-op, identical
	/// to the previous "no handler" behaviour. Operand counts match docs/06-rse-vm-opcodes.md. See the
	/// ride-engine plan / T-007. (More engine families — animation, lights, … — land in later stages.)
	/// </summary>
	public static class Objects
	{
		[OpcodeHandler( Opcode.ADDOBJ, "Spawn a ride object (sound / particle / …) under a handle." )]
		public static void AddObj( ref RideVM vm, Operand type, Operand parameter, Operand id, Operand slot )
		{
			vm.Engine?.SpawnObject( type.Value, parameter.Value, id.Value, slot.Value );
		}

		[OpcodeHandler( Opcode.SPAWNSOUND, "Play a sound." )]
		public static void SpawnSound( ref RideVM vm, Operand sound )
		{
			vm.Engine?.PlaySound( sound.Value );
		}

		[OpcodeHandler( Opcode.KILLOBJ, "Remove a spawned object by its handle." )]
		public static void KillObj( ref RideVM vm, Operand id )
		{
			vm.Engine?.KillObject( id.Value );
		}
	}
}
