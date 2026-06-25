using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A ride instance: loads its RSE script + settings + model from the ride's `.wad`, drives the
/// script through a <see cref="RideEngine"/>, and renders the ride's geometry. Slice 1 of the ride
/// engine — the model is static and engine opcodes cover sound (see the plan / docs/tickets/T-007).
/// </summary>
public class Ride : Entity
{
	public RideVM VM { get; private set; }

	/// <summary>The ride's tile footprint (from its <c>.sam</c> Info.Shape) — how many grid tiles it occupies.</summary>
	public RideShape Shape { get; }

	/// <summary>Fractional position within the entrance cell where queueing peeps stand (default centre).</summary>
	public (float X, float Y) EntryStandPos { get; private set; } = (0.5f, 0.5f);

	/// <summary>Fractional position within the exit cell where peeps appear (default centre).</summary>
	public (float X, float Y) ExitAppearPos { get; private set; } = (0.5f, 0.5f);

	/// <summary>One research/upgrade level from the ride's <c>Upgrades[*]</c> table.</summary>
	public readonly record struct UpgradeLevelInfo( int Capacity, float CostResearch, float CostUpgrade );

	private int maxCapacity = 8;
	private readonly List<UpgradeLevelInfo> upgrades = new();

	/// <summary>How many peeps the ride holds at once — the current upgrade level's capacity
	/// (<c>Upgrades[level].InitCapacity</c>), falling back to <c>UsageInfo.MaxCapacity</c>.</summary>
	public int Capacity => upgrades.Count > 0 ? upgrades[Math.Min( UpgradeLevel, upgrades.Count - 1 )].Capacity : maxCapacity;

	/// <summary>The ride's available upgrade levels (level 0 = as built).</summary>
	public IReadOnlyList<UpgradeLevelInfo> Upgrades => upgrades;

	/// <summary>Current applied upgrade level, and the highest level researched (≥ <see cref="UpgradeLevel"/>).</summary>
	public int UpgradeLevel { get; private set; }
	public int ResearchedLevel { get; private set; }

	private const float ResearchDuration = 12f; // researcher-seconds to unlock the next level
	private bool researching;
	private float researchProgress;

	public bool HasNextLevel => UpgradeLevel + 1 < upgrades.Count;
	public bool NextResearched => ResearchedLevel > UpgradeLevel;
	public bool IsResearching => researching;
	public float ResearchFraction => researching ? Math.Clamp( researchProgress / ResearchDuration, 0f, 1f ) : 0f;
	public float NextResearchCost => HasNextLevel ? upgrades[UpgradeLevel + 1].CostResearch : 0f;
	public float NextUpgradeCost => HasNextLevel ? upgrades[UpgradeLevel + 1].CostUpgrade : 0f;

	/// <summary>Begin researching the next level (caller pays <see cref="NextResearchCost"/>).</summary>
	public void StartResearch()
	{
		if ( HasNextLevel && !NextResearched && !researching )
		{
			researching = true;
			researchProgress = 0f;
		}
	}

	/// <summary>Advance research by <paramref name="researchers"/> researcher-seconds.</summary>
	public void TickResearch( float dt, int researchers )
	{
		if ( !researching || researchers <= 0 )
			return;
		researchProgress += dt * researchers;
		if ( researchProgress >= ResearchDuration )
		{
			ResearchedLevel = UpgradeLevel + 1;
			researching = false;
			researchProgress = 0f;
		}
	}

	/// <summary>Apply the researched next level (caller pays <see cref="NextUpgradeCost"/>); bumps capacity.</summary>
	public void ApplyUpgrade()
	{
		if ( NextResearched )
			UpgradeLevel++;
	}

	/// <summary>The ride's duration band (<c>Info.DurationUnit</c>, 0–3 — indexes a seconds table in TP.EXE).</summary>
	public int DurationUnit { get; private set; }

	/// <summary>How long a single ride lasts, in seconds (peeps stay aboard this long). Anchored to the
	/// ride's own running animation length — see the ctor.</summary>
	public float RideDuration { get; private set; } = 5f;

