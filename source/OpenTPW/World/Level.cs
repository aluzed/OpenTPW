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

		// Centre the placement grid on the terrain's dense centroid (robust to stray distant meshes).
		var standard = new SettingsFile( "/levels/jungle/Standard.sam" );
		var centre = terrain.Centroid;
		var grid = PlacementGrid.FromLevelSettings( standard, tileSize: 16f, worldCenter: new Vector3( centre.X, centre.Y, 0 ) );

		// Overview camera framing the park around the centroid.
		ParkOverviewCameraMode.Target = new Vector3( centre.X, centre.Y, centre.Z );
		ParkOverviewCameraMode.Radius = 240f;
		ParkOverviewCameraMode.Height = centre.Z + 200f;
		Camera.SetCameraMode<ParkOverviewCameraMode>();

		// Lay a few rides out in a row near the centre, each reserving its real tile footprint (from the
		// ride's Info.Shape) on the grid and dropped onto the terrain surface.
		LoadProgress.Report( "Placing rides...", 0.9f );
		var paths = new[] { "levels/jungle/rides/totem", "levels/jungle/rides/monkey", "levels/jungle/rides/wateride" };
		int tx = grid.Width / 2 - 7, ty = grid.Height / 2 - 2;
		foreach ( var path in paths )
		{
			var shape = RideShape.Load( path, Path.GetFileName( path ) );
			if ( !grid.TryPlace( tx, ty, shape.Width, shape.Height ) )
			{
				tx += shape.Width + 1;
				continue;
			}

			var w = grid.TileToWorld( tx, ty, shape.Width, shape.Height );
			var wz = w.WithZ( terrain.SampleHeight( w.X, w.Y ) );
			Log.Info( $"[park] {Path.GetFileName( path )} footprint {shape.Width}x{shape.Height} at tile({tx},{ty})" );
			try
			{
				var ride = new Ride( path, wz );
				PlaceEntranceExitMarkers( ride, grid, terrain, tx, ty );
			}
			catch ( Exception e ) { Log.Warning( $"[park] ride '{path}' failed: {e.Message}" ); }

			tx += shape.Width + 1; // 1-tile gap before the next ride
		}
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

		Hud.AddChild( new Cursor() );
	}

	public void Update()
	{
		Entity.All.ForEach( entity => entity.Update() );
	}

	public void Render()
	{
		Camera.Update();

		Entity.All.ForEach( entity => entity.Render() );
	}
}
