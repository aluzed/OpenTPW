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

		// Place a few rides at tile coordinates near the centre, dropped onto the terrain surface.
		LoadProgress.Report( "Placing rides...", 0.9f );
		int cx = grid.Width / 2, cy = grid.Height / 2;
		var rides = new (string path, int tx, int ty)[]
		{
			("levels/jungle/rides/totem", cx - 4, cy - 4),
			("levels/jungle/rides/monkey", cx + 2, cy - 4),
			("levels/jungle/rides/wateride", cx - 4, cy + 2),
		};
		foreach ( var (path, tx, ty) in rides )
		{
			if ( !grid.TryPlace( tx, ty, 4, 4 ) )
				continue;
			var w = grid.TileToWorld( tx, ty, 4, 4 );
			var wz = w.WithZ( terrain.SampleHeight( w.X, w.Y ) );
			try { _ = new Ride( path, wz ); }
			catch ( Exception e ) { Log.Warning( $"[park] ride '{path}' failed: {e.Message}" ); }
		}
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
