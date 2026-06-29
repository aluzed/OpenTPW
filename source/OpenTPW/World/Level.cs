using OpenTPW.UI;

namespace OpenTPW;

public class Level
{
	internal static Level Current { get; set; } = null!;

	public RootPanel Hud { get; set; } = null!;
	public Sun SunLight { get; set; } = null!;

	/// <summary>True once a park has loaded — selects the in-park HUD over the lobby front-end (T-038).</summary>
	public bool InPark { get; private set; }

	public SettingsFile Global { get; private init; }

	/// <summary>The level's folder name (e.g. <c>jungle</c>) — locates per-level assets like the minimap image.</summary>
	public string Name { get; private init; }

	public Level( string levelName )
	{
		LoadProgress.Report( "Loading level settings...", 0.03f );
		Name = levelName;
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
			InPark = true; // a park is loaded → show the in-park HUD (not the lobby front-end)
		}
		catch ( Exception e )
		{
			Log.Warning( $"dev park failed to load: {e.Message}" );
			Camera.SetCameraMode<LobbyCameraMode>();
			InPark = false; // fall back to the lobby front-end
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
		ResearchQueue.Reset(); // fresh park-wide research queue for this level (T-044)

		// In-game calendar (T-053): the single time source; finances settle once per in-game month.
		GameClock.Current = new GameClock();
		GameClock.Current.OnNewMonth += ParkFinances.Current.SettleMonth;

		// Challenges (T-054): load the level's goals, keep the ones we can track, run them on the daily clock.
		try
		{
			using var cs = FileSystem.OpenRead( "Challenges.sam" );
			if ( cs != null )
			{
				var challenges = Challenge.ParseAll( new SettingsFile( cs ).Entries )
					.Where( c => ChallengeMetrics.IsSupported( c.Type ) ).ToList();
				var mgr = new ChallengeManager( challenges, ChallengeMetrics.Current,
					prize => ParkFinances.Current?.AwardPrize( prize ) );
				ChallengeManager.Current = mgr;
				GameClock.Current.OnNewDay += mgr.OnNewDay;
				Log.Info( $"[challenge] loaded {challenges.Count} trackable challenge(s)" );
			}
		}
		catch ( Exception e ) { Log.Warning( $"[challenge] load failed: {e.Message}" ); }

		// Centre the placement grid on the terrain's dense centroid (robust to stray distant meshes).
		var standard = new SettingsFile( "/levels/jungle/Standard.sam" );

		// Peep ride-choice weights (T-060): the authored DecisionVar* weights drive which ride peeps pick.
		try { RideChoiceScorer.Weights = DecisionWeights.Load( new SettingsFile( "/levels/Standard.sam" ) ); }
		catch ( Exception e ) { Log.Warning( $"[peep] decision weights load failed: {e.Message}" ); }

		// Golden-ticket goals (T-055): the level win condition; re-evaluate daily, award once all are met.
		var goals = new GoldenTicketGoals( GoldenTicketTargets.ParseLocal( standard ) );
		GoldenTicketGoals.Current = goals;
		GameClock.Current.OnNewDay += goals.Evaluate;

		// Weather + seasons (T-056): roll the level's authored weather over the four seasons, re-rolling on the
		// daily clock; the overlay/HUD read WeatherSim.Current. Sunny fallback if the block is missing.
		try
		{
			var weather = new WeatherSim( WeatherConfig.ParseLocal( standard ) );
			WeatherSim.Current = weather;
			GameClock.Current.OnNewDay += weather.OnNewDay;

			// Dev/demo: pin a weather state so the overlay can be seen on demand (jungle summers rarely rain).
			switch ( Environment.GetEnvironmentVariable( "OPENTPW_WEATHER" )?.ToLowerInvariant() )
			{
				case "rain": weather.Force( new WeatherState( WeatherKind.Rain, false ), 30 ); break;
				case "storm": weather.Force( new WeatherState( WeatherKind.Rain, true ), 5 ); break;
				case "snow": weather.Force( new WeatherState( WeatherKind.Snow, false ), 10 ); break;
				case "blizzard": weather.Force( new WeatherState( WeatherKind.Snow, true ), 2 ); break;
			}
		}
		catch ( Exception e ) { Log.Warning( $"[weather] load failed: {e.Message}" ); }

		// Day/night ambient (T-056): the level's ThemeEngine.AmbientLightLevel (packed ARGB) is the dim colour
		// the park drifts toward at night; darken it a touch so the night wash reads as dusk-blue, not flat grey.
		if ( uint.TryParse( standard["ThemeEngine.AmbientLightLevel"], out var ambient ) )
		{
			byte R( int shift ) => (byte)(((ambient >> shift) & 0xFF) * 0.4f);
			DayNightOverlay.NightColor = (R( 16 ), R( 8 ), R( 0 ));
		}
		DayNightCycle.Reset( Time.Now ); // start the visual day/night cycle at midday for this park
		WeatherAudio.Reset();            // silence any rain loop carried over from a previous park
		var centre = terrain.Centroid;
		var grid = PlacementGrid.FromLevelSettings( standard, tileSize: 16f, worldCenter: new Vector3( centre.X, centre.Y, 0 ) );

		// Flag low terrain as water so peeps route around lakes/moats and nothing is built on them (T-050).
		// Water level = a little above the terrain's lowest point (the dev park has no explicit water plane).
		var waterLevel = terrain.Min.Z + (terrain.Max.Z - terrain.Min.Z) * 0.08f;
		var waterTiles = grid.MarkWaterFromTerrain( terrain.SampleHeight, waterLevel );
		Log.Info( $"[park] water tiles: {waterTiles}/{grid.Width * grid.Height} (level z={waterLevel:0.0})" );

		var pathGraph = new PathGraph( grid ); // peeps route around ride/shop footprints + water (T-036/T-050)

		// Player-controlled build/manage camera focused on the park centroid (T-040).
		BuildCameraMode.Focus = new Vector3( centre.X, centre.Y, centre.Z );
		Camera.SetCameraMode<BuildCameraMode>();

		// Build mode (T-041): an empty park the player fills via the catalog (number keys pick an item,
		// left-click places it, charging its cost). Rides register their queue so peeps can use them.
		parkQueues = new List<RideQueue>();
		var catalog = BuildCatalog();
		// Keep the placement context so a loaded save can rebuild placements (T-059).
		saveGrid = grid; saveTerrain = terrain; saveCatalog = catalog;
		_ = new BuildMode( grid, terrain, catalog,
			( item, tx, ty, rot ) => CommitPlacement( item, grid, terrain, tx, ty, rot ),
			ride => DemolishRide( ride, grid ),
			shop => DemolishShop( shop, grid ) );

		// Diagnostic: deterministically exercise the placement pipeline (the same path a click commits).
		if ( Environment.GetEnvironmentVariable( "OPENTPW_AUTOPLACE" ) != null )
		{
			int cx = grid.Width / 2, cy = grid.Height / 2;
			BuildCatalogItem Item( string name ) => catalog.First( c => c.Name == name );

			// Bumper demo: place ONLY a bumper at the park centre on clear grass and aim the camera at it,
			// so the arena collision sim (CarSim) is cleanly framed (OPENTPW_BUMPER_DEMO=1). See docs/08.
			if ( Environment.GetEnvironmentVariable( "OPENTPW_BUMPER_DEMO" ) == "1" )
			{
				if ( CommitPlacement( Item( "bumper" ), grid, terrain, cx - 2, cy - 2 ) )
				{
					var b = Entity.All.OfType<Ride>().Last();
					BuildCameraMode.Focus = b.Position;
					Log.Info( $"[build] bumper-demo: placed at park centre, isBumper={b.IsBumperRide} vehicle={(b.Vehicle != null)} cars={b.CarNodeCount}" );
				}
				return;
			}

			Log.Info( $"[build] autoplace totem={CommitPlacement( Item( "totem" ), grid, terrain, cx - 6, cy - 2 )} "
				+ $"monkey={CommitPlacement( Item( "monkey" ), grid, terrain, cx + 1, cy - 2 )} "
				+ $"coaster={CommitPlacement( Item( "coaster1" ), grid, terrain, cx - 6, cy + 4 )} "
				+ $"shop={CommitPlacement( Item( "shop" ), grid, terrain, cx + 6, cy + 4 )} "
				+ $"drink={CommitPlacement( Item( "drink" ), grid, terrain, cx + 6, cy + 1 )} "
				+ $"toilet={CommitPlacement( Item( "toilet" ), grid, terrain, cx + 8, cy + 4 )} "
					+ $"sideshow={CommitPlacement( Item( "puzzle" ), grid, terrain, cx - 2, cy + 4 )} " // T-058: a stall peeps play
					+ $"squark={CommitPlacement( Item( "squark" ), grid, terrain, cx + 2, cy + 4 )} "
				+ $"ent={CommitPlacement( Item( "entertainer" ), grid, terrain, cx, cy + 2 )} "
				+ $"hand={CommitPlacement( Item( "handyman" ), grid, terrain, cx + 2, cy + 2 )} "
				+ $"rsch={CommitPlacement( Item( "researcher" ), grid, terrain, cx + 4, cy + 2 )} "
				+ $"mech={CommitPlacement( Item( "mechanic" ), grid, terrain, cx + 4, cy )} "
				+ $"guard={CommitPlacement( Item( "guard" ), grid, terrain, cx, cy )}" ); // guard patrols the centre (T-039 vandalism deterrence)

			// Rotation (T-041): place a totem rotated 90° and confirm its footprint dims swapped (3x4 -> 4x3).
			if ( CommitPlacement( Item( "totem" ), grid, terrain, cx - 12, cy - 6, rotation: 1 ) )
			{
				var rot = Entity.All.OfType<Ride>().Last();
				Log.Info( $"[build] rotate-test: totem placed rot{rot.Rotation}, footprint {rot.TileW}x{rot.TileH} (upright is 3x4)" );
			}

			// Car rides (T-032): place a tour ride + go-karts + bumpers and confirm each gets a moving
			// RideVehicle. The bumper drives the arena collision sim (CarSim), the others the circuit loop.
			foreach ( var (carName, ctx, cty) in new[] { ("tourride", cx - 12, cy + 2), ("gokarts", cx - 12, cy + 8), ("bumper", cx + 9, cy - 6) } )
				if ( CommitPlacement( Item( carName ), grid, terrain, ctx, cty ) )
				{
					var car = Entity.All.OfType<Ride>().Last();
					Log.Info( $"[build] car-test: {carName} isCarRide={car.IsCarRide} isBumper={car.IsBumperRide} vehicle={(car.Vehicle != null)}" );
					// Dev visualisation: point the build camera at the bumper so its arena cars are on screen.
					if ( car.IsBumperRide && Environment.GetEnvironmentVariable( "OPENTPW_BUMPER_DEMO" ) == "1" )
						BuildCameraMode.Focus = car.Position;
				}

			// Lay a coaster track that loops around the station back to its '<' entry connector, so the
			// CoasterTrack closes into a circuit and its train (slice 3) runs a continuous loop.
			var coaster = Entity.All.OfType<Ride>().FirstOrDefault( r => r.Shape.HasTrack );
			if ( coaster != null && coaster.Shape.TrackOut is { } outC && coaster.Shape.TrackIn is { } inC )
			{
				var t = new CoasterTrack( coaster, grid, terrain );
				int ax = coaster.TileX + outC.X, ay = coaster.TileY + outC.Y; // '>' anchor
				int tx = coaster.TileX + inC.X, ty = coaster.TileY + inC.Y;   // '<' entry tile
				int top = ay - 2, right = ax + 1, left = tx - 1;              // a ring around the station top
				var loop = new List<(int X, int Y)>();
				for ( int y = ay; y >= top; y-- ) loop.Add( (right, y) );       // up the right side
				for ( int x = right - 1; x >= left; x-- ) loop.Add( (x, top) ); // across the top
				for ( int y = top + 1; y <= ty; y++ ) loop.Add( (left, y) );    // down the left side
				loop.Add( (tx, ty) );                                           // into the entry connector
				int laid = 0;
				foreach ( var (lx, ly) in loop )
				{
					if ( !t.Extend( lx, ly ) )
						break;
					if ( ++laid == 3 ) { t.StackHead( +1 ); t.StackHead( +1 ); } // raise a mid-track hill (T-045 3b)
				}
				Log.Info( $"[build] autotrack segments={t.SegmentCount} closed={t.IsClosed}" );
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

				// Exercise the ride light opcodes (T-007 82-85) through the real engine: no jungle ride
				// script uses lights (they're for scenery), so drive them here to prove the engine proxy
				// path renders without throwing. Enable two lights, colour + dim one, disable the other.
				var v = ride.VM;
				Operand Lit( int n ) => new( v, Operand.Type.Literal, n );
				v.CallOpcodeHandler( Opcode.ENABLELIGHT, Lit( 1 ) );
				v.CallOpcodeHandler( Opcode.ENABLELIGHT, Lit( 2 ) );
				v.CallOpcodeHandler( Opcode.COLOURLIGHT, Lit( 1 ), Lit( 100 ), Lit( 40 ), Lit( 0 ) ); // warm orange
				v.CallOpcodeHandler( Opcode.SETLIGHT, Lit( 1 ), Lit( 75 ) );                          // 75% brightness
				v.CallOpcodeHandler( Opcode.DISABLELIGHT, Lit( 2 ) );

				// Exercise the particle opcodes (T-007 93/105) through the engine's .PLB-driven effect system
				// (T-019): no ride script uses them, so drive them here — proves the effect lookup + proxy.
				v.CallOpcodeHandler( Opcode.REPAIREFFECT, Lit( 0 ) );
				v.CallOpcodeHandler( Opcode.SPARK, Lit( 0 ), Lit( 0 ), Lit( 0 ), Lit( 0 ) );
				// ADDOBJ_EXT with a particle type (4) + effect code 11 (Fire) — proves the extended object
				// spawn routes particle types through the .PLB system (T-007).
				v.CallOpcodeHandler( Opcode.ADDOBJ_EXT, Lit( 4 ), Lit( 0 ), Lit( 11 ), Lit( 100 ), Lit( 0 ) );
			}

			// Exercise sell/demolish (T-041): tear down a ride + a shop, confirming the refund + cleanup.
			var rideToSell = Entity.All.OfType<Ride>().LastOrDefault();
			if ( rideToSell != null )
			{
				int ridesBefore = Entity.All.Count( e => e is Ride );
				int queuesBefore = parkQueues.Count;
				float moneyBefore = ParkFinances.Current?.Money ?? 0f;
				DemolishRide( rideToSell, grid );
				int ridesAfter = Entity.All.Count( e => e is Ride );
				float refund = ParkFinances.Current?.Money - moneyBefore ?? 0f;
				Log.Info( $"[build] sell-test ride {rideToSell.Name}: rides {ridesBefore}->{ridesAfter}, "
					+ $"queues {queuesBefore}->{parkQueues.Count}, refund {refund:0}" );
			}
			if ( Shop.Stalls.FirstOrDefault() is { } shopToSell )
			{
				int shopsBefore = Shop.Stalls.Count;
				float moneyBefore = ParkFinances.Current?.Money ?? 0f;
				DemolishShop( shopToSell, grid );
				float refund = ParkFinances.Current?.Money - moneyBefore ?? 0f;
				Log.Info( $"[build] sell-test shop {shopToSell.Name}: shops {shopsBefore}->{Shop.Stalls.Count}, refund {refund:0}" );
			}
		}

		// Spawn a crowd at the park's real entrance gate (T-050): peeps enter and leave through the gate
		// (FixedItemInfo.EntranceA/B in Standard.sam) rather than a synthetic spawn ring.
		LoadProgress.Report( "Spawning visitors...", 0.95f );
		var (gateTx, gateTy) = ReadEntranceTile( standard );
		var gate = grid.PointToWorld( gateTx, gateTy );
		Log.Info( $"[park] entrance gate at tile ({gateTx:0.0},{gateTy:0.0})" );
		for ( int i = 0; i < 30; i++ )
		{
			// Fan the queue back from the gate (just outside it) so arrivals don't all stack on one tile.
			var spawn = gate + new Vector3( (i % 6 - 2.5f) * 10f, -(i / 6) * 14f - 10f, 0 );
			_ = new Peep( terrain, parkQueues, spawn, i, pathGraph );
		}
		Log.Info( $"[park] build mode ready ({catalog.Count} catalog items), {parkQueues.Count} queues" );
		// Staff start empty too — the player hires entertainers/handymen/guards/researchers from the
		// catalog (T-043), each charged a hire cost and then drawing wages.
	}