	/// <summary>How exciting the ride is (<c>UsageInfo.ExcitementLevel</c>) — its draw: peeps pick rides
	/// weighted by this, so more exciting rides build longer queues.</summary>
	public int Excitement { get; private set; } = 50;

	// Ride rating (T-050): a running average of how satisfied riders were (0..100), seeded from Excitement.
	// Peeps weight their ride choice by this, and it feeds back into park rating via rider happiness. A
	// well-priced, reliable, exciting ride climbs; an overpriced or unreliable one sinks.
	private float ratingSum;
	private int ratingCount;

	/// <summary>Average rider satisfaction 0..100 (the ride's reputation); = <see cref="Excitement"/>
	/// until anyone has ridden.</summary>
	public float Rating => ratingCount == 0 ? Excitement : ratingSum / ratingCount;

	/// <summary>Record one rider's satisfaction into the running rating (called when a peep finishes).</summary>
	public void RegisterRideExperience( float satisfaction )
	{
		ratingSum += Math.Clamp( satisfaction, 0f, 100f );
		ratingCount++;
	}

	/// <summary>The ride's base attraction value (<c>Info.AttractionValue</c>).</summary>
	public int Attraction { get; private set; } = 25;

	/// <summary>What a peep pays to board, in coins. Player-settable (T-042); defaults to a value
	/// derived from the ride's excitement (more exciting rides can charge more).</summary>
	public float TicketPrice { get; set; } = 5f;

	/// <summary>The ride's running cost per second (its money sink), scaled by how many it seats.</summary>
	public float UpkeepPerSecond => Capacity * 0.1f;

	/// <summary>The footprint this ride occupies on the placement grid (set when placed) — used to
	/// select it by clicking a covered tile.</summary>
	public int TileX { get; set; }
	public int TileY { get; set; }
	public int TileW { get; set; }
	public int TileH { get; set; }

	/// <summary>True if the grid tile (tx,ty) is within this ride's footprint.</summary>
	public bool Covers( int tx, int ty ) => tx >= TileX && tx < TileX + TileW && ty >= TileY && ty < TileY + TileH;

	/// <summary>This ride's boarding queue (set when the queue is created), and its live rider count —
	/// used by the coaster train to carry visible riders and run only while occupied.</summary>
	public RideQueue? Queue { get; internal set; }
	public int Riders => Queue?.Riders ?? 0;

	// Reliability & breakdown (T-032/T-039): reliability wears down while the ride carries riders; at 0 it
	// breaks down — stops boarding + running until a mechanic repairs it. Wear is tuned for visible dev
	// breakdowns; a real balance pass would slow it (and scale by upgrade level / age).
	private const float WearPerSecond = 0.04f; // ~25 s of occupied running before a breakdown

	/// <summary>Mechanical condition, 1 = perfect, 0 = broken down.</summary>
	public float Reliability { get; private set; } = 1f;

	/// <summary>True while the ride is broken down — it stops boarding + running until repaired.</summary>
	public bool IsBroken { get; private set; }

	/// <summary>Park-wide breakdown / repair tallies (diagnostics + HUD).</summary>
	public static int Breakdowns { get; private set; }
	public static int Repairs { get; private set; }

	private void BreakDown()
	{
		if ( IsBroken )
			return;
		IsBroken = true;
		Reliability = 0f;
		Breakdowns++;
		SetActive( false ); // halt the ride animation/cycle
		Log.Info( $"[ride] {Name} broke down (total {Breakdowns})" );
	}

	/// <summary>Repair a broken-down ride (a mechanic, see <see cref="Staff"/>): restore it and play the
	/// repair particle effect at the ride.</summary>
	public void Repair()
	{
		if ( !IsBroken )
			return;
		IsBroken = false;
		Reliability = 1f;
		Repairs++;
		engine.SpawnParticleEffect( 51 ); // P_EFFECT_Repair (T-019 .PLB / T-007)
		Log.Info( $"[ride] {Name} repaired (total {Repairs})" );
	}

