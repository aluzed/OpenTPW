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
	// Keyframe time units advanced per second. The original computes animTime = elapsed_ms * 0.03 * speed
	// (FUN_004735d0; _DAT_006fec0c = 0.03 ≈ 1/33.33, the 30 FPS frame time), i.e. keyframe times are in
	// 30 FPS frames — so the clock advances 30 units/second. (RE'd, see docs/08-ghidra-animation.md.)
	private const float KeyframeRate = 30f;

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

	// Frame count for an animation channel (0 if the ride doesn't ship it), and the playback duration.
	// A channel with decoded keyframes runs for its real track length (Duration is in keyframe time
	// units; the clock advances at KeyframeRate per second), so a one-shot (Load/Start/End) plays in
	// full before the cycle advances. Channels with no decoded keyframes fall back to a per-frame
	// placeholder length.
	private int FrameCount( int anim ) => channelFrames.TryGetValue( anim, out var n ) ? n : 0;
	private float DurationOf( int anim ) =>
		channelAnims.TryGetValue( anim, out var kf ) && kf.Duration > 0
			? kf.Duration / KeyframeRate
			: Math.Max( FrameCount( anim ), 1 ) * FrameSeconds;

	// Registers the ride's loaded meshes as the body, and starts its looping motion so it visibly
	// moves. The looping channel is Main (the original's primary ride motion) when the ride ships it,
	// otherwise Idle — see docs/08-ghidra-animation.md.
	public void RegisterBody( IEnumerable<ModelEntity> parts )
	{
		var body = new RideObject { Id = SelfId, Type = -1 };
		int globalOffset = 0;
		foreach ( var e in parts )
		{
			body.Parts.Add( (e, e.Position, e.Rotation, e.Scale) );

			// Capture the rest-pose vertex positions + this part's global vertex start, so vertex-morph
			// (which addresses one combined buffer across all parts) can map its global slots to parts.
			var verts = e.Model?.Vertices ?? System.Array.Empty<Vertex>();
			var basePos = new Vector3[verts.Length];
			for ( int i = 0; i < verts.Length; i++ )
				basePos[i] = verts[i].Position;
			body.PartBaseVerts.Add( basePos );
			body.PartVertexOffset.Add( globalOffset );
			globalOffset += verts.Length;
		}

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
		foreach ( var (entity, _, _, _) in obj.Parts )
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

	/// <summary>
	/// Supplies the decoded keyframe tracks for an animation channel (the ride's <c>&lt;base&gt;&lt;c&gt;[n].md2</c>
	/// files, parsed by <see cref="RideKeyframeFile"/>). When that channel is the active animation, the
	/// engine drives each animated surface's part by its real rotation track instead of the bob.
	/// </summary>
	public void SetChannelKeyframes( int anim, RideKeyframeFile keyframes )
	{
		if ( keyframes.Surfaces.Count > 0 )
			channelAnims[anim] = keyframes;
	}

	/// <summary>
	/// Chooses the body's running loop + idle channels once keyframes are loaded, and leaves the body
	/// idle (occupancy drives whether it runs — see <see cref="SetActive"/>). The running loop prefers
	/// Main, then Idle, otherwise any channel with real keyframe data (so a ride that only ships e.g.
	/// Create still animates); the idle loop is the ride's Idle channel when it ships one (else the body
	/// rests). Called by the Ride after <see cref="SetChannelKeyframes"/>.
	/// </summary>
	public void StartBestBodyAnim()
	{
		if ( !objects.TryGetValue( SelfId, out var body ) )
			return;

		int chosen = -1;
		foreach ( var pref in new[] { (int)ScriptDefs.Animations.ANIM_Main, (int)ScriptDefs.Animations.ANIM_Idle } )
			if ( channelAnims.ContainsKey( pref ) ) { chosen = pref; break; }

		if ( chosen < 0 && channelAnims.Count > 0 )
			chosen = channelAnims.Keys.OrderBy( k => k ).First();

		bodyLoopAnim = chosen;
		// Idle loop for an empty ride: only if the ride ships an Idle channel (else it rests).
		bodyIdleAnim = FrameCount( (int)ScriptDefs.Animations.ANIM_Idle ) > 0 ? (int)ScriptDefs.Animations.ANIM_Idle : -1;

		if ( chosen >= 0 )
			Log.Info( $"[ride] body loop {(ScriptDefs.Animations)chosen}, idle "
				+ ( bodyIdleAnim >= 0 ? $"{(ScriptDefs.Animations)bodyIdleAnim}" : "rest" ) + " (keyframed)" );

		GoIdle( body );
	}

	private int bodyLoopAnim = -1;
	private int bodyIdleAnim = -1;
	private bool bodyRunning;

	// The body's pending animation stages (the rest of the boarding/unloading cycle after the stage it
	// is currently playing). Update advances through these as each one-shot stage finishes.
	private readonly Queue<(int Anim, bool Loop)> bodyQueue = new();

	/// <summary>
	/// Runs (true) or idles (false) the ride body, tying its animation to occupancy. The first rider
	/// starts the boarding cycle (Load → Start → Main loop); emptying runs the unloading cycle (End →
	/// Unload → Idle loop, or rest). Stages the ride doesn't ship are skipped, so a ride with no cycle
	/// art simply switches between its run loop and idle. Idempotent while occupied (a second rider
	/// boarding does not restart the cycle).
	/// </summary>
	public void SetActive( bool active )
	{
		if ( !objects.TryGetValue( SelfId, out var body ) )
			return;

		if ( active )
		{
			if ( bodyRunning )
				return; // already running — another rider joining must not restart the cycle
			bodyRunning = true;
			var seq = BuildCycle( running: true );
			Log.Trace( $"[ride] boarding cycle: {DescribeCycle( seq )}" );
			PlayBodySequence( body, seq );
		}
		else if ( bodyRunning )
		{
			bodyRunning = false;
			var seq = BuildCycle( running: false );
			Log.Trace( $"[ride] unloading cycle: {DescribeCycle( seq )}" );
			PlayBodySequence( body, seq );
		}
		else
		{
			GoIdle( body ); // not running (initial setup / already empty): just settle into idle
		}
	}

	// Builds the body's animation cycle. Running: optional Load/Start lead-ins then the run loop.
	// Stopping: optional End/Unload outros then the idle loop (or rest). Only stages the ride actually
	// ships are included.
	private List<(int Anim, bool Loop)> BuildCycle( bool running )
	{
		var seq = new List<(int Anim, bool Loop)>();
		void AddIfShipped( ScriptDefs.Animations a )
		{
			if ( FrameCount( (int)a ) > 0 )
				seq.Add( ((int)a, false) );
		}

		if ( running )
		{
			AddIfShipped( ScriptDefs.Animations.ANIM_Load );
			AddIfShipped( ScriptDefs.Animations.ANIM_Start );
			if ( bodyLoopAnim >= 0 )
				seq.Add( (bodyLoopAnim, true) );
		}
		else
		{
			AddIfShipped( ScriptDefs.Animations.ANIM_End );
			AddIfShipped( ScriptDefs.Animations.ANIM_Unload );
			if ( bodyIdleAnim >= 0 )
				seq.Add( (bodyIdleAnim, true) );
		}
		return seq;
	}

	private static string DescribeCycle( List<(int Anim, bool Loop)> seq ) =>
		seq.Count == 0 ? "rest" : string.Join( " -> ", seq.Select( s => $"{(ScriptDefs.Animations)s.Anim}{( s.Loop ? "(loop)" : "" )}" ) );

	// Starts the first stage of a cycle now and queues the rest; an empty cycle rests the body.
	private void PlayBodySequence( RideObject body, List<(int Anim, bool Loop)> seq )
	{
		bodyQueue.Clear();
		if ( seq.Count == 0 )
		{
			StopAnim( body );
			return;
		}

		StartAnim( body, seq[0].Anim, seq[0].Loop );
		for ( int i = 1; i < seq.Count; i++ )
			bodyQueue.Enqueue( seq[i] );
	}

	// Puts the body into its empty-ride state directly (no End/Unload outro): the idle loop if shipped,
	// otherwise the rest pose.
	private void GoIdle( RideObject body )
	{
		bodyQueue.Clear();
		if ( bodyIdleAnim >= 0 )
			StartAnim( body, bodyIdleAnim, loop: true );
		else
			StopAnim( body );
	}

	private readonly Dictionary<int, RideKeyframeFile> channelAnims = new();

	/// <summary>Per-frame animation: drive real keyframe tracks where we have them, else the placeholder bob.</summary>
	public void Update( float now )
	{
		foreach ( var obj in objects.Values )
		{
			if ( obj.AnimId == null || obj.Parts.Count == 0 )
				continue;

			var anim = obj.AnimId.Value;
			var t = ( now - obj.AnimStart ) * obj.AnimSpeed;
			if ( !obj.AnimLoop && t > DurationOf( anim ) )
			{
				// The ride body steps through its boarding/unloading cycle: when a one-shot stage
				// (Load/Start/End/Unload) finishes, advance to the next queued stage rather than stopping.
				if ( obj.Id == SelfId && bodyQueue.Count > 0 )
				{
					var (next, loop) = bodyQueue.Dequeue();
					StartAnim( obj, next, loop );
				}
				else
					StopAnim( obj );
				continue;
			}

			if ( channelAnims.TryGetValue( anim, out var kf ) )
				ApplyKeyframes( obj, kf, t );
			else
				ApplyBob( obj, t );
		}
	}

	// Drives each animated surface's part by its keyframe tracks. The model-space TRS is swizzled to
	// our world (Y/Z swapped — the same swizzle the loader applies to the mesh transform; rotation W
	// is negated to match) and composed onto the part's loaded base transform. Each track loops over
	// its own last-key time (looping anims) or clamps to its end (one-shots), so a short rotation
	// track keeps spinning while a full-length identity scale track stays a no-op.
	private void ApplyKeyframes( RideObject obj, RideKeyframeFile kf, float elapsed )
	{
		var clock = elapsed * KeyframeRate;

		foreach ( var sa in kf.Surfaces )
		{
			if ( sa.SurfaceIndex < 0 || sa.SurfaceIndex >= obj.Parts.Count || !sa.HasAnimation )
				continue;

			var (entity, basePos, baseRot, baseScale) = obj.Parts[sa.SurfaceIndex];

			if ( sa.Rotation.Count > 0 )
			{
				var q = RideKeyframeFile.SampleRotation( sa.Rotation, TrackTime( clock, sa.Rotation[^1].Time, obj.AnimLoop ) );
				entity.Rotation = baseRot * new System.Numerics.Quaternion( q.X, q.Z, q.Y, -q.W );
			}

			if ( sa.Scale.Count > 0 )
			{
				var s = RideKeyframeFile.SampleVector( sa.Scale, TrackTime( clock, sa.Scale[^1].Time, obj.AnimLoop ), System.Numerics.Vector3.One );
				entity.Scale = new Vector3( baseScale.X * s.X, baseScale.Y * s.Z, baseScale.Z * s.Y );
			}

			if ( sa.Translation.Count > 0 )
			{
				var tr = RideKeyframeFile.SampleVector( sa.Translation, TrackTime( clock, sa.Translation[^1].Time, obj.AnimLoop ), System.Numerics.Vector3.Zero );
				entity.Position = basePos + new Vector3( tr.X, tr.Z, tr.Y );
			}
		}

		ApplyMorph( obj, kf, clock );
	}

	// Vertex-morph: a global additive blend-shape over one combined vertex buffer (see
	// docs/08-ghidra-animation.md). Resets each touched part to its rest pose, then writes the
	// keyframed positions (model space → world Y/Z-swizzled, the same swizzle the loader applies to
	// vertices) at the morph's global vertex slots, and re-uploads only the parts that changed.
	private void ApplyMorph( RideObject obj, RideKeyframeFile kf, float clock )
	{
		Span<bool> touched = obj.Parts.Count <= 64 ? stackalloc bool[obj.Parts.Count] : new bool[obj.Parts.Count];

		foreach ( var sa in kf.Surfaces )
		{
			foreach ( var m in sa.Morph )
			{
				if ( m.VertexSlots.Length == 0 || m.Times.Length == 0 )
					continue;

				var t = TrackTime( clock, m.Times[^1], obj.AnimLoop );
				for ( int i = 0; i < m.VertexSlots.Length; i++ )
				{
					if ( !TryMapSlot( obj, m.VertexSlots[i], out var part, out var local ) )
						continue;

					if ( !touched[part] )
					{
						ResetPartVerts( obj, part );
						touched[part] = true;
					}

					var p = m.Sample( i, t );
					obj.Parts[part].Entity.Model!.Vertices[local].Position = new Vector3( p.X, p.Z, p.Y );
				}
			}
		}

		for ( int part = 0; part < touched.Length; part++ )
			if ( touched[part] )
				obj.Parts[part].Entity.Model?.UploadVertices();
	}

	// Maps a global morph vertex slot to its part + local index (parts hold contiguous global ranges).
	private static bool TryMapSlot( RideObject obj, int slot, out int part, out int local )
	{
		for ( int p = obj.Parts.Count - 1; p >= 0; p-- )
		{
			if ( slot >= obj.PartVertexOffset[p] )
			{
				local = slot - obj.PartVertexOffset[p];
				part = p;
				var verts = obj.Parts[p].Entity.Model?.Vertices;
				return verts != null && local < verts.Length;
			}
		}
		part = local = -1;
		return false;
	}

	private static void ResetPartVerts( RideObject obj, int part )
	{
		var verts = obj.Parts[part].Entity.Model?.Vertices;
		var basePos = obj.PartBaseVerts[part];
		if ( verts == null )
			return;
		for ( int i = 0; i < verts.Length && i < basePos.Length; i++ )
			verts[i].Position = basePos[i];
	}

	// Maps the animation clock onto a single track's [0, last] domain: wrap for a looping anim, clamp
	// for a one-shot. A zero-length track collapses to 0.
	private static float TrackTime( float clock, float last, bool loop ) =>
		last <= 0 ? 0f : ( loop ? clock % last : MathF.Min( clock, last ) );

	private static void ApplyBob( RideObject obj, float t )
	{
		// Procedural placeholder for channels we have no decoded keyframes for: a gentle vertical bob.
		var bob = MathF.Sin( t * 2f ) * BobAmplitude;
		foreach ( var (entity, basePos, _, _) in obj.Parts )
			entity.Position = basePos + new Vector3( 0, 0, bob );
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
		foreach ( var (entity, basePos, baseRot, baseScale) in o.Parts )
		{
			entity.Position = basePos;
			entity.Rotation = baseRot;
			entity.Scale = baseScale;
		}
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