	// The park's ride queues — placed rides register here so peeps (which hold this list) can use them.
	private static List<RideQueue> parkQueues = new();

	// Placement context captured for save/load (T-059): a loaded save rebuilds rides/shops through these.
	private static PlacementGrid? saveGrid;
	private static ParkTerrain? saveTerrain;
	private static IReadOnlyList<BuildCatalogItem>? saveCatalog;

	/// <summary>Snapshot the park into a <see cref="SaveGame"/> (T-059): balance + loans, the in-game clock,
	/// and the placed rides/shops.</summary>
	public static SaveGame CaptureSave()
	{
		var s = new SaveGame();
		if ( ParkFinances.Current is { } fin )
		{
			s.Money = fin.Money;
			s.EntryFee = fin.EntryFee;
			foreach ( var l in fin.Loans )
				s.Loans.Add( new SaveGame.LoanState { Name = l.Name, Bought = l.Bought, Outstanding = l.Outstanding, Monthly = l.Monthly } );
		}
		s.ClockSeconds = GameClock.Current?.ElapsedSeconds ?? 0f;

		foreach ( var r in Entity.All.OfType<Ride>() )
			s.Placements.Add( new SaveGame.Placement
			{
				Kind = "ride", Name = Path.GetFileName( r.Archive ),
				TileX = r.TileX, TileY = r.TileY, Rotation = r.Rotation, TicketPrice = r.TicketPrice,
			} );
		foreach ( var sh in Shop.Stalls )
			s.Placements.Add( new SaveGame.Placement
			{
				Kind = "shop", Name = ShopCatalogName( sh.Kind ), TileX = sh.TileX, TileY = sh.TileY,
			} );
		return s;
	}

