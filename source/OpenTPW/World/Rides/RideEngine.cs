namespace OpenTPW;

/// <summary>
/// Concrete ride engine for one running <see cref="Ride"/>. Owns the registry of objects a ride's
/// RSE script spawns, which the VM's engine opcodes operate on. Slice 1: the object registry + sound
/// playback (through the game <see cref="Audio"/> system). Animation / lights / peeps / coaster come
/// in later stages. See the plan and docs/tickets/T-007.
/// </summary>
public sealed class RideEngine : IRideEngine
{
	private readonly Ride ride;
	private readonly Dictionary<int, RideObject> objects = new();
	private SdtArchive? rideSounds;

	public RideEngine( Ride ride )
	{
		this.ride = ride;
	}

	public void SpawnObject( int type, int parameter, int id, int slot )
	{
		var obj = new RideObject { Id = id, Slot = slot, Type = type };
		objects[id] = obj;

		if ( IsSoundType( type ) )
		{
			Log.Info( $"[ride] ADDOBJ sound type={type} param={parameter} id={id} slot={slot}" );
			obj.SoundKey = PlayRideSound( parameter );
		}
		else
		{
			// Particles / other object kinds are not handled yet (Stage 2+); registered so later
			// opcodes can still reference the handle without erroring.
			Log.Trace( $"[ride] ADDOBJ type={type} param={parameter} id={id} slot={slot} (kind deferred)" );
		}
	}

	public void PlaySound( int sound )
	{
		Log.Info( $"[ride] SPAWNSOUND {sound}" );
		PlayRideSound( sound );
	}

	public void KillObject( int id )
	{
		// Registry removal only for now; full world despawn of a model object is Stage 2 (the Entity
		// list isn't yet pruned on delete).
		objects.Remove( id );
	}

	private static bool IsSoundType( int type ) => type is
		(int)ScriptDefs.Effects.OBJ_SOUND_LOC_RID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_RID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_KID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_STA
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_AMB
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_UI
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_BMP;
	// NB: OBJ_SOUND_LOC_AMB shares value 1 with OBJ_PTCL, so type==1 is ambiguous and treated as a
	// (deferred) particle for now — the unambiguous sound types above are enough for slice 1.

	/// <summary>
	/// Best-effort: play a ride sound for the script's sound code. The exact code→asset mapping is
	/// the `.MAP` audio catalog (T-016) — not decoded yet — so for slice 1 this indexes the global
	/// ride-sound archive to give an audible, logged proof that the opcode fired. Returns the audio
	/// cache key, or null if unavailable.
	/// </summary>
	private string? PlayRideSound( int code )
	{
		try
		{
			var path = Path.Join( GameDir.GamePath, "data", "global", "sound", "RideHD.sdt" );
			if ( !File.Exists( path ) )
				return null;

			rideSounds ??= new SdtArchive( path );
			if ( rideSounds.soundFiles.Count == 0 )
				return null;

			var track = rideSounds.soundFiles[Math.Abs( code ) % rideSounds.soundFiles.Count];
			var key = $"ride_{track.Name}";
			Audio.PlaySfx( key, track.SoundData );
			Log.Info( $"[ride] sound code={code} -> {track.Name} (approx mapping, see T-016)" );
			return key;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] sound code={code} failed: {e.Message}" );
			return null;
		}
	}
}
