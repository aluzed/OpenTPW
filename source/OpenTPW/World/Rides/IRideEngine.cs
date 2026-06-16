namespace OpenTPW;

/// <summary>
/// The runtime that a ride's RSE script drives. The VM's "engine" opcodes (Batch B) call into this;
/// when a VM has no engine attached (unit tests, headless tools) the calls are guarded no-ops, so
/// the pure-VM behaviour is unchanged. Slice 1 covers object spawning + sound; later stages add
/// animation, lights, peeps and coaster behaviour (see docs/tickets/T-007 + T-032 and the plan).
/// </summary>
public interface IRideEngine
{
	/// <summary>ADDOBJ — register an object (sound / particle / …) under the script's handle + slot.</summary>
	void SpawnObject( int type, int parameter, int id, int slot );

	/// <summary>SPAWNSOUND — play a sound identified by the script.</summary>
	void PlaySound( int sound );

	/// <summary>KILLOBJ — remove a previously-spawned object by its handle id.</summary>
	void KillObject( int id );
}