	/// <summary>Restore a park from a <see cref="SaveGame"/> (T-059): clear the current rides/shops, restore the
	/// balance + clock, then rebuild each placement for free (the balance is already restored).</summary>
	public static void ApplySave( SaveGame s )
	{
		if ( saveGrid is not { } grid || saveTerrain is not { } terrain || saveCatalog is not { } catalog )
			return;

		foreach ( var r in Entity.All.OfType<Ride>().ToList() )
			DemolishRide( r, grid );
		foreach ( var sh in Shop.Stalls.ToList() )
			DemolishShop( sh, grid );

		if ( ParkFinances.Current is { } fin )
		{
			fin.RestoreMoney( s.Money, s.EntryFee );
			foreach ( var saved in s.Loans )
				foreach ( var l in fin.Loans )
					if ( l.Name == saved.Name )
					{
						l.Bought = saved.Bought; l.Outstanding = saved.Outstanding; l.Monthly = saved.Monthly;
					}
		}
		GameClock.Current?.SetElapsed( s.ClockSeconds );

		foreach ( var p in s.Placements )
		{
			var item = catalog.FirstOrDefault( c => c.Name == p.Name );
			if ( item.Name == null )
				continue;
			if ( !CommitPlacement( item, grid, terrain, p.TileX, p.TileY, p.Rotation, charge: false ) )
				continue;
			if ( p.Kind == "ride" )
				if ( Entity.All.OfType<Ride>().FirstOrDefault( r => r.TileX == p.TileX && r.TileY == p.TileY ) is { } ride )
					ride.TicketPrice = p.TicketPrice;
		}
		Log.Info( $"[save] restored {s.Placements.Count} placement(s)" );
	}

