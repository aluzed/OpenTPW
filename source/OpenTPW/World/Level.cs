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
		// within the bar range assigned here.
		LoadProgress.BeginPhase( 0.15f, 0.45f );
		_ = new LobbyIsland( new Vector3( 400, 400, 0 ), "Jungle" );

		LoadProgress.BeginPhase( 0.45f, 0.70f );
		_ = new LobbyIsland( new Vector3( 600, 400, 0 ), "Hallow" );

		LoadProgress.BeginPhase( 0.70f, 0.92f );
		_ = new LobbyIsland( new Vector3( 600, 600, 0 ), "Fantasy" );
		// _ = new LobbyIsland( new Vector3( 400, 600, 0 ), "Space" );

		// Dev test (ride engine slice 1): instantiate one real ride — render its model + run its VM.
		// Guarded so a load failure never breaks the lobby. No park yet, so placement is approximate.
		try
		{
			// Path omits the .wad extension — the VFS mounts "<path>.wad" when a segment matches.
			// Dev placement (no park/grid to site rides yet — that's roadmap stage 7).
			_ = new Ride( "levels/jungle/rides/tourride", new Vector3( 500, 500, 30 ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"dev ride failed to load: {e.Message}" );
		}

		Camera.SetCameraMode<LobbyCameraMode>();
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
