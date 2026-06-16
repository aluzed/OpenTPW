namespace OpenTPW;

/// <summary>
/// The runtime that a ride's RSE script drives. The VM's "engine" opcodes (Batch B) call into this;
/// when a VM has no engine attached (unit tests, headless tools) the calls are guarded no-ops, so
/// the pure-VM behaviour is unchanged. Stage 1 covers object spawning, sound, object lifecycle and
/// (procedural) animation; later stages add real keyframe animation, lights, peeps and coasters (see
/// docs/tickets/T-007 + T-032).
/// </summary>
public interface IRideEngine
{
	/// <summary>ADDOBJ — register an object (sound / particle / …) under the script's handle + slot.</summary>
	void SpawnObject( int type, int parameter, int id, int slot );

	/// <summary>SPAWNSOUND — play a sound identified by the script.</summary>
	void PlaySound( int sound );

	/// <summary>KILLOBJ — remove a previously-spawned object by its handle id (and despawn its model).</summary>
	void KillObject( int id );

	/// <summary>SETOBJPARAM — set parameter <paramref name="param"/> of object <paramref name="id"/>.</summary>
	void SetObjectParam( int id, int param, int value );

	/// <summary>TRIGANIM / LOOPANIM — start animation <paramref name="anim"/> on object <paramref name="id"/>.</summary>
	void TriggerAnim( int id, int anim, bool loop );

	/// <summary>TRIGANIMSPEED — set the animation playback speed of object <paramref name="id"/>.</summary>
	void SetAnimSpeed( int id, float speed );

	/// <summary>FLUSHANIM — stop the animation on object <paramref name="id"/> (id &lt; 0 = all).</summary>
	void FlushAnims( int id );

	/// <summary>GETANIM — the current animation of object <paramref name="id"/> (0 if none).</summary>
	int GetAnim( int id );

	/// <summary>WAITANIM — whether object <paramref name="id"/> is still playing the given (non-looping) anim.</summary>
	bool IsAnimating( int id, int anim );

	/// <summary>WAIT4ANIM — whether any object is still playing a (non-looping) animation.</summary>
	bool AnyAnimating();
}