	// The build-catalog item name for a shop kind (the inverse of ShopKindOf).
	private static string ShopCatalogName( ShopKind kind ) => kind switch
	{
		ShopKind.Drink => "drink",
		ShopKind.Toilet => "toilet",
		_ => "shop",
	};

	// Build catalog: jungle rides (footprint from RideShape, cost from the .sam) + a food shop + hireable
	// staff (no footprint — placed on a tile and charged a hire cost, T-043).
	private static List<BuildCatalogItem> BuildCatalog()
	{
		var list = new List<BuildCatalogItem>();
		foreach ( var path in new[] { "levels/jungle/rides/totem", "levels/jungle/rides/monkey", "levels/jungle/rides/wateride", "levels/jungle/rides/coaster1", "levels/jungle/rides/gokarts", "levels/jungle/rides/tourride", "levels/jungle/rides/bumper" } )
		{
			var name = Path.GetFileName( path );
			var shape = RideShape.Load( path, name );
			list.Add( new BuildCatalogItem( name, path, null, shape.Width, shape.Height, ReadRideCost( path, name ) ) );
		}
		// Sideshows (T-058): the level's stalls/games — placed like rides (their wad carries shape/mesh/script),
		// but peeps play them for pay-to-play takings rather than a flat ticket (see Ride.IsSideshow).
		foreach ( var name in new[] { "puzzle", "squark", "hyenas", "junspray", "arc2x3" } )
		{
			var path = $"levels/jungle/sideshow/{name}";
			var shape = RideShape.Load( path, name );
			list.Add( new BuildCatalogItem( name, path, null, shape.Width, shape.Height, ReadRideCost( path, name ) ) );
		}
		list.Add( new BuildCatalogItem( "shop", null, null, 2, 2, 500f ) );   // food stall (satisfies hunger)
		list.Add( new BuildCatalogItem( "drink", null, null, 2, 2, 450f ) );  // drink stall (satisfies thirst, T-039)
		list.Add( new BuildCatalogItem( "toilet", null, null, 2, 2, 400f ) ); // toilet (relieves bladder, free to use, T-039)
		list.Add( new BuildCatalogItem( "entertainer", null, StaffRole.Entertainer, 1, 1, 800f ) );
		list.Add( new BuildCatalogItem( "handyman", null, StaffRole.Handyman, 1, 1, 600f ) );
		list.Add( new BuildCatalogItem( "guard", null, StaffRole.Guard, 1, 1, 1000f ) );
		list.Add( new BuildCatalogItem( "mechanic", null, StaffRole.Mechanic, 1, 1, 900f ) ); // repairs broken rides (T-032)
		list.Add( new BuildCatalogItem( "researcher", null, StaffRole.Researcher, 1, 1, 1500f ) );
		return list;
	}

