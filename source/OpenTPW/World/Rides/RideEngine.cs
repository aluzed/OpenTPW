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
	private SdtArchive? peepSounds;      // KidsHD.sdt — peep voices (screams live here, not RideHD)
	private int[]? screamIndices;        // indices of the scream/yell samples in peepSounds

	// Rider scream (STARTSCREAM/STOPSCREAM/SINGLESCREAM/SCREAMLEVEL — used by e.g. monkey). A sustained
	// scream re-plays the scream sound every ScreamPeriod while active; level (0..100) scales volume.
	private const float ScreamPeriod = 1.8f;
	private bool screaming;
	private int screamCode;
	private float screamGain = 0.8f;        // 0..1, from the script's level
	private float lastScreamAt = float.NegativeInfinity;

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

		// An ADDOBJ sound object's `parameter` is NOT a direct RideHD index — e.g. param -1 is a sentinel,
		// and the real code→asset binding is the ride's EventMap / .MAP catalog (deferred — T-032/T-016).
		// Playing RideHD[param%N] here produced wrong clips spammed every cycle (param -1 → Crunch.mp2,
		// the "explosions"), so we register the object (for KILLOBJ) but don't play the approximate clip.
		// Real rider screams still play correctly via the scream opcodes (KidsHD).
		Log.Trace( $"[ride] ADDOBJ type={type} param={parameter} id={id} slot={slot}"
			+ ( IsSoundType( type ) ? " (sound binding deferred)" : " (kind deferred)" ) );
	}

	public void PlaySound( string name )
	{
		// A ".rse" name is a sound-event-map script (e.g. EventMap.rse) — a child script that sets up the
		// ride's EVENT→sound bindings. The EVENT effects engine that consumes those bindings is deferred
		// (T-032), so we recognise it without playing a clip (playing RideHD[offset] here was the old
		// wrong-sound bug). A plain sound name plays from the ride's banks by name.
		if ( name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) )
		{
			Log.Info( $"[ride] SPAWNSOUND sound-event map '{name}' (EVENT bindings deferred — T-032)" );
			return;
		}
		PlayNamedSound( name );
	}

	// Play a sound by file name from the ride's sound bank (RideHD). Best-effort: the catalog (T-016)
	// lists the category's banks; for now we resolve names against the ride bank.
	private void PlayNamedSound( string name )
	{
		try
		{
			var path = Path.Join( GameDir.GamePath, "data", "global", "sound", "RideHD.sdt" );
			if ( !File.Exists( path ) )
				return;
			rideSounds ??= new SdtArchive( path );
			var stem = Path.GetFileNameWithoutExtension( name );
			var track = rideSounds.soundFiles.FirstOrDefault( f =>
				f.Name.StartsWith( stem, StringComparison.OrdinalIgnoreCase ) );
			if ( track == null )
			{
				Log.Warning( $"[ride] SPAWNSOUND '{name}' not found in RideHD" );
				return;
			}
			Audio.PlaySfx( $"ride_{track.Name}", track.SoundData );
			Log.Info( $"[ride] SPAWNSOUND '{name}' -> {track.Name}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] SPAWNSOUND '{name}' failed: {e.Message}" );
		}
	}

	public void KillObject( int id )
	{
		if ( !objects.Remove( id, out var obj ) )
			return;

		// Despawn its visual parts (the Entity list isn't otherwise pruned on delete).
		foreach ( var (entity, _, _, _) in obj.Parts )
			Entity.All.Remove( entity );
	}

	/// <summary>Tear down everything this engine spawned — the ride body + all script objects, plus the
	/// light and particle-effect proxies — when the ride is sold/demolished (T-041).</summary>
	public void Despawn()
	{
		foreach ( var obj in objects.Values )
			foreach ( var (entity, _, _, _) in obj.Parts )
				Entity.All.Remove( entity );
		objects.Clear();

		foreach ( var light in lights.Values )
			if ( light.Proxy != null )
				Entity.All.Remove( light.Proxy );
		lights.Clear();

		foreach ( var (entity, _) in particleProxies )
			Entity.All.Remove( entity );
		particleProxies.Clear();
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

	public void StartScream( int code, int level )
	{
		screaming = true;
		screamCode = code;
		if ( level >= 0 )
			screamGain = Math.Clamp( level / 100f, 0f, 1f );
		PlayScream( code, screamGain ); // begin immediately; Update sustains it
		Log.Info( $"[ride] STARTSCREAM code={code} level={level}" );
	}

	public void StopScream()
	{
		screaming = false;
		Log.Info( "[ride] STOPSCREAM" );
	}

	public void SingleScream( int code, int level )
	{
		float gain = level >= 0 ? Math.Clamp( level / 100f, 0f, 1f ) : screamGain;
		PlayScream( code, gain );
		Log.Info( $"[ride] SINGLESCREAM code={code} level={level}" );
	}

	public void SetScreamLevel( int level )
	{
		if ( level >= 0 )
			screamGain = Math.Clamp( level / 100f, 0f, 1f );
	}

	// Coaster control, multiplexed by subcommand (RE'd from coaster1.rse): 1 load rider · 2 can-load? ·
	// 3 peep-wants-off? · 4 set running/broken · 5 mode/closed · 6 capacity · 7 worn · 8 init. The query
	// subcommands' Zero-flag result is set in the opcode handler so the script's load/unload loops yield
	// rather than spin; real car loading + motion needs the coaster car/track engine (T-045) — deferred.
	// Silent: coaster1's loops call this thousands of times per second, so it must not log.
	public void Coast( int sub, int arg ) { }

	// Tour-ride / bumper-kart control, multiplexed by subcommand (RE'd from op_53 / op_54). Like COAST,
	// these drive an authored car-object subsystem (tour-route cars / bumper cars). We don't model that
	// subsystem, so — exactly as the original does when the subsystem isn't live (every helper bails on
	// the `0x4a454647` magic) — commands are no-ops here and the queries return 0 (set in the handler).
	// Our car rides still show visible motion via the occupancy-driven RideVehicle (T-032). Silent: these
	// fire in tight per-tick loops, so they must not log.
	public void Tour( int sub, int arg ) { }
	public void Bump( int sub, int arg ) { }

	private readonly Dictionary<int, float> lastEventAt = new();
	private const float EventDebounce = 0.5f; // ride loops re-fire EVENT each tick — don't restart too fast

	// Ride event dispatch. EVENT(type, target, code) — RE'd end to end (handler FUN_00552615 → dispatch
	// FUN_005573d0). The corrected model (T-047): the 7 "pools" (DAT_00803a20..3c) are **sound CATEGORIES**
	// registered through the unified effect manager (DAT_00802bcc), not particle sets, so:
	//   • types **1 & 2** = positioned SOUNDS (FUN_00521e60 / FUN_00521930) — `code` is a global sound id
	//     resolved through the rides BANK,
	//   • types **3 & 4** = PARTICLE effects (FUN_0051bfc0, the REPAIREFFECT/SPARK spawner) — `code` is a
	//     `Tp2.plb` par_lib index, rendered through the decoded .PLB proxy (T-019),
	//   • types **5-9** = positioned CATEGORY sounds — 5→rides, 6→kids, 7→staff, 8→ambient, 9→ui (each its
	//     own cat_*BANK.map), `code` a global id within that category,
	//   • type **10** = a per-ride custom effect; not yet RE'd, so kept on the particle path (a visible
	//     stand-in) pending its decode.
	// (The pre-T-047 code routed all of 3-10 to particles, which mis-played the 5-9 category sounds as
	// silent/odd proxies — this split fixes that.) Both kinds are debounced so a per-tick ride loop doesn't
	// restart the clip / spam proxies. Real 3D node positioning (FUN_00556b90 / T-048) remains a follow-up.
	/// <summary>The effect kind an EVENT type selects (RE'd from FUN_005573d0, corrected in T-047): types
	/// 1-2 & 5-9 are positioned sounds, 3-4 (and the custom type 10) are particle effects, anything else is
	/// "RSSE: Unknown object type".</summary>
	public enum EventKind { Unknown, Sound, Particle }

	public static EventKind ClassifyEvent( int type ) => type switch
	{
		1 or 2 => EventKind.Sound,
		3 or 4 => EventKind.Particle,
		>= 5 and <= 9 => EventKind.Sound,
		10 => EventKind.Particle, // custom effect — particle stand-in until its handler is RE'd
		_ => EventKind.Unknown
	};

	/// <summary>The sound category an EVENT type plays through (T-047): types 1-2 & 5 → rides, 6 → kids,
	/// 7 → staff, 8 → ambient, 9 → ui. Null for non-sound types. Each maps to a <c>cat_*BANK.map</c>.</summary>
	public static string? EventSoundCategory( int type ) => type switch
	{
		1 or 2 or 5 => "rides",
		6 => "kids",
		7 => "staff",
		8 => "ambient",
		9 => "ui",
		_ => null
	};

	/// <summary>Resolves this ride's node ids to world positions (T-048/T-047) — set by <see cref="Ride"/>
	/// when the model loads. EVENT effects + REPAIREFFECT/SPARK spawn at the addressed node through it.</summary>
	public RideNodePositions? NodeField { get; set; }

	// World position of an addressed ride node (T-048): the node's resolved position (a live car/seat
	// position, or the footprint layout), else the ride body — so a script with no decoded node graph
	// behaves exactly as before (effect at the ride centre).
	private Vector3 NodePosition( int nodeId )
		=> NodeField != null && NodeField.TryResolve( nodeId, out var p ) ? p : LightPosition( SelfId );

	public void Event( int type, int p1, int p2 )
	{
		// p1 = target node id (where the effect/sound plays); p2 = code (a sound id or par_lib particle index).
		switch ( ClassifyEvent( type ) )
		{
			case EventKind.Sound:
				PlayEventSound( type, p2, p1 );
				break;
			case EventKind.Particle:
				if ( !EventDebounced( $"fx{type}:{p2}" ) )
					SpawnParticleEffect( p2, p1 );
				break;
		}
	}

	private void PlayEventSound( int type, int code, int node )
	{
		var category = EventSoundCategory( type );
		if ( category == null )
			return;
		var track = CategoryBank( category )?.Resolve( code );
		if ( track == null )
			return;
		if ( EventDebounced( $"snd:{category}:{code}" ) )
			return;
		// The node world position is resolved (T-048) so positional playback can use it once the audio
		// bus is 3D; the current mixer is 2D, so it's recorded rather than spatialised.
		var pos = NodePosition( node );
		Audio.PlaySfx( $"ev_{track.Name}", track.SoundData );
		Log.Trace( $"[ride] EVENT t{type} cat={category} code={code} node={node}@{pos} -> {track.Name}" );
	}

	// True if this event key fired within the debounce window (and refreshes it when it hasn't).
	private bool EventDebounced( string key )
	{
		var k = key.GetHashCode();
		if ( Time.Now - lastEventAt.GetValueOrDefault( k, float.NegativeInfinity ) < EventDebounce )
			return true;
		lastEventAt[k] = Time.Now;
		return false;
	}

	// Per-category sound registries (global sound ids → samples), one per EVENT sound category and built
	// once from that category's BANK catalog (cat_<category>BANK.map). Cached (incl. a null = "no catalog")
	// so a missing/headless install is only probed once. See EventSoundCategory + T-047.
	private static readonly Dictionary<string, RideSoundBank?> categoryBanks = new();
	private static RideSoundBank? CategoryBank( string category )
	{
		if ( !categoryBanks.TryGetValue( category, out var bank ) )
		{
			bank = RideSoundBank.FromBankCatalog(
				Path.Join( GameDir.GamePath, "data", "global", "sound", $"cat_{category}BANK.map" ) );
			categoryBanks[category] = bank;
		}
		return bank;
	}

	//
	// Particle effects (opcodes REPAIREFFECT/SPARK, RE'd from op_93/op_105 → the spawner FUN_0051bfc0).
	// The original instantiates a .PLB effect at a 3D position. Our renderer has no particle system, so we
	// use the **decoded .PLB** (T-019): look the effect up by its par_lib code, take a representative colour
	// from its colour ramp, and spawn a short-lived emissive proxy of that colour at the ride. A visible,
	// .PLB-driven stand-in; a real GPU particle system is a renderer follow-up.
	//
	private static ParticleLibraryFile? particleLib;
	private static bool particleLibTried;
	private static ParticleLibraryFile? ParticleLib
	{
		get
		{
			if ( !particleLibTried )
			{
				particleLibTried = true;
				try
				{
					// Through the VFS (rooted at the install's data/), case-insensitive (T-014).
					using var s = FileSystem.OpenRead( "Particle/Tp2.plb" );
					if ( s != null )
						particleLib = new ParticleLibraryFile( s );
				}
				catch ( Exception e ) { Log.Warning( $"[ride] particle library load failed: {e.Message}" ); }
			}
			return particleLib;
		}
	}

	private const float ParticleProxyLife = 1.2f; // seconds an effect proxy stays before fading out
	private readonly List<(ModelEntity Entity, float Expire)> particleProxies = new();
	private float lastNow; // most recent Update time, for timing proxy expiry

	// Particle effect at the ride centre (no addressed node) — the Repair() effect + any caller without a
	// node. Sits high over the ride body, as before.
	public void SpawnParticleEffect( int effectCode )
		=> SpawnParticleEffectAt( effectCode, LightPosition( SelfId ) + Vector3.Up * 8f );

	// Particle effect at a ride node's world position (REPAIREFFECT/SPARK + EVENT particles — T-048/T-047).
	// Resolves the node (a live car/seat position, or the footprint layout), lifting it slightly so the
	// proxy reads above the geometry; falls back to the ride centre when the node can't be placed.
	public void SpawnParticleEffect( int effectCode, int nodeId )
		=> SpawnParticleEffectAt( effectCode, NodePosition( nodeId ) + Vector3.Up * 2f );

	private void SpawnParticleEffectAt( int effectCode, Vector3 position )
	{
		var (name, r, g, b) = ResolveEffectColour( effectCode );
		if ( name == "?" )
		{
			// Code outside the decoded Tp2.plb (e.g. a per-ride custom/other-pool effect we don't have) —
			// don't render a meaningless white proxy; just record it. See T-037 (per-type pool mapping).
			Log.Trace( $"[ride] particle effect {effectCode} (unresolved)" );
			return;
		}
		try
		{
			var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
				MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
			mat.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
			var proxy = new ModelEntity
			{
				Model = Primitives.Cube.GenerateModel( mat ),
				Scale = new Vector3( 2.5f ),
				Position = position,
			};
			particleProxies.Add( (proxy, lastNow + ParticleProxyLife) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] particle effect {effectCode} proxy failed: {e.Message}" );
		}
		Log.Info( $"[ride] particle effect {effectCode} ({name}) @{position}" );
	}

	// The effect's name + a representative colour (its brightest ramp stop) from the decoded .PLB (T-019);
	// a white fallback if the library/effect isn't available.
	private static (string Name, byte R, byte G, byte B) ResolveEffectColour( int effectCode )
	{
		var lib = ParticleLib;
		if ( lib == null || effectCode < 0 || effectCode >= lib.Effects.Count )
			return ("?", 255, 255, 255);

		var fx = lib.Effects[effectCode];
		if ( fx.ColorRamp.Count == 0 )
			return (fx.Name, 255, 255, 255);

		var stop = fx.ColorRamp.OrderByDescending( c => c.R + c.G + c.B ).First();
		return (fx.Name, stop.R, stop.G, stop.B);
	}

	private void ExpireParticleProxies( float now )
	{
		lastNow = now;
		for ( var i = particleProxies.Count - 1; i >= 0; i-- )
		{
			if ( now < particleProxies[i].Expire )
				continue;
			Entity.All.Remove( particleProxies[i].Entity );
			particleProxies.RemoveAt( i );
		}
	}

	public void SetReverb( int level ) => Log.Trace( $"[ride] SETREVERB {level}" );

	public void DipMusic( int amount ) => Log.Trace( $"[ride] DIPMUSIC {amount}" );

	//
	// Ride lights (opcodes ENABLELIGHT/DISABLELIGHT/SETLIGHT/COLOURLIGHT, RE'd from op_82..op_85). The
	// original toggles a light object's enable flag and sets its colour×intensity on the scene light. Our
	// renderer is unlit (no dynamic per-pixel lighting), so each enabled light shows a small **emissive
	// colour proxy** at its position — a visible, verifiable stand-in. Real scene lighting is a renderer
	// follow-up. State is kept regardless so the proxy reflects the latest colour/brightness/enable.
	//
	private sealed class RideLight
	{
		public bool Enabled;
		public float R = 1f, G = 1f, B = 1f; // colour 0..1
		public float Brightness = 1f;        // intensity 0..1
		public ModelEntity? Proxy;           // emissive marker, present only while enabled
	}

	private readonly Dictionary<int, RideLight> lights = new();

	/// <summary>How many ride lights are currently enabled (diagnostics / tests).</summary>
	public int EnabledLightCount => lights.Values.Count( l => l.Enabled );

	public void EnableLight( int id )
	{
		var light = GetLight( id );
		light.Enabled = true;
		UpdateLightProxy( id, light );
		Log.Info( $"[ride] ENABLELIGHT {id} (now {EnabledLightCount} on)" );
	}

	public void DisableLight( int id )
	{
		var light = GetLight( id );
		light.Enabled = false;
		UpdateLightProxy( id, light );
		Log.Info( $"[ride] DISABLELIGHT {id} (now {EnabledLightCount} on)" );
	}

	public void SetLight( int id, float brightness )
	{
		var light = GetLight( id );
		light.Brightness = Math.Clamp( brightness, 0f, 1f );
		UpdateLightProxy( id, light );
	}

	public void ColourLight( int id, float r, float g, float b )
	{
		var light = GetLight( id );
		light.R = Math.Clamp( r, 0f, 1f );
		light.G = Math.Clamp( g, 0f, 1f );
		light.B = Math.Clamp( b, 0f, 1f );
		UpdateLightProxy( id, light );
	}

	private RideLight GetLight( int id ) =>
		lights.TryGetValue( id, out var l ) ? l : lights[id] = new RideLight();

	// Show/refresh (or hide) the emissive colour proxy for a light. Best-effort: any renderer failure
	// (e.g. headless) is swallowed so the VM keeps running and the light state stays correct.
	private void UpdateLightProxy( int id, RideLight light )
	{
		try
		{
			if ( !light.Enabled )
			{
				if ( light.Proxy != null )
				{
					Entity.All.Remove( light.Proxy );
					light.Proxy = null;
				}
				return;
			}

			byte R = (byte)(Math.Clamp( light.R * light.Brightness, 0f, 1f ) * 255f);
			byte G = (byte)(Math.Clamp( light.G * light.Brightness, 0f, 1f ) * 255f);
			byte B = (byte)(Math.Clamp( light.B * light.Brightness, 0f, 1f ) * 255f);

			var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
			mat.Set( "Color", new Texture( [R, G, B, 255], 1, 1 ) );
			light.Proxy ??= new ModelEntity { Scale = new Vector3( 3f ) };
			light.Proxy.Model = Primitives.Cube.GenerateModel( mat );
			light.Proxy.Position = LightPosition( id ) + Vector3.Up * 6f;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] light {id} proxy failed: {e.Message}" );
		}
	}

	// A light's world position: its own object's first part if it has one, else the ride body, else origin.
	private Vector3 LightPosition( int id )
	{
		if ( objects.TryGetValue( id, out var obj ) && obj.Parts.Count > 0 )
			return obj.Parts[0].Entity.Position;
		if ( objects.TryGetValue( SelfId, out var body ) && body.Parts.Count > 0 )
			return body.Parts[0].Entity.Position;
		return Vector3.Zero;
	}

	// Play an actual peep scream at the given volume, and stamp the time so a sustained scream paces its
	// re-triggers. Screams are peep voices in KidsHD.sdt (sceem*/screem*/yell*/whoop*), NOT RideHD — the
	// script's scream code (0) is not an index into a sound bank, so we pick a scream sample by name.
	// (Playing RideHD[code] before gave Backfire/Crunch — the "explosions/gunshots" bug.)
	private void PlayScream( int code, float gain )
	{
		lastScreamAt = Time.Now;
		try
		{
			var path = Path.Join( GameDir.GamePath, "data", "global", "sound", "KidsHD.sdt" );
			if ( !File.Exists( path ) )
				return;
			peepSounds ??= new SdtArchive( path );
			screamIndices ??= peepSounds.soundFiles
				.Select( ( f, i ) => (f.Name, i) )
				.Where( x => x.Name.Contains( "sceem", StringComparison.OrdinalIgnoreCase )
					|| x.Name.Contains( "screem", StringComparison.OrdinalIgnoreCase )
					|| x.Name.Contains( "yell", StringComparison.OrdinalIgnoreCase )
					|| x.Name.Contains( "whoop", StringComparison.OrdinalIgnoreCase ) )
				.Select( x => x.i )
				.ToArray();
			if ( screamIndices.Length == 0 )
				return;

			var track = peepSounds.soundFiles[screamIndices[Random.Shared.Next( screamIndices.Length )]];
			Audio.PlaySfx( $"scream_{track.Name}", track.SoundData, gain );
			Log.Info( $"[ride] scream -> {track.Name}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] scream failed: {e.Message}" );
		}
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

		// The real-world length of one full pass of the running loop (its decoded keyframe track), used
		// as the authentic per-ride ride duration. Null when the loop has no decoded keyframes.
		BodyLoopDuration = chosen >= 0 && channelAnims.TryGetValue( chosen, out var loopKf ) && loopKf.Duration > 0
			? loopKf.Duration / KeyframeRate
			: null;

		if ( chosen >= 0 )
			Log.Info( $"[ride] body loop {(ScriptDefs.Animations)chosen}, idle "
				+ ( bodyIdleAnim >= 0 ? $"{(ScriptDefs.Animations)bodyIdleAnim}" : "rest" ) + " (keyframed)" );

		GoIdle( body );
	}

	private int bodyLoopAnim = -1;
	private int bodyIdleAnim = -1;
	private bool bodyRunning;

	/// <summary>Real-world seconds for one full pass of the ride's running loop animation (null if it has
	/// no decoded keyframes). The owning Ride uses it as the authentic ride duration.</summary>
	public float? BodyLoopDuration { get; private set; }

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
		// Sustain an active scream by re-playing it each period (PlaySfx is one-shot; there's no looping
		// sfx source). STOPSCREAM clears `screaming`.
		if ( screaming && now - lastScreamAt >= ScreamPeriod )
			PlayScream( screamCode, screamGain );

		ExpireParticleProxies( now ); // remove finished particle-effect proxies

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
}
