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

		LoadProgress.Report( "Loading water...", 0.08f );
		_ = new Water() { Scale = new Vector3( 10000f ) };

		LoadProgress.Report( "Loading sky...", 0.12f );
		_ = new Sky();

		// The islands are the bulk of the load; each reports per-mesh sub-progress (LobbyIsland)
		// within the bar range assigned here. The central (400,400) slot is used by the dev park demo
		// below instead of the Jungle island.
		LoadProgress.BeginPhase( 0.45f, 0.70f );
		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Hallow" );

		LoadProgress.BeginPhase( 0.70f, 0.92f );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Fantasy" );

		// Dev test: a real park placement grid (jungle's 95×84 tile heightfield, from Standard.sam),
		// with several rides placed at tile coordinates and sitting on the ground — replacing the
		// earlier single floated ride. Authentic terrain-heightfield rendering is a follow-up; the plot
		// below is a flat stand-in. Guarded so a load failure never breaks the lobby.
		try
		{
			SetupDevPark();
		}
		catch ( Exception e )
		{
			Log.Warning( $"dev park failed to load: {e.Message}" );
		}

		Camera.SetCameraMode<LobbyCameraMode>();
	}

	// Dev demonstration of the placement grid: build the jungle park grid, lay down a flat plot, and
	// place a few rides at tile coordinates on the ground. See PlacementGrid / docs T-032.
	private static void SetupDevPark()
	{
		var parkCentre = new Vector3( 400, 400, 0 );
		var standard = new SettingsFile( "/levels/jungle/Standard.sam" );
		var grid = PlacementGrid.FromLevelSettings( standard, tileSize: 16f, worldCenter: parkCentre );

		// A flat park plot to place rides on (stand-in for the heightfield terrain mesh).
		const int plot = 18;
		var groundMat = new Material<ObjectUniformBuffer>( "content/shaders/3d.shader" );
		try { groundMat.Set( "Color", new Texture( "levels/jungle/terrain/textures/jgr_bas1.wct", TextureFlags.Repeat ) ); }
		catch { groundMat.Set( "Color", Texture.Missing ); }
		_ = new ModelEntity
		{
			Model = Primitives.Plane.GenerateModel( groundMat, new Point2( plot, plot ) ),
			Position = parkCentre.WithZ( 0.05f ),
			Scale = new Vector3( grid.TileSize / 2f ), // a plane repeat is 2 units, so this makes one repeat == one tile
		};

		// Place a few rides at tile coordinates near the grid centre, each on a 4×4 footprint.
		int cx = grid.Width / 2, cy = grid.Height / 2;
		var rides = new (string path, int tx, int ty)[]
		{
			("levels/jungle/rides/totem", cx - 4, cy - 4),
			("levels/jungle/rides/monkey", cx + 1, cy - 4),
			("levels/jungle/rides/wateride", cx - 4, cy + 1),
		};
		foreach ( var (path, tx, ty) in rides )
		{
			if ( !grid.TryPlace( tx, ty, 4, 4 ) )
				continue;
			try { _ = new Ride( path, grid.TileToWorld( tx, ty, 4, 4 ) ); }
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