	/// <summary>
	/// The park's entrance gate, in heightfield tile coordinates (T-050). TPW levels declare a fixed
	/// 2-tile entrance (<c>FixedItemInfo.EntranceA/B PosX/Y</c> in <c>Standard.sam</c>); this returns the
	/// centre of that span (+0.5 to land on the tile centre). Falls back to the heightfield centre if the
	/// keys are absent. Pure, so it can be unit-tested.
	/// </summary>
	public static (float TileX, float TileY) ReadEntranceTile( SettingsFile s )
	{
		if ( TryFloat( s, "FixedItemInfo.EntranceAPosX", out var ax ) && TryFloat( s, "FixedItemInfo.EntranceAPosY", out var ay ) )
		{
			float bx = TryFloat( s, "FixedItemInfo.EntranceBPosX", out var v1 ) ? v1 : ax;
			float by = TryFloat( s, "FixedItemInfo.EntranceBPosY", out var v2 ) ? v2 : ay;
			return ((ax + bx) / 2f + 0.5f, (ay + by) / 2f + 0.5f);
		}
		float w = TryFloat( s, "MapInfo.HeightfieldWidth", out var ww ) ? ww : 95f;
		float h = TryFloat( s, "MapInfo.HeightfieldHeight", out var hh ) ? hh : 84f;
		return (w / 2f, h / 2f);
	}

