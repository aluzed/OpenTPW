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

	/// <summary>How many peeps the ride holds at once (from <c>UsageInfo.MaxCapacity</c>).</summary>
	public int Capacity { get; private set; } = 8;

	/// <summary>The ride's duration band (<c>Info.DurationUnit</c>, 0–3 — indexes a seconds table in TP.EXE).</summary>
	public int DurationUnit { get; private set; }

	/// <summary>How long a single ride lasts, in seconds (peeps stay aboard this long). Anchored to the
	/// ride's own running animation length — see the ctor.</summary>
	public float RideDuration { get; private set; } = 5f;

	/// <summary>How exciting the ride is (<c>UsageInfo.ExcitementLevel</c>) — its draw: peeps pick rides
	/// weighted by this, so more exciting rides build longer queues.</summary>
	public int Excitement { get; private set; } = 50;

	/// <summary>The ride's base attraction value (<c>Info.AttractionValue</c>).</summary>
	public int Attraction { get; private set; } = 25;

	/// <summary>Run (true) or idle (false) the ride's animation — driven by occupancy (see RideQueue / Peep).</summary>
	public void SetActive( bool active ) => engine.SetActive( active );

	private readonly RideEngine engine = new();

	public Ride( string rideArchive, Vector3 position )
	{
		Position = position;
		var rideName = Path.GetFileNameWithoutExtension( rideArchive );
		Shape = RideShape.Load( rideArchive, rideName );

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
			Capacity = Math.Max( 1, ReadInt( settings, "UsageInfo.MaxCapacity", 8 ) );
			DurationUnit = ReadInt( settings, "Info.DurationUnit", 0 );
			Excitement = Math.Max( 1, ReadInt( settings, "UsageInfo.ExcitementLevel", 50 ) );
			Attraction = ReadInt( settings, "Info.AttractionValue", 25 );

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

		VM.IsRunning = true;
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
			var modelFile = new ModelFile( md2Path );
			Log.Info( $"[ride] model {md2Path}: {modelFile.Meshes.Count} mesh(es)" );

			var bodyParts = new List<ModelEntity>();
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

				bodyParts.Add( new ModelEntity
				{
					Model = model,
					Scale = new Vector3( scl.X, scl.Z, scl.Y ),
					Rotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W ),
					Position = new Vector3( pos.X, pos.Z, pos.Y ) + Position,
				} );
			}

			// Register the meshes as the ride body so the engine can animate them (it starts a looping
			// idle so the model visibly moves and is easy to pick out).
			engine.RegisterBody( bodyParts );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] model load failed ({md2Path}): {e.Message}" );
		}
	}

	protected override void OnUpdate()
	{
		// Null-guarded: the Entity base ctor registers this in Entity.All before the ctor body runs,
		// so a ride that failed to load (VM never assigned) would otherwise crash the update loop.
		VM?.Update();
		engine.Update( Time.Now );
	}
}