	/// <summary>The coaster train running this ride's player-laid track, if any (set by
	/// <see cref="CoasterTrack"/>). A boarding peep rides it in view instead of being hidden (T-045 3b).</summary>
	public CoasterTrain? Train { get; internal set; }

	/// <summary>The coaster track laid for this ride, if any (set by <see cref="CoasterTrack"/>) — torn
	/// down with the ride on sell/demolish.</summary>
	public CoasterTrack? Track { get; internal set; }

	/// <summary>A generic moving car for "car" rides (set by the placement code) — torn down with the ride.</summary>
	public RideVehicle? Vehicle { get; internal set; }

	/// <summary>How many car/seat nodes the ride's authored model declares (object 0x80 + car 0x100 nodes
	/// in its node graph — e.g. Bird's nine per-seat nodes). Drives the visible seat/car count on the
	/// <see cref="RideVehicle"/> instead of a fixed guess (T-048). 0 when the model carries no node graph.</summary>
	public int CarNodeCount { get; private set; }

	/// <summary>Resolves this ride's node ids to world positions (T-048/T-047): car/seat nodes follow the
	/// <see cref="RideVehicle"/> path, other nodes sit at a footprint-derived layout. EVENT effects and
	/// REPAIREFFECT/SPARK spawn at the addressed node via this instead of the ride centre. Built when the
	/// model loads (empty until then); placed by Level.SpawnRideAt.</summary>
	public RideNodePositions NodeField { get; private set; } = new( null );

	/// <summary>True if this is a car ride — its script drives cars via the <c>TOUR</c>/<c>BUMP</c> opcodes
	/// (tour rides, go-karts, water rides, bumpers). Such a ride gets a visible <see cref="RideVehicle"/>.</summary>
	public bool IsCarRide => VM != null && VM.Instructions.Any( i => i.opcode is Opcode.TOUR or Opcode.BUMP );

	/// <summary>True if this is a <b>bumper/dodgem</b> ride — its script drives the cars via the <c>BUMP</c>
	/// opcode. Its <see cref="RideVehicle"/> runs the arena collision sim (<see cref="CarSim"/>) instead of
	/// the tour/kart circuit loop (T-048 / docs/08).</summary>
	public bool IsBumperRide => VM != null && VM.Instructions.Any( i => i.opcode is Opcode.BUMP );

	/// <summary>What the player paid to build this ride — used to compute the sell refund (T-041).</summary>
	public float BuildCost { get; set; }

	/// <summary>Fraction of <see cref="BuildCost"/> refunded when the ride is sold/demolished.</summary>
	public const float SellRefundFraction = 0.5f;

	/// <summary>Extra entities this ride spawned outside the engine (entrance/exit markers, queue-path
	/// quads) so they can be removed on sell/demolish. Populated by the placement code.</summary>
	public List<ModelEntity> OwnedEntities { get; } = new();

	/// <summary>The grid cells this ride's queue path reserved — freed on sell/demolish (T-041).</summary>
	public List<(int X, int Y)> QueuePathCells { get; } = new();

	/// <summary>Tear the ride out of the world: stop its VM, despawn its engine objects + light/particle
	/// proxies, its markers + queue quads, its coaster track/train, and unregister it. Grid cells, the
	/// <see cref="RideQueue"/> and the refund are handled by the caller (it owns the grid + queue list).</summary>
	public void Despawn()
	{
		if ( VM != null )
			VM.IsRunning = false;
		engine.Despawn();
		Track?.Despawn();           // coaster: pylons + ribbon + train (no-op if none)
		Vehicle?.Despawn();         // car rides: the moving wagon (no-op if none)
		foreach ( var e in OwnedEntities )
			Entity.All.Remove( e );
		OwnedEntities.Clear();
		Entity.All.Remove( this );
	}

	/// <summary>Run (true) or idle (false) the ride's animation — driven by occupancy (see RideQueue / Peep).</summary>
	public void SetActive( bool active ) => engine.SetActive( active );