	private static bool TryFloat( SettingsFile s, string key, out float value ) =>
		float.TryParse( s[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value );

	private static float ReadRideCost( string path, string name )
	{
		try
		{
			var s = new SettingsFile( FileSystem.OpenRead( $"{path}/{name}.sam" ) );
			return int.TryParse( s["Upgrades[0].CostOfUpgrade"], out var v ) ? v : 2000f;
		}
		catch { return 2000f; }
	}

	// Commit a placement at tile (tx,ty) with an orientation (rotation in 90° CW steps): validate +
	// (for rides/shops) reserve the grid, spawn, charge. A rotated ride footprint swaps W/H on odd turns.
	// charge=false rebuilds a placement for free (a loaded save already restored the balance — T-059).
	private static bool CommitPlacement( BuildCatalogItem item, PlacementGrid grid, ParkTerrain terrain, int tx, int ty, int rotation = 0, bool charge = true )
	{
		var fin = ParkFinances.Current;
		if ( charge && fin != null && !fin.CanAfford( item.Cost ) )
			return false;

		// Staff are mobile — hire + drop at the tile, no grid reservation.
		if ( item.Staff is { } role )
		{
			var c = grid.TileToWorld( tx, ty );
			// A wide patrol radius so staff actually cover the park — guards reach the queue backs where
			// unhappy peeps vandalise, handymen reach far litter, entertainers cover more of the crowd (T-039).
			_ = new Staff( role, terrain, c.WithZ( 0 ), roam: 160f );
			if ( charge ) fin?.PayBuild( item.Cost );
			return true;
		}

		// A rotated ride footprint swaps width/height on odd quarter-turns; shops/staff are unaffected.
		int rw = item.RidePath != null && rotation % 2 == 1 ? item.Height : item.Width;
		int rh = item.RidePath != null && rotation % 2 == 1 ? item.Width : item.Height;

		// Reserve via a footprint mask (T-052): an .hmp template (when the item carries one) reserves only
		// its solid tiles; otherwise a plain rw×rh rectangle, identical to the previous behaviour.
		var footprint = ResolveFootprint( item, rw, rh );

		if ( !grid.TryPlace( tx, ty, footprint ) )
			return false;

		bool ok = item.RidePath == null
			? SpawnShopAt( terrain, grid, tx, ty, ShopKindOf( item.Name ), item )
			: SpawnRideAt( item.RidePath, grid, terrain, tx, ty, item.Cost, rotation );

		if ( !ok )
		{
			grid.Clear( tx, ty, footprint );
			return false;
		}
		if ( charge ) fin?.PayBuild( item.Cost );
		return true;
	}

	// The footprint a catalog item reserves (T-052): its .hmp template's solid-tile mask when one is set
	// and loads, else a plain rw×rh rectangle. Loading failures (missing file / bad data) fall back to the
	// rectangle so placement never breaks on a stray template path.
	private static PlacementFootprint ResolveFootprint( BuildCatalogItem item, int rw, int rh )
	{
		if ( string.IsNullOrEmpty( item.HmpPath ) )
			return PlacementFootprint.Rectangle( rw, rh );

		try
		{
			return PlacementFootprint.FromHmp( new HmpFile( FileSystem.OpenRead( item.HmpPath ) ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[build] .hmp footprint '{item.HmpPath}' unavailable ({e.Message}); using {rw}×{rh} rectangle." );
			return PlacementFootprint.Rectangle( rw, rh );
		}
	}

	// Sell/demolish a placed ride (T-041): drop its queue so peeps stop targeting it, free its footprint +
	// queue-path grid cells, refund part of the build cost, and tear down all its entities. The grid lives
	// in SetupDevPark, so this is wired as the BuildMode demolish callback.
	private static void DemolishRide( Ride ride, PlacementGrid grid )
	{
		parkQueues.RemoveAll( q => q.Ride == ride );
		grid.Clear( ride.TileX, ride.TileY, ride.TileW, ride.TileH );
		foreach ( var (cx, cy) in ride.QueuePathCells )
			grid.Clear( cx, cy, 1, 1 );

		var refund = ride.BuildCost * Ride.SellRefundFraction;
		ParkFinances.Current?.RefundBuild( refund );
		ride.Despawn();
		Log.Info( $"[build] sold {ride.Name} for ${refund:0} (built ${ride.BuildCost:0})" );
	}

	// Spawns a ride on its (already reserved) footprint: model, entrance/exit markers, queue path, and
	// registers its RideQueue so peeps can ride it. Starts idle (occupancy drives the animation).
	private static bool SpawnRideAt( string path, PlacementGrid grid, ParkTerrain terrain, int tx, int ty, float cost, int rotation = 0 )
	{
		try
		{
			// The ride rotates its own footprint internally; use the rotated dims for the centre + grid.
			var shape = RideShape.Load( path, Path.GetFileName( path ) ).Rotated( rotation );
			var w = grid.TileToWorld( tx, ty, shape.Width, shape.Height );
			var ride = new Ride( path, w.WithZ( terrain.SampleHeight( w.X, w.Y ) ), rotation );
			ride.SetActive( false );
			ride.BuildCost = cost; // remembered for the sell refund (T-041)
			ride.TileX = tx; ride.TileY = ty; ride.TileW = ride.Shape.Width; ride.TileH = ride.Shape.Height;
			// Fix the node field's world placement now the footprint is known, so static (walk/head/particle)
			// nodes worldise at the ride's real origin/orientation/size (T-048). Moving car/seat nodes are
			// published by the vehicle each frame regardless.
			ride.NodeField.Configure( ride.Position, rotation, ride.TileW * grid.TileSize, ride.TileH * grid.TileSize );
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

			// Car rides (tour/go-karts/water/bumper — scripts use TOUR/BUMP) get a visible moving wagon (T-032).
			if ( ride.IsCarRide )
				ride.Vehicle = new RideVehicle( ride, grid.TileSize, terrain );

			return true;
		}
		catch ( Exception e ) { Log.Warning( $"[build] ride '{path}' failed: {e.Message}" ); return false; }
	}

	// The shop kind for a catalog item name.
	private static ShopKind ShopKindOf( string name ) => name switch
	{
		"drink" => ShopKind.Drink,
		"toilet" => ShopKind.Toilet,
		_ => ShopKind.Food,
	};

	private static bool SpawnShopAt( ParkTerrain terrain, PlacementGrid grid, int tx, int ty, ShopKind kind, BuildCatalogItem item )
	{
		var c = grid.TileToWorld( tx, ty, item.Width, item.Height );
		_ = new Shop( terrain, c, kind )
		{
			TileX = tx, TileY = ty, TileW = item.Width, TileH = item.Height, BuildCost = item.Cost,
		};
		return true;
	}

	// Sell/demolish a placed stall (T-041): free its footprint cells, refund part of the cost, remove it.
	private static void DemolishShop( Shop shop, PlacementGrid grid )
	{
		grid.Clear( shop.TileX, shop.TileY, shop.TileW, shop.TileH );
		var refund = shop.BuildCost * Ride.SellRefundFraction;
		ParkFinances.Current?.RefundBuild( refund );
		shop.Despawn();
		Log.Info( $"[build] sold {shop.Name} for ${refund:0} (built ${shop.BuildCost:0})" );
	}

	// Visualises a ride's entrance/exit cells (from its Info.Shape) as small markers on the terrain —
	// green = entrance (where the queue connects), red = exit (where peeps appear). The sub-tile stand
	// point comes from the ride's UsageInfo entry/exit positions.
	private static void PlaceEntranceExitMarkers( Ride ride, PlacementGrid grid, ParkTerrain terrain, int tx, int ty )
	{
		if ( ride.Shape.Entrance is { } e )
		{
			var p = grid.PointToWorld( tx + e.X + ride.EntryStandPos.X, ty + e.Y + ride.EntryStandPos.Y );
			ride.OwnedEntities.Add( SpawnMarker( p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f ), 40, 220, 60 ) );
		}
		if ( ride.Shape.Exit is { } x )
		{
			var p = grid.PointToWorld( tx + x.X + ride.ExitAppearPos.X, ty + x.Y + ride.ExitAppearPos.Y );
			ride.OwnedEntities.Add( SpawnMarker( p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f ), 230, 40, 40 ) );
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
		// Sideshow shapes mark only an exit cell (no separate entrance) — peeps queue at, play, and leave from
		// that one cell (T-058). Fall back to it so a stall still gets a queue and earns takings.
		if ( (ride.Shape.Entrance ?? ride.Shape.Exit) is not { } e )
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
			grid.MarkPath( cx, cy ); // reserved against placement, but peeps may walk it (T-036)
			ride.QueuePathCells.Add( (cx, cy) ); // freed on sell/demolish (T-041)

			var w = OnGround( grid.TileToWorld( cx, cy ) );
			laid.Add( w );
			ride.OwnedEntities.Add( new ModelEntity
			{
				Model = Primitives.Plane.GenerateModel( material ),
				Position = w.WithZ( w.Z + 0.15f ),
				Scale = new Vector3( grid.TileSize / 2f ),
			} );
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

	private static ModelEntity SpawnMarker( Vector3 position, byte r, byte g, byte b )
	{
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		material.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
		return new ModelEntity { Model = Primitives.Cube.GenerateModel( material ), Position = position, Scale = new Vector3( 4f ) };
	}

	private void SetupHud()
	{
		LoadProgress.Report( "Loading interface...", 0.95f );
		Hud = new();

		// Separate the front-end from the in-park HUD: the lobby's logo + player/quit buttons only belong
		// in the lobby (they were drawing over the loaded park), and the build/manage HUD only in a park.
		if ( InPark )
		{
			Hud.AddChild( new DayNightOverlay() ); // day/night wash beneath the weather + HUD (T-056)
			Hud.AddChild( new WeatherOverlay() );  // rain/snow tint + precipitation under the rest of the HUD (T-056)
			Hud.AddChild( new ParkStatsPanel() ); // live park finances/visitors readout
			Hud.AddChild( new BuildPanel() );     // clickable build catalog (T-038)
			Hud.AddChild( new ManagePanel() );    // clickable economy/ride manage buttons (T-038)
			Hud.AddChild( new FinancePanel() );   // F11 income/expense graph (T-049)
			Hud.AddChild( new MinimapPanel() );   // corner minimap (M toggles, click pans) (T-057)
		}
		else
		{
			var layout = new LobbyLayout() { Hud = Hud };
			layout.OnInit();
		}

		// Advisor (T-046): the real bug-head character that gives the player tips — driven by the live park
		// state + the Advisor.sam pacing rules, lip-syncing the speech clip via its .LIP. On by default in a
		// park (GameSettings.AdvisorEnabled, the original's AdvisorOn); the env var forces it on anywhere
		// (lobby / testing) regardless of the setting.
		if ( Environment.GetEnvironmentVariable( "OPENTPW_ADVISOR_DEMO" ) == "1"
			|| (InPark && GameSettings.Current.AdvisorEnabled) )
		{
			_ = new Advisor();
			Hud.AddChild( new AdvisorPanel() );
		}

		Hud.AddChild( new OptionsPanel() ); // F10 audio volume sliders (T-051), available in both states
		Hud.AddChild( new Cursor() );       // the cursor is shown in both states
	}

	private static float nextEconLog;

	public void Update()
	{
		// Iterate a snapshot: an entity's update may spawn or remove entities (e.g. peeps dropping litter,
		// handymen clearing it), which would otherwise invalidate the enumeration.
		foreach ( var entity in Entity.All.ToArray() )
			entity.Update();

		GameClock.Current?.Tick( Time.Delta ); // advances the calendar; fires OnNewMonth → finances settle (T-053)
		WeatherAudio.Update();                 // rain loop + thunder claps follow the weather (T-056)

		// Diagnostic: periodically report the park's balance and the cumulative income/cost flows.
		if ( Environment.GetEnvironmentVariable( "OPENTPW_ECON_DEBUG" ) != null && ParkFinances.Current is { } f && Time.Now > nextEconLog )
		{
			nextEconLog = Time.Now + 5f;
			Log.Info( $"[econ] money={f.Money:0}  ride+={f.RideRevenue:0}  entry+={f.EntryRevenue:0}  shop+={f.FoodRevenue:0}  upkeep-={f.UpkeepPaid:0}  wages-={f.WagesPaid:0}"
				+ $"  vandalism={Peep.VandalismActs} deterred={Peep.VandalismDeterred} toilet={Peep.ToiletVisits}"
				+ $"  breakdowns={Ride.Breakdowns} repairs={Ride.Repairs}" );
		}
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
