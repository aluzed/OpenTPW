namespace OpenTPW;

/// <summary>
/// Concrete ride engine for one running <see cref="Ride"/>. Owns the registry of objects a ride's
/// RSE script spawns/animates. Stage 1: object registry, sound (via <see cref="Audio"/>), object
/// lifecycle, and **procedural** animation — a model with an active animation bobs over time (real
/// MD2 keyframe playback is stage 2-3). The ride body is registered as a self object (id
/// <see cref="SelfId"/>) and plays a looping idle by default, so the model is visibly alive and easy
/// to pick out. See the plan and docs/tickets/T-032 / T-007.
/// </summary>
public sealed class RideEngine : IRideEngine
{
	private const int SelfId = -1;     // the ride body's handle (script object ids are >= 0)
	private const float AnimDuration = 2f;   // procedural one-shot animation length, seconds
	private const float BobAmplitude = 4f;

	private readonly Dictionary<int, RideObject> objects = new();
	private SdtArchive? rideSounds;

	// Registers the ride's loaded meshes as the body, and starts a looping idle so it visibly moves.
	public void RegisterBody( IEnumerable<ModelEntity> parts )
	{
		var body = new RideObject { Id = SelfId, Type = -1 };
		foreach ( var e in parts )
			body.Parts.Add( (e, e.Position) );

		objects[SelfId] = body;
		StartAnim( body, (int)ScriptDefs.Animations.ANIM_Idle, loop: true );
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
		if ( !objects.Remove( id, out var obj ) )
			return;

		// Despawn its visual parts (the Entity list isn't otherwise pruned on delete).
		foreach ( var (entity, _) in obj.Parts )
			Entity.All.Remove( entity );
	}

	public void SetObjectParam( int id, int param, int value )
	{
		if ( objects.TryGetValue( id, out var obj ) )
			obj.Params[param] = value;
	}

	public void TriggerAnim( int id, int anim, bool loop )
	{
		var obj = Resolve( id );
		if ( obj == null )
			return;

		// Idempotent: re-triggering the same animation (e.g. each tick of a TRIGWAIT loop) must not
		// keep resetting its start time.
		if ( obj.AnimId == anim && obj.AnimLoop == loop )
			return;

		StartAnim( obj, anim, loop );
		Log.Trace( $"[ride] anim {(ScriptDefs.Animations)anim} on {id} loop={loop}" );
	}

	public void SetAnimSpeed( int id, float speed )
	{
		var obj = Resolve( id );
		if ( obj != null )
			obj.AnimSpeed = speed == 0 ? 1f : speed;
	}

	public void FlushAnims( int id )
	{
		if ( id < 0 )
		{
			foreach ( var obj in objects.Values )
				StopAnim( obj );
			return;
		}

		var o = Resolve( id );
		if ( o != null )
			StopAnim( o );
	}

	public int GetAnim( int id ) => Resolve( id )?.AnimId ?? 0;

	public bool IsAnimating( int id, int anim )
	{
		var obj = Resolve( id );
		// Looping anims (e.g. the idle) never "finish", so they never block a WAITANIM.
		return obj?.AnimId == anim && !obj.AnimLoop && Time.Now - obj.AnimStart < AnimDuration / obj.AnimSpeed;
	}

	public bool AnyAnimating()
	{
		foreach ( var obj in objects.Values )
			if ( obj.AnimId != null && !obj.AnimLoop && Time.Now - obj.AnimStart < AnimDuration / obj.AnimSpeed )
				return true;
		return false;
	}

	/// <summary>Per-frame procedural animation: bob each animating object's parts. Called by the Ride.</summary>
	public void Update( float now )
	{
		foreach ( var obj in objects.Values )
		{
			if ( obj.AnimId == null || obj.Parts.Count == 0 )
				continue;

			var t = ( now - obj.AnimStart ) * obj.AnimSpeed;
			if ( !obj.AnimLoop && t > AnimDuration )
			{
				StopAnim( obj );
				continue;
			}

			// Procedural placeholder until MD2 keyframes (stage 2-3): a gentle vertical bob.
			var bob = MathF.Sin( t * 2f ) * BobAmplitude;
			foreach ( var (entity, basePos) in obj.Parts )
				entity.Position = basePos + new Vector3( 0, 0, bob );
		}
	}

	private static void StartAnim( RideObject o, int anim, bool loop )
	{
		o.AnimId = anim;
		o.AnimLoop = loop;
		o.AnimStart = Time.Now;
	}

	private static void StopAnim( RideObject o )
	{
		o.AnimId = null;
		foreach ( var (entity, basePos) in o.Parts )
			entity.Position = basePos;
	}

	// Unknown ids fall back to the ride body, so a script animating a part we don't model yet still
	// produces visible motion (stage 1). Once parts get their own models this resolves them directly.
	private RideObject? Resolve( int id ) =>
		objects.TryGetValue( id, out var obj ) ? obj : objects.GetValueOrDefault( SelfId );

	private static bool IsSoundType( int type ) => type is
		(int)ScriptDefs.Effects.OBJ_SOUND_LOC_RID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_RID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_KID
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_STA
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_AMB
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_UI
		or (int)ScriptDefs.Effects.OBJ_SOUND_GLO_BMP;

	/// <summary>
	/// Best-effort: play a ride sound for the script's sound code. The exact code→asset mapping is
	/// the `.MAP` audio catalog (T-016) — not decoded yet — so for now this indexes the global
	/// ride-sound archive to give an audible, logged proof. Returns the audio cache key, or null.
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