	/// <summary>Begin / end the sustained rider scream — the coaster train raises it while it carries
	/// riders (the VM-driven rides scream from their own scripts instead). See T-045 3b / T-037.</summary>
	public void StartRiderScream() => engine.StartScream( 0, 70 );
	public void StopRiderScream() => engine.StopScream();

	private readonly RideEngine engine = new();

	/// <summary>The ride's WAD path (e.g. <c>levels/jungle/rides/coaster1</c>) — used to load extra
	/// assets like coaster track textures (T-045).</summary>
	public string Archive { get; }

	/// <summary>Placement orientation in 90° clockwise steps (0–3) — rotates the footprint + mesh (T-041).</summary>
	public int Rotation { get; }

	public Ride( string rideArchive, Vector3 position, int rotation = 0 )
	{
		Position = position;
		Archive = rideArchive;
		Rotation = ((rotation % 4) + 4) % 4;
		var rideName = Path.GetFileNameWithoutExtension( rideArchive );
		Shape = RideShape.Load( rideArchive, rideName ).Rotated( Rotation );

		// Script (the VFS resolves the path into the .wad; matching is case-insensitive — T-014).
		VM = new RideVM( FileSystem.OpenRead( $"{rideArchive}/{rideName}.rse" ) );
		VM.Engine = engine;

		// SPAWNCHILD loads sibling child scripts from the same WAD, sharing this ride's engine.
		VM.ChildLoader = name =>
		{
			try
			{
				return new RideVM( FileSystem.OpenRead( $"{rideArchive}/{name}.rse" ) ) { Engine = VM.Engine };
			}
			catch ( Exception e )
			{
				Log.Warning( $"[ride] child script '{name}' not found: {e.Message}" );
				return null;
			}
		};

		try
		{
			var settings = new SettingsFile( FileSystem.OpenRead( $"{rideArchive}/{rideName}.sam" ) );
			var rideTitle = settings.Entries.Where( x => x.Key == "Info.Name" ).Select( x => x.Value ).FirstOrDefault();
			Name = string.IsNullOrEmpty( rideTitle ) ? rideName : rideTitle;

			// Sub-tile positions within the entrance/exit cells (where peeps stand / appear), default centre.
			EntryStandPos = (ReadFloat( settings, "UsageInfo.EntryCellStandPosX", 0.5f ), ReadFloat( settings, "UsageInfo.EntryCellStandPosY", 0.5f ));
			ExitAppearPos = (ReadFloat( settings, "UsageInfo.ExitCellAppearPosX", 0.5f ), ReadFloat( settings, "UsageInfo.ExitCellAppearPosY", 0.5f ));
			maxCapacity = Math.Max( 1, ReadInt( settings, "UsageInfo.MaxCapacity", 8 ) );
			DurationUnit = ReadInt( settings, "Info.DurationUnit", 0 );

			// Upgrade table: Upgrades[0] = as built, [1..] = researchable capacity upgrades (T-044).
			for ( int i = 0; ; i++ )
			{
				int cap = ReadInt( settings, $"Upgrades[{i}].InitCapacity", -1 );
				if ( cap < 0 )
					break;
				upgrades.Add( new UpgradeLevelInfo( cap,
					ReadInt( settings, $"Upgrades[{i}].CostOfResearch", 0 ),
					ReadInt( settings, $"Upgrades[{i}].CostOfUpgrade", 0 ) ) );
			}
			Excitement = Math.Max( 1, ReadInt( settings, "UsageInfo.ExcitementLevel", 50 ) );
			Attraction = ReadInt( settings, "Info.AttractionValue", 25 );
			TicketPrice = MathF.Max( 1f, MathF.Round( Excitement / 10f ) );

			Log.Info( $"[ride] loaded '{Name}' from {rideArchive} (footprint {Shape.Width}x{Shape.Height}, entrance {Shape.Entrance?.ToString() ?? "none"}, exit {Shape.Exit?.ToString() ?? "none"})" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] settings unavailable: {e.Message}" );
		}

		// The in-world model is the exact base name (`<name>.md2`). WADs also ship `P<name>.md2`
		// preview models for the build menu (see docs/08-ghidra-animation.md) — loading by exact base
		// name keeps those out of the park. There is no separate LOD model set (the original has no
		// distance-based model swap).
		LoadModel( $"{rideArchive}/{rideName}.md2", rideArchive );

		// Discover the ride's real animation channels from the WAD (see docs/08-ghidra-animation.md):
		// the original names keyframe files <base><letter>[<n>].md2, letter = first letter of the
		// animation name. We probe each ScriptDefs.Animations channel so the engine animates only what
		// the ride actually ships, and knows each channel's frame count.
		var channels = DiscoverAnimChannels( rideArchive, rideName );
		engine.SetAnimChannels( channels );
		LoadKeyframes( rideArchive, rideName, channels );
		engine.StartBestBodyAnim();

		// Ride duration: one full pass of the ride's running animation is the authoritative, ride-specific
		// length we have (monkey ~11 s, totem ~14 s). Fall back to Info.DurationUnit (a 0–3 band; the exact
		// band→seconds table lives in TP.EXE and isn't RE'd yet — ~4 s/unit matches the decoded loop lengths)
		// then a flat default for rides whose loop has no decoded keyframes.
		RideDuration = engine.BodyLoopDuration is float loop && loop > 0 ? loop
			: DurationUnit > 0 ? DurationUnit * 4f
			: 5f;
		Log.Info( $"[ride] '{Name}' duration {RideDuration:0.0}s (DurationUnit {DurationUnit}), capacity {Capacity}" );

		// Open the ride and tell its script the capacity. A ride starts CLOSED in the VM (VAR_RIDECLOSED
		// = 1); the engine is what opens it. The script's load loop then polls VAR_LETMEON — the "a peep
		// wants on" signal the game sets — to take a rider, run, and (for thrill rides) scream. Wiring
		// our RideQueue → VAR_LETMEON (NotifyBoarding) is what finally drives that authentic run sequence.
		// RE'd from monkey.rse (TEST VAR_RIDECLOSED → TEST VAR_LETMEON → ADD VAR_ONRIDE → STARTSCREAM).
		VM.Variables[(int)RideVariables.VAR_RIDECLOSED] = 0;
		VM.Variables[(int)RideVariables.VAR_CAPACITY] = Capacity;

		VM.IsRunning = true;
	}

