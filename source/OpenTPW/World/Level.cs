using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; } = null!;

	public RootPanel Hud { get; set; } = null!;
	public Sun SunLight { get; set; } = null!;

	public SettingsFile Global { get; private init; }

	public Level( string levelName )
	{
		LoadProgress.Report( "Loading level settings...", 0.03f );
		Global = new SettingsFile( $"/levels/{levelName}/global.sam" );
		Current = this;

		SetupEntities();
		SetupHud();
	}

	private void SetupEntities()
	{
		SunLight = new Sun() { Position = new( 0, 100, 100 ) };

		LoadProgress.Report( "Loading sky...", 0.12f );
		_ = new Sky();

		// (The lobby's giant water plane is omitted in the dev park demo below — the real terrain mesh
		// has its own water surfaces, and a 10000-unit plane at z=0 would submerge/occlude the park.)

		// Dev test: render the real jungle park terrain (terrain.wad/base.md2 — 272 meshes, the actual
		// landscape), with several rides placed at tile coordinates on its surface. Replaces the lobby
		// islands here. Guarded so a load failure falls back to the lobby orbit camera.
		LoadProgress.BeginPhase( 0.45f, 0.92f );
		try
		{
			SetupDevPark();
		}
		catch ( Exception e )
		{
			Log.Warning( $"dev park failed to load: {e.Message}" );
			Camera.SetCameraMode<LobbyCameraMode>();
		}
	}

	// Dev demonstration: render the real jungle terrain mesh and place a few rides on its surface via
	// the placement grid. See ParkTerrain / PlacementGrid / docs T-032.
	private static void SetupDevPark()
	{
		LoadProgress.Report( "Loading park terrain...", 0.5f );
		var terrain = new ParkTerrain( "levels/jungle/terrain" );

		// The park starts with a budget; peeps pay a gate fee + ride tickets, rides cost upkeep (see Ride / Peep).
		ParkFinances.Current = new ParkFinances( starting: 30000f, entryFee: 10f );

		// Centre the placement grid on the terrain's dense centroid (robust to stray distant meshes).
		var standard = new SettingsFile( "/levels/jungle/Standard.sam" );
		var centre = terrain.Centroid;
		var grid = PlacementGrid.FromLevelSettings( standard, tileSize: 16f, worldCenter: new Vector3( centre.X, centre.Y, 0 ) );

		// Player-controlled build/manage camera focused on the park centroid (T-040).
		BuildCameraMode.Focus = new Vector3( centre.X, centre.Y, centre.Z );
		Camera.SetCameraMode<BuildCameraMode>();

		// Build mode (T-041): an empty park the player fills via the catalog (number keys pick an item,
		// left-click places it, charging its cost). Rides register their queue so peeps can use them.
		parkQueues = new List<RideQueue>();
		var catalog = BuildCatalog();
		_ = new BuildMode( grid, terrain, catalog, ( item, tx, ty ) => CommitPlacement( item, grid, terrain, tx, ty ) );

		// Diagnostic: deterministically exercise the placement pipeline (the same path a click commits).
		if ( Environment.GetEnvironmentVariable( "OPENTPW_AUTOPLACE" ) != null )
		{
			int cx = grid.Width / 2, cy = grid.Height / 2;
			BuildCatalogItem Item( string name ) => catalog.First( c => c.Name == name );
			Log.Info( $"[build] autoplace totem={CommitPlacement( Item( "totem" ), grid, terrain, cx - 6, cy - 2 )} "
				+ $"monkey={CommitPlacement( Item( "monkey" ), grid, terrain, cx + 1, cy - 2 )} "
				+ $"coaster={CommitPlacement( Item( "coaster1" ), grid, terrain, cx - 6, cy + 4 )} "
				+ $"shop={CommitPlacement( Item( "shop" ), grid, terrain, cx + 6, cy + 4 )} "
				+ $"ent={CommitPlacement( Item( "entertainer" ), grid, terrain, cx, cy + 2 )} "
				+ $"hand={CommitPlacement( Item( "handyman" ), grid, terrain, cx + 2, cy + 2 )} "
				+ $"rsch={CommitPlacement( Item( "researcher" ), grid, terrain, cx + 4, cy + 2 )}" );

			// Lay a short coaster track from the placed coaster's connector (T-045 slice 2).
			var coaster = Entity.All.OfType<Ride>().FirstOrDefault( r => r.Shape.HasTrack );
			if ( coaster != null )
			{
				var t = new CoasterTrack( coaster, grid, terrain );
				for ( int k = 0; k < 7; k++ )
				{
					var (hx, hy) = t.Head;
					if ( !t.Extend( hx + 1, hy ) )
						break;
				}
				Log.Info( $"[build] autotrack segments={t.SegmentCount}" );
			}

			// Exercise the research → upgrade pipeline (T-044) on the first placed ride.
			var ride = Entity.All.OfType<Ride>().FirstOrDefault();
			if ( ride != null )
			{
				int before = ride.Capacity;
				ride.StartResearch();
				ride.TickResearch( 100f, 1 ); // force-complete the research
				bool researched = ride.NextResearched;
				ride.ApplyUpgrade();
				Log.Info( $"[build] upgrade-test {ride.Name}: L0 cap {before} -> researched={researched} L{ride.UpgradeLevel} cap {ride.Capacity} (levels={ride.Upgrades.Count})" );
			}
		}

		// Spawn a crowd; with no rides yet they wander until the player builds one (then they queue).
		LoadProgress.Report( "Spawning visitors...", 0.95f );
		for ( int i = 0; i < 30; i++ )
		{
			var a = i / 30f * MathF.PI * 2f;
			var spawn = new Vector3( centre.X + MathF.Cos( a ) * 120f, centre.Y + MathF.Sin( a ) * 120f, 0 );
			_ = new Peep( terrain, parkQueues, spawn, i );
		}
		Log.Info( $"[park] build mode ready ({catalog.Count} catalog items), {parkQueues.Count} queues" );
		// Staff start empty too — the player hires entertainers/handymen/guards/researchers from the
		// catalog (T-043), each charged a hire cost and then drawing wages.
	}

	// The park's ride queues — placed rides register here so peeps (which hold this list) can use them.
	private static List<RideQueue> parkQueues = new();

	// Build catalog: jungle rides (footprint from RideShape, cost from the .sam) + a food shop + hireable
	// staff (no footprint — placed on a tile and charged a hire cost, T-043).
	private static List<BuildCatalogItem> BuildCatalog()
	{
		var list = new List<BuildCatalogItem>();
		foreach ( var path in new[] { "levels/jungle/rides/totem", "levels/jungle/rides/monkey", "levels/jungle/rides/wateride", "levels/jungle/rides/coaster1" } )
		{
			var name = Path.GetFileName( path );
			var shape = RideShape.Load( path, name );
			list.Add( new BuildCatalogItem( name, path, null, shape.Width, shape.Height, ReadRideCost( path, name ) ) );
		}
		list.Add( new BuildCatalogItem( "shop", null, null, 2, 2, 500f ) );
		list.Add( new BuildCatalogItem( "entertainer", null, StaffRole.Entertainer, 1, 1, 800f ) );
		list.Add( new BuildCatalogItem( "handyman", null, StaffRole.Handyman, 1, 1, 600f ) );
		list.Add( new BuildCatalogItem( "guard", null, StaffRole.Guard, 1, 1, 1000f ) );
		list.Add( new BuildCatalogItem( "researcher", null, StaffRole.Researcher, 1, 1, 1500f ) );
		return list;
	}

	private static float ReadRideCost( string path, string name )
	{
		try
		{
			var s = new SettingsFile( FileSystem.OpenRead( $"{path}/{name}.sam" ) );
			return int.TryParse( s["Upgrades[0].CostOfUpgrade"], out var v ) ? v : 2000f;
		}
		catch { return 2000f; }
	}

	// Commit a placement at tile (tx,ty): validate + (for rides/shops) reserve the grid, spawn, charge.
	private static bool CommitPlacement( BuildCatalogItem item, PlacementGrid grid, ParkTerrain terrain, int tx, int ty )
	{
		var fin = ParkFinances.Current;
		if ( fin != null && !fin.CanAfford( item.Cost ) )
			return false;

		// Staff are mobile — hire + drop at the tile, no grid reservation.
		if ( item.Staff is { } role )
		{
			var c = grid.TileToWorld( tx, ty );
			_ = new Staff( role, terrain, c.WithZ( 0 ), roam: 70f );
			fin?.PayBuild( item.Cost );
			return true;
		}

		if ( !grid.CanPlace( tx, ty, item.Width, item.Height ) || !grid.TryPlace( tx, ty, item.Width, item.Height ) )
			return false;

		bool ok = item.RidePath == null
			? SpawnShopAt( terrain, grid, tx, ty )
			: SpawnRideAt( item.RidePath, grid, terrain, tx, ty );

		if ( !ok )
		{
			grid.Clear( tx, ty, item.Width, item.Height );
			return false;
		}
		fin?.PayBuild( item.Cost );
		return true;
	}

	// Spawns a ride on its (already reserved) footprint: model, entrance/exit markers, queue path, and
	// registers its RideQueue so peeps can ride it. Starts idle (occupancy drives the animation).
	private static bool SpawnRideAt( string path, PlacementGrid grid, ParkTerrain terrain, int tx, int ty )
	{
		try
		{
			var w = grid.TileToWorld( tx, ty, RideShape.Load( path, Path.GetFileName( path ) ).Width,
				RideShape.Load( path, Path.GetFileName( path ) ).Height );
			var ride = new Ride( path, w.WithZ( terrain.SampleHeight( w.X, w.Y ) ) );
			ride.SetActive( false );
			ride.TileX = tx; ride.TileY = ty; ride.TileW = ride.Shape.Width; ride.TileH = ride.Shape.Height;
			PlaceEntranceExitMarkers( ride, grid, terrain, tx, ty );
			var waypoints = SpawnQueuePath( ride, grid, terrain, tx, ty );
			if ( waypoints != null )
			{
				var exit = ride.Shape.Exit is { } x
					? grid.PointToWorld( tx + x.X + ride.ExitAppearPos.X, ty + x.Y + ride.ExitAppearPos.Y )
					: waypoints[^1];
				exit = exit.WithZ( terrain.SampleHeight( exit.X, exit.Y ) );
				parkQueues.Add( new RideQueue( ride, waypoints, exit, ride.RideDuration ) );
			}
			return true;
		}
		catch ( Exception e ) { Log.Warning( $"[build] ride '{path}' failed: {e.Message}" ); return false; }
	}

	private static bool SpawnShopAt( ParkTerrain terrain, PlacementGrid grid, int tx, int ty )
	{
		var c = grid.TileToWorld( tx, ty, 2, 2 );
		_ = new Shop( terrain, c );
		return true;
	}

	// Visualises a ride's entrance/exit cells (from its Info.Shape) as small markers on the terrain —
	// green = entrance (where the queue connects), red = exit (where peeps appear). The sub-tile stand
	// point comes from the ride's UsageInfo entry/exit positions.
	private static void PlaceEntranceExitMarkers( Ride ride, PlacementGrid grid, ParkTerrain terrain, int tx, int ty )
	{
		if ( ride.Shape.Entrance is { } e )
		{
			var p = grid.PointToWorld( tx + e.X + ride.EntryStandPos.X, ty + e.Y + ride.EntryStandPos.Y );
			SpawnMarker( p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f ), 40, 220, 60 );
		}
		if ( ride.Shape.Exit is { } x )
		{
			var p = grid.PointToWorld( tx + x.X + ride.ExitAppearPos.X, ty + x.Y + ride.ExitAppearPos.Y );
			SpawnMarker( p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f ), 230, 40, 40 );
		}
	}

	// Lays a queue path leading out from a ride's entrance cell: a strip of path tiles stepping away
	// from the footprint edge the entrance sits on, each reserved on the grid and rendered as a flat
	// path quad on the terrain. (The 3D queue-fence meshes — questra/quebnd in queue.wad — are a
	// follow-up; this is the walkable path peeps queue along.)
	// Returns the queue's walk waypoints, ordered from the outer end up to the ride entrance (so peeps
	// can follow them), or null if the ride has no entrance.
	private static IReadOnlyList<Vector3>? SpawnQueuePath( Ride ride, PlacementGrid grid, ParkTerrain terrain, int tx, int ty, int length = 6 )
	{
		if ( ride.Shape.Entrance is not { } e )
			return null;

		// Outward direction = away from the footprint edge the entrance is on.
		int dx = 0, dy = 0;
		if ( e.Y == 0 ) dy = -1;
		else if ( e.Y == ride.Shape.Height - 1 ) dy = 1;
		else if ( e.X == 0 ) dx = -1;
		else if ( e.X == ride.Shape.Width - 1 ) dx = 1;
		else dy = -1;

		var material = new Material<ObjectUniformBuffer>( "content/shaders/3d.shader" );
		material.Set( "Color", LoadPathTexture() );

		Vector3 OnGround( Vector3 w ) => w.WithZ( terrain.SampleHeight( w.X, w.Y ) );

		var laid = new List<Vector3>();
		for ( int i = 1; i <= length; i++ )
		{
			int cx = tx + e.X + dx * i, cy = ty + e.Y + dy * i;
			if ( !grid.TryPlace( cx, cy, 1, 1 ) )
				break; // ran off the grid or hit another object

			var w = OnGround( grid.TileToWorld( cx, cy ) );
			laid.Add( w );
			_ = new ModelEntity
			{
				Model = Primitives.Plane.GenerateModel( material ),
				Position = w.WithZ( w.Z + 0.15f ),
				Scale = new Vector3( grid.TileSize / 2f ),
			};
		}

		// Walk order: outer end → inner tiles → the entrance stand point.
		laid.Reverse();
		laid.Add( OnGround( grid.PointToWorld( tx + e.X + ride.EntryStandPos.X, ty + e.Y + ride.EntryStandPos.Y ) ) );
		return laid;
	}

	private static Texture LoadPathTexture()
	{
		foreach ( var p in new[] { "levels/jungle/queue/stexture/jpa_que1.wct", "levels/jungle/terrain/spathtex/jpa_str1.wct" } )
		{
			try { return new Texture( p, TextureFlags.Repeat ); }
			catch { /* try next */ }
		}
		return Texture.Missing;
	}

	private static void SpawnMarker( Vector3 position, byte r, byte g, byte b )
	{
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		material.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
		_ = new ModelEntity { Model = Primitives.Cube.GenerateModel( material ), Position = position, Scale = new Vector3( 4f ) };
	}

	private void SetupHud()
	{
		LoadProgress.Report( "Loading interface...", 0.95f );
		Hud = new();

		var layout = new LobbyLayout() { Hud = Hud };
		layout.OnInit();

		Hud.AddChild( new ParkStatsPanel() ); // live park finances/visitors readout (no-op without a park)
		Hud.AddChild( new Cursor() );
	}

	private static float nextEconLog;

	public void Update()
	{
		// Iterate a snapshot: an entity's update may spawn or remove entities (e.g. peeps dropping litter,
		// handymen clearing it), which would otherwise invalidate the enumeration.
		foreach ( var entity in Entity.All.ToArray() )
			entity.Update();

		ParkFinances.Current?.Tick( Time.Delta ); // monthly loan instalments + bankruptcy check (T-042)

		// Diagnostic: periodically report the park's balance and the cumulative income/cost flows.
		if ( Environment.GetEnvironmentVariable( "OPENTPW_ECON_DEBUG" ) != null && ParkFinances.Current is { } f && Time.Now > nextEconLog )
		{
			nextEconLog = Time.Now + 5f;
			Log.Info( $"[econ] money={f.Money:0}  ride+={f.RideRevenue:0}  entry+={f.EntryRevenue:0}  food+={f.FoodRevenue:0}  upkeep-={f.UpkeepPaid:0}  wages-={f.WagesPaid:0}" );
		}
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
