namespace OpenTPW;

/// <summary>
/// Concrete ride engine for one running <see cref="Ride"/>. Owns the registry of objects a ride's
/// RSE script spawns/animates. Object registry, sound (via <see cref="Audio"/>), object lifecycle,
/// and animation. Animation is now <b>channel-aware</b>: the owning Ride discovers the ride's real
/// animation channels from its WAD (see <see cref="SetAnimChannels"/> and docs/08-ghidra-animation.md)
/// so the engine animates only channels the ride actually ships and scales each by its keyframe
/// count. The visible motion is still a procedural placeholder bob — decoding the per-frame vertex
/// payload to morph the real keyframes is T-033. The ride body is registered as a self object (id
/// <see cref="SelfId"/>) and plays its looping motion (Main, else Idle) so the model is visibly alive.
/// See docs/tickets/T-032 / T-033 / T-007.
/// </summary>
public sealed class RideEngine : IRideEngine
{
	private const int SelfId = -1;     // the ride body's handle (script object ids are >= 0)
	private const float FrameSeconds = 0.1f; // placeholder playback rate (~10 fps) per keyframe
	private const float BobAmplitude = 12f;

	private readonly Dictionary<int, RideObject> objects = new();
	private SdtArchive? rideSounds;

	// The ride's real animation channels, discovered from the WAD: anim id -> keyframe count. Empty
	// until the owning Ride calls SetAnimChannels. A ScriptDefs.Animations value present here means
	// the ride actually ships that channel's keyframe files (see docs/08-ghidra-animation.md, T-033).
	private IReadOnlyDictionary<int, int> channelFrames = new Dictionary<int, int>();

	/// <summary>
	/// The single-letter animation channel code for a <see cref="ScriptDefs.Animations"/> value, as the
	/// original game derives it: the first letter of the animation name (Create→'c', Idle→'i',
	/// Main→'m', …). Ride keyframe files are named <c>&lt;base&gt;&lt;letter&gt;[&lt;frame#&gt;].md2</c>.
	/// </summary>
	public static char ChannelLetter( ScriptDefs.Animations anim )
	{
		const string prefix = "ANIM_";
		var name = anim.ToString();
		return name.StartsWith( prefix ) && name.Length > prefix.Length
			? char.ToLowerInvariant( name[prefix.Length] )
			: '\0';
	}

	/// <summary>
	/// Supplies the ride's real animation channels (anim id -> keyframe count), discovered by probing
	/// the WAD for <c>&lt;base&gt;&lt;letter&gt;[&lt;n&gt;].md2</c> files. Lets the engine animate only
	/// channels the ride actually has, and scale a channel's duration by its keyframe count.
	/// </summary>
	public void SetAnimChannels( IReadOnlyDictionary<int, int> channels )
	{
		channelFrames = channels;
		Log.Info( $"[ride] animation channels: " + ( channels.Count == 0
			? "none"
			: string.Join( ", ", channels.OrderBy( kv => kv.Key )
				.Select( kv => $"{(ScriptDefs.Animations)kv.Key}({ChannelLetter( (ScriptDefs.Animations)kv.Key )}×{kv.Value})" ) ) ) );
	}

	// Frame count for an animation channel (0 if the ride doesn't ship it), and the placeholder
	// playback duration derived from it.
	private int FrameCount( int anim ) => channelFrames.TryGetValue( anim, out var n ) ? n : 0;
	private float DurationOf( int anim ) => Math.Max( FrameCount( anim ), 1 ) * FrameSeconds;

	// Registers the ride's loaded meshes as the body, and starts its looping motion so it visibly
	// moves. The looping channel is Main (the original's primary ride motion) when the ride ships it,
	// otherwise Idle — see docs/08-ghidra-animation.md.
	public void RegisterBody( IEnumerable<ModelEntity> parts )
	{
		var body = new RideObject { Id = SelfId, Type = -1 };
		foreach ( var e in parts )
			body.Parts.Add( (e, e.Position) );

		objects[SelfId] = body;

		var loopAnim = FrameCount( (int)ScriptDefs.Animations.ANIM_Main ) > 0
			? ScriptDefs.Animations.ANIM_Main
			: ScriptDefs.Animations.ANIM_Idle;
		StartAnim( body, (int)loopAnim, loop: true );
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
		var frames = FrameCount( anim );
		Log.Trace( $"[ride] anim {(ScriptDefs.Animations)anim} ('{ChannelLetter( (ScriptDefs.Animations)anim )}', {frames} frame(s)) on {id} loop={loop}"
			+ ( frames == 0 ? " — ride has no art for this channel" : "" ) );
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
		// Looping anims (e.g. the idle/main loop) never "finish", so they never block a WAITANIM.
		return obj?.AnimId == anim && !obj.AnimLoop && Time.Now - obj.AnimStart < DurationOf( anim ) / obj.AnimSpeed;
	}

	public bool AnyAnimating()
	{
		foreach ( var obj in objects.Values )
			if ( obj.AnimId is int a && !obj.AnimLoop && Time.Now - obj.AnimStart < DurationOf( a ) / obj.AnimSpeed )
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
			if ( !obj.AnimLoop && t > DurationOf( obj.AnimId.Value ) )
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