	/// <summary>
	/// Tell the ride script a peep is boarding by raising <c>VAR_LETMEON</c> — the per-ride "a peep wants
	/// on" flag its load loop polls (it consumes the flag, increments its on-ride count, and runs the
	/// ride). This bridges our <see cref="RideQueue"/> occupancy to the VM's own rider model so scripted
	/// run sequences (animations, scream, …) actually fire. See T-032.
	/// </summary>
	public void NotifyBoarding()
	{
		if ( VM != null )
			VM.Variables[(int)RideVariables.VAR_LETMEON] = 1;
	}

	// Loads the decoded keyframe tracks for each animation channel and hands them to the engine, so a
	// channel with real animation data plays its tracks (rotation, …) instead of the placeholder bob.
	// A channel's animation lives in its frame file(s) (<base><c>.md2 or <base><c>1.md2 … — see
	// docs/08-ghidra-animation.md). A numbered channel's files each animate different surfaces of the
	// ride (e.g. totem: m1→part 0/11, m2→part 14, …), so all are loaded and their surfaces merged —
	// otherwise only the first file's parts would move. Non-fatal: failures leave that channel on the bob.
	private void LoadKeyframes( string rideArchive, string rideName, Dictionary<int, int> channels )
	{
		foreach ( var (anim, frames) in channels )
		{
			var c = RideEngine.ChannelLetter( (ScriptDefs.Animations)anim );

			RideKeyframeFile? merged = null;
			if ( frames > 1 )
			{
				for ( int n = 1; n <= frames; n++ )
				{
					var kf = TryLoadKeyframe( $"{rideArchive}/{rideName}{c}{n}.md2", anim );
					if ( kf == null || kf.Surfaces.Count == 0 )
						continue;
					if ( merged == null )
						merged = kf;
					else
						merged.Merge( kf );
				}
			}
			else
			{
				merged = TryLoadKeyframe( $"{rideArchive}/{rideName}{c}.md2", anim );
			}

			if ( merged != null && merged.Surfaces.Count > 0 )
			{
				engine.SetChannelKeyframes( anim, merged );
				Log.Info( $"[ride] keyframes for {(ScriptDefs.Animations)anim}: {merged.Surfaces.Count} animated surface(s), duration {merged.Duration}" );
			}
		}
	}

	private static RideKeyframeFile? TryLoadKeyframe( string rel, int anim )
	{
		try
		{
			using var s = FileSystem.OpenRead( rel );
			return s == null ? null : new RideKeyframeFile( s );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] keyframes for {(ScriptDefs.Animations)anim} ({rel}) failed: {e.Message}" );
			return null;
		}
	}

	private static float ReadFloat( SettingsFile settings, string key, float fallback ) =>
		float.TryParse( settings[key], System.Globalization.CultureInfo.InvariantCulture, out var v ) ? v : fallback;

	private static int ReadInt( SettingsFile settings, string key, int fallback ) =>
		int.TryParse( settings[key], out var v ) ? v : fallback;

	// Probes the WAD for each animation channel's keyframe files and returns anim id -> frame count.
	// A channel is a numbered sequence (<base><c>1.md2, <base><c>2.md2, …) or a single frame
	// (<base><c>.md2); a channel with no files is simply absent (no animation for that state).
	private static Dictionary<int, int> DiscoverAnimChannels( string rideArchive, string rideName )
	{
		// A file is present only if OpenRead yields a non-null stream: a missing entry inside a
		// *mounted* WAD returns null (not an exception), so a bare try/catch would treat every
		// missing frame as present and loop forever. See WadArchive.OpenFile.
		bool Exists( string rel )
		{
			try { using var s = FileSystem.OpenRead( rel ); return s != null; }
			catch { return false; }
		}

		var map = new Dictionary<int, int>();
		foreach ( ScriptDefs.Animations anim in Enum.GetValues<ScriptDefs.Animations>() )
		{
			var c = RideEngine.ChannelLetter( anim );
			if ( c == '\0' )
				continue;

			// Numbered sequence first (Main is e.g. <base>m1.md2 … <base>m7.md2).
			int frames = 0;
			while ( Exists( $"{rideArchive}/{rideName}{c}{frames + 1}.md2" ) )
				frames++;

			// Otherwise a single unnumbered frame (<base><c>.md2).
			if ( frames == 0 && Exists( $"{rideArchive}/{rideName}{c}.md2" ) )
				frames = 1;

			if ( frames > 0 )
				map[(int)anim] = frames;
		}

		return map;
	}

	// Loads the ride's main model and spawns a ModelEntity per mesh (the LobbyIsland pattern). Ride
	// textures live in the WAD under textures/; missing ones fall back to Texture.Missing. Any failure
	// is non-fatal — the VM still runs (so the sound/engine proof holds) even if geometry doesn't load.
	private void LoadModel( string md2Path, string rideArchive )
	{
		try
		{
			var parts = BuildMeshEntities( md2Path, rideArchive );
			// Register the meshes as the ride body so the engine can animate them (it starts a looping
			// idle so the model visibly moves and is easy to pick out).
			engine.RegisterBody( parts );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] model load failed ({md2Path}): {e.Message}" );
		}
	}

	// Builds a ModelEntity per mesh of an MD2 at this ride's position (the LobbyIsland pattern). Ride
	// textures live in the WAD under textures/; missing ones fall back to Texture.Missing.
	private List<ModelEntity> BuildMeshEntities( string md2Path, string rideArchive )
	{
		var modelFile = new ModelFile( md2Path );
		// The authored car/seat nodes (object 0x80 + car 0x100) — their count drives how many riders the
		// RideVehicle shows (T-048). Their world positions are runtime sim output, not file data, so only
		// the count is used here.
		CarNodeCount = modelFile.Nodes.Count( n => n.IsObject || n.IsCar );
		// Runtime node→world-position resolver (T-048/T-047): EVENT/SPARK effects + the vehicle read it.
		NodeField = new RideNodePositions( modelFile.Nodes );
		engine.NodeField = NodeField;
		// Size the head-slot table to the model's head-node count (the original probes type-0x80 at spawn).
		VM?.SetHeadCapacity( NodeField.ObjectNodeIds.Count );
		Log.Info( $"[ride] model {md2Path}: {modelFile.Meshes.Count} mesh(es), {modelFile.Nodes.Count} node(s) ({CarNodeCount} car/seat)" );

		var parts = new List<ModelEntity>();
		foreach ( var mesh in modelFile.Meshes )
		{
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();
			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i )
				{
					textures.Add( Texture.Missing );
					continue;
				}

				try { textures.Add( new Texture( $"{rideArchive}/textures/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) ); }
				catch { textures.Add( Texture.Missing ); }
			}
			material.Set( "Color", [.. textures] );

			var vertices = new List<Vertex>();
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				vertices.Add( new Vertex
				{
					Position = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ),
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags
				} );
			}

			var model = new Model( [.. vertices], mesh.Indices, material );
			Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );

			// Placement rotation (T-041): spin the whole ride about its footprint centre (= Position) by
			// the orientation yaw — rotate each part's local offset and compose the yaw onto its rotation.
			// 90° CW per step (negative about world +Z, which is up here). RegisterBody captures this as the
			// rest pose, so the engine's animation composes on top of the placed orientation.
			var localOffset = new Vector3( pos.X, pos.Z, pos.Y );
			var partRot = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W );
			if ( Rotation != 0 )
			{
				// Rotate the horizontal offset (XY; Z is up) by -Rotation·90°, exact per quarter-turn.
				(float c, float s) = Rotation switch { 1 => (0f, -1f), 2 => (-1f, 0f), 3 => (0f, 1f), _ => (1f, 0f) };
				localOffset = new Vector3( localOffset.X * c - localOffset.Y * s, localOffset.X * s + localOffset.Y * c, localOffset.Z );
				var yaw = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, -Rotation * MathF.PI / 2f );
				partRot = yaw * partRot;
			}

			parts.Add( new ModelEntity
			{
				Model = model,
				Scale = new Vector3( scl.X, scl.Z, scl.Y ),
				Rotation = partRot,
				Position = localOffset + Position,
			} );
		}
		return parts;
	}

	protected override void OnUpdate()
	{
		// Null-guarded: the Entity base ctor registers this in Entity.All before the ctor body runs,
		// so a ride that failed to load (VM never assigned) would otherwise crash the update loop.
		VM?.Update();
		engine.Update( Time.Now );

		// Mirror the VM's WALKON/ADDHEAD slot tables into the world at the ride's node positions (T-048):
		// gliding walk peeps between walk nodes, decorative heads at head nodes.
		if ( VM != null )
		{
			engine.SyncHeads( VM.HeadSlots );
			engine.SyncWalk( VM.WalkSlots, VM.GameTime );
		}

		// Running the ride costs money over time (its upkeep drains the park balance).
		ParkFinances.Current?.PayUpkeep( UpkeepPerSecond * Time.Delta );

		// Carrying riders wears the ride down; at zero reliability it breaks (a mechanic must repair it).
		if ( !IsBroken && Riders > 0 )
		{
			Reliability -= WearPerSecond * Time.Delta;
			if ( Reliability <= 0f )
				BreakDown();
		}

		// Researchers advance any in-progress upgrade research for this ride (T-044).
		TickResearch( Time.Delta, Staff.ResearcherCount );
	}
}
