namespace OpenTPW;

/// <summary>
/// A park visitor. Renders as an upright camera-facing billboard (a placeholder until the authentic
/// peep sprites — <c>esprites.wad</c>'s <c>.TPC</c>/<c>.FPC</c>, a custom encoded image format — are
/// decoded). It picks rides (weighted by excitement), queues for them, boards, rides, then re-routes,
/// gaining <see cref="happiness"/> from exciting rides and losing it to long waits while
/// <see cref="energy"/> drains as it walks the park. A tired or fed-up peep heads home and is recycled
/// as a fresh arrival, so the crowd turns over. Always dropped onto the terrain surface; the billboard
/// yaws (about world up) to face the camera.
/// </summary>
public sealed class Peep : ModelEntity
{
	private const float StartHappiness = 50f;  // mood on arrival (0..100)
	private const float MaxEnergy = 100f;       // full on arrival, drains while in the park
	private const float EnergyDrainPerSec = 2f; // walking/queuing tires the peep (~50 s of activity)
	private const float WaitPenaltyPerSec = 1f; // standing in a line that isn't moving sours the mood
	private const float RideRewardScale = 0.3f; // happiness gained = ride excitement × this
	private const float LeaveHappiness = 10f;   // fed up below this
	private const float LitterChancePerSec = 0.04f; // ~1 dropped every ~25 s of wandering
	private const float LitterRadius = 18f;          // litter within this sours the mood
	private const float LitterPenaltyPerSec = 1.5f;  // happiness lost per second per nearby litter (capped)
	private const float HungerPerSec = 2f;           // hunger builds while in the park (0..100)
	private const float HungerThreshold = 55f;        // detour to a shop for a snack at this point
	private const float WalkFps = 8f;                // walk-cycle frames per second

	// A small palette of clothing colours so the crowd reads as varied people.
	private static readonly (byte R, byte G, byte B)[] Palette =
	{
		(220, 80, 80), (70, 140, 225), (240, 205, 70), (110, 200, 110), (205, 120, 205), (235, 235, 235)
	};

	private static Model[]? sharedModels;

	// The kid sprites in esprites.wad — each peep gets a random one so the crowd reads as varied.
	private static readonly string[] KidSprites =
		{ "SPR_BE", "SPR_BI", "SPR_CH", "SPR_FR", "SPR_KI", "SPR_SA", "SPR_SU", "SPR_TA" };

	private readonly SpriteSheet? sheet;

	private readonly ParkTerrain terrain;
	private readonly IReadOnlyList<RideQueue> queues;
	private readonly PathGraph? paths;
	private readonly float speed;
	private readonly Model billboard;

	private readonly Vector3 home; // the park-edge point this peep entered at and heads back to when done

	private RideQueue? route;
	private RideQueue? lastRoute;
	private float rideTimer;
	private bool riding;
	private CoasterTrain? mount; // the coaster train this peep is riding in view (null = ordinary hidden ride)
	private bool leaving;
	private Shop? shopTarget;
	private float happiness = StartHappiness;
	private float energy = MaxEnergy;
	private float hunger;

	// Sprite animation state (real sprite path only).
	private readonly float spriteHeight;
	private int facing;          // 0..7 camera-relative direction sector → directional walk cycle
	private float walkPhase;     // advances while moving; indexes the walk cycle
	private bool movedThisFrame;
	private Vector3 moveDir = new( 0, -1, 0 ); // last world travel direction (drives the facing each frame)

	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );
	private readonly ModelEntity? shadow; // soft ground shadow under the peep (hidden while riding)

	// Pathfinding state (T-036): the active route's waypoints + the goal it was planned for, so a path is
	// reused until the goal moves to another tile rather than re-planned every frame.
	private List<Vector3>? pathPts;
	private int pathIdx;
	private (int X, int Y)? pathGoalTile;

	private static int recycles; // park-wide count of visitors that left and were replaced (diagnostics)

	public Peep( ParkTerrain terrain, IReadOnlyList<RideQueue> queues, Vector3 spawn, int colorIndex, PathGraph? paths = null )
	{
		this.terrain = terrain;
		this.queues = queues;
		this.paths = paths;
		home = spawn;

		// Prefer a real decoded peep sprite (per-frame models, directional walk cycles), a random kid for
		// crowd variety; fall back to a flat-colour billboard if it can't load.
		spriteHeight = 15f + (float)Random.Shared.NextDouble() * 3f;
		sheet = SpriteSheet.Load( "esprites/Generic/Kids", KidSprites[Random.Shared.Next( KidSprites.Length )] );
		if ( sheet != null )
		{
			billboard = sheet.FrameModel( 0 );
			Model = billboard;
			ApplySprite();
		}
		else
		{
			billboard = SharedModel( colorIndex );
			Model = billboard;
			Scale = new Vector3( 3f, 1f, 5f + (float)Random.Shared.NextDouble() * 2f );
		}
		speed = 8f + (float)Random.Shared.NextDouble() * 7f;
		Position = spawn;
		shadow = new ModelEntity { Model = ShadowModel(), Scale = new Vector3( spriteHeight * 0.4f, spriteHeight * 0.4f, 1f ) };
		ParkFinances.Current?.TakeEntryFee(); // pays the gate on arrival
		PickRoute();
		DropToGround();
	}

	protected override void OnUpdate()
	{
		UpdateShadow();

		// On the ride: hidden, waiting out the ride duration, then reappear at the exit, bank the fun, and
		// either head home (tired/fed up) or pick another ride.
		if ( riding )
		{
			rideTimer -= Time.Delta;
			if ( rideTimer <= 0f )
			{
				happiness = Math.Min( 100f, happiness + route!.Ride.Excitement * RideRewardScale );
				riding = false;
				route.Leave();
				mount?.Unboard( this ); // climb off the coaster train (no-op for ordinary rides)
				mount = null;
				Model = billboard;
				Position = route.ExitPoint;
				ResetPath(); // teleported to the exit — replan from here
				if ( WantsToLeave() )
					BeginLeaving();
				else
					PickRoute();
			}
			return;
		}

		// Heading for the exit: walk home (routing around rides), then recycle into a fresh visitor.
		if ( leaving )
		{
			if ( MoveTo( home ) )
				Recycle();
			FaceCamera();
			ApplySprite();
			return;
		}

		// Detouring to a shop: walk there (routing around rides), buy a snack (park income), then resume.
		if ( shopTarget != null )
		{
			if ( MoveTo( shopTarget.Position ) )
			{
				ParkFinances.Current?.TakeFoodSale( shopTarget.Price );
				hunger = 0f;
				shopTarget = null;
				ResetPath();
				PickRoute();
			}
			FaceCamera();
			ApplySprite();
			return;
		}

		if ( route == null )
		{
			PickRoute();
			return;
		}

		// Being in the park is tiring; a long enough day (or a soured mood) sends the peep home.
		energy -= EnergyDrainPerSec * Time.Delta;

		// Visitors occasionally drop litter, and standing among litter sours the mood (until a handyman
		// clears it). Both raise the chance the peep heads home unhappy. A nearby guard deters littering.
		if ( Random.Shared.NextDouble() < LitterChancePerSec * Time.Delta && !Staff.GuardNear( Position ) )
			_ = new Litter( Position );
		int nearbyLitter = CountNearbyLitter();
		if ( nearbyLitter > 0 )
			happiness = Math.Max( 0f, happiness - LitterPenaltyPerSec * nearbyLitter * Time.Delta );

		if ( WantsToLeave() )
		{
			BeginLeaving();
			return;
		}

		// Getting peckish: leave the ride line and head for the nearest shop for a snack.
		hunger += HungerPerSec * Time.Delta;
		if ( hunger >= HungerThreshold && Shop.Stalls.Count > 0 )
		{
			route?.Dequeue( this );
			route = null;
			ResetPath();
			shopTarget = NearestShop();
			return;
		}

		// Stand at my place in line (front = the entrance, each place back steps one waypoint out), and
		// walk toward it — as those ahead board, the line shifts forward and my target advances.
		int pos = route.PositionOf( this );
		if ( pos < 0 )
		{
			route.Enqueue( this );
			pos = route.PositionOf( this );
		}

		bool atSpot = MoveTo( route.StandPoint( pos ) );

		// Only the front peep boards, and only once it has reached the entrance and a slot is free.
		if ( pos == 0 && atSpot && route.HasFreeSlot )
		{
			ParkFinances.Current?.TakeRideTicket( route.Ride.TicketPrice ); // pays to board
			route.Board( this );
			riding = true;
			rideTimer = route.RideDuration;
			// A coaster carries the peep on its train in view; any other ride (or a full/track-less coaster)
			// hides the peep until it reappears at the exit.
			mount = route.Ride.Train;
			if ( mount == null || !mount.TryBoard( this ) )
			{
				mount = null;
				Model = null;
			}
			return;
		}

		// Waiting in a line that isn't moving (not yet at the front) slowly sours the mood.
		if ( atSpot && pos > 0 )
			happiness = Math.Max( 0f, happiness - WaitPenaltyPerSec * Time.Delta );

		FaceCamera();
		ApplySprite();
	}

	// True once a peep is tired out or fed up — time to head for the exit.
	private bool WantsToLeave() => energy <= 0f || happiness <= LeaveHappiness;

	private void BeginLeaving()
	{
		route?.Dequeue( this );
		route = null;
		ResetPath();
		leaving = true;
	}

	// Recycle a departed peep as a fresh arrival, keeping the crowd steady without churning entities.
	private void Recycle()
	{
		recycles++;
		leaving = false;
		shopTarget = null;
		ResetPath();
		happiness = StartHappiness;
		energy = MaxEnergy;
		hunger = 0f;
		lastRoute = null;
		ParkFinances.Current?.TakeEntryFee(); // a fresh visitor pays the gate
		PickRoute();
		if ( Environment.GetEnvironmentVariable( "OPENTPW_PEEP_DEBUG" ) != null )
			Log.Trace( $"[peep] recycled (total {recycles})" );
	}

	// Walk toward a world point (XY only), dropping onto the terrain. Returns true once within reach.
	// Tracks the movement direction (for the directional sprite) and whether we actually moved.
	private bool WalkToward( Vector3 target )
	{
		var to = new Vector3( target.X - Position.X, target.Y - Position.Y, 0 );
		bool arrived = to.Length < 2.5f;
		movedThisFrame = !arrived;
		if ( !arrived )
		{
			Position += to.Normal * speed * Time.Delta;
			moveDir = to.Normal; // world travel direction; the facing is derived from it per frame
		}
		DropToGround();
		return arrived;
	}

	// Walk toward a goal along a path that routes around rides/shops (T-036), falling back to a straight
	// line when there's no pathfinder or no route. The path is re-planned only when the goal lands in a new
	// tile (so a peep shuffling up a queue or chasing a fixed target doesn't re-run A* every frame).
	// Returns true once the peep reaches the goal.
	private bool MoveTo( Vector3 goal )
	{
		if ( paths == null )
			return WalkToward( goal );

		var goalTile = paths.TileOf( goal );
		if ( pathPts == null || pathGoalTile != goalTile )
		{
			pathPts = paths.FindPath( Position, goal );
			pathGoalTile = goalTile;
			pathIdx = 0;
		}

		if ( pathPts == null || pathPts.Count == 0 )
			return WalkToward( goal ); // unreachable / off-grid — head straight there

		bool reachedWaypoint = WalkToward( pathPts[pathIdx] );
		if ( reachedWaypoint && pathIdx < pathPts.Count - 1 )
			pathIdx++;
		return reachedWaypoint && pathIdx >= pathPts.Count - 1;
	}

	// Drop the cached path so the next MoveTo re-plans (called when the peep switches goal/mode).
	private void ResetPath()
	{
		pathPts = null;
		pathGoalTile = null;
	}

	// Picks the current sprite frame from the camera-relative facing + walk phase, holding the cycle's
	// first (standing) frame when idle, at a uniform scale (the quad is hotspot-anchored so feet stay
	// planted and frames don't pulse). No-op on the flat-billboard fallback path.
	private void ApplySprite()
	{
		if ( sheet == null )
			return;

		facing = SpriteFacing.Sector( moveDir ); // matches the on-screen travel direction (T-035)
		var anims = sheet.Anims;
		int frame = 0;
		if ( anims.Count > 0 )
		{
			var a = anims[facing % anims.Count];
			walkPhase = movedThisFrame ? walkPhase + WalkFps * Time.Delta : 0f; // idle holds the standing frame
			frame = a.Start + ( a.Count > 0 ? (int)walkPhase % a.Count : 0 );
		}

		Model = sheet.FrameModel( frame );
		float p2w = spriteHeight / sheet.RefHeight; // one px→world factor for the whole sheet (no jitter)
		Scale = new Vector3( p2w, 1f, p2w );
	}

	// Keep the soft ground shadow under the peep (hidden while it's riding / off the ground).
	private void UpdateShadow()
	{
		if ( shadow == null )
			return;
		shadow.Position = riding
			? Offscreen
			: Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) + 0.12f );
	}

	// A shared flat dark translucent ground decal, so adding peeps/staff allocates nothing extra.
	private static Model? shadowModel;
	internal static Model ShadowModel()
	{
		if ( shadowModel != null )
			return shadowModel;
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
			MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
		material.Set( "Color", new Texture( [0, 0, 0, 90], 1, 1 ) );
		shadowModel = Primitives.Plane.GenerateModel( material );
		return shadowModel;
	}

	/// <summary>An entertainer lifts this peep's mood (capped at full); a happier peep stays longer.</summary>
	public void Cheer( float amount ) => happiness = Math.Min( 100f, happiness + amount );

	/// <summary>Place this peep on a moving coaster seat (T-045 3b): sit at <paramref name="seat"/> and
	/// keep facing the camera. Called by the <see cref="CoasterTrain"/> each frame while the peep is aboard;
	/// the peep's own update skips its walk logic while riding, so the train fully owns its transform.</summary>
	public void SeatAt( Vector3 seat )
	{
		Position = seat;
		FaceCamera();
	}

	// The closest shop to head to when hungry (null if the park has none).
	private Shop? NearestShop()
	{
		Shop? best = null;
		float bestD2 = float.MaxValue;
		foreach ( var s in Shop.Stalls )
		{
			float dx = s.Position.X - Position.X, dy = s.Position.Y - Position.Y;
			float d2 = dx * dx + dy * dy;
			if ( d2 < bestD2 )
			{
				bestD2 = d2;
				best = s;
			}
		}
		return best;
	}

	// Litter within reach, capped so a single filthy spot doesn't drive happiness down instantly.
	private int CountNearbyLitter()
	{
		float r2 = LitterRadius * LitterRadius;
		int count = 0;
		foreach ( var l in Litter.Active )
		{
			float dx = l.Position.X - Position.X, dy = l.Position.Y - Position.Y;
			if ( dx * dx + dy * dy <= r2 && ++count >= 3 )
				break;
		}
		return count;
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

	// Cylindrical billboard: yaw about world up so the quad's +Y face points at the camera.
	private void FaceCamera()
	{
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	// Choose the next ride weighted by its excitement (more exciting rides draw more peeps), avoiding an
	// immediate repeat of the last ride when there's a choice, and join the back of its line.
	private void PickRoute()
	{
		if ( queues.Count == 0 )
		{
			route = null;
			return;
		}

		var candidates = queues.Where( q => q != lastRoute ).ToList();
		if ( candidates.Count == 0 )
			candidates = queues.ToList();

		int Weight( RideQueue q ) => Math.Max( 1, q.Ride.Excitement );
		int roll = Random.Shared.Next( candidates.Sum( Weight ) );
		route = candidates[^1];
		foreach ( var q in candidates )
			if ( ( roll -= Weight( q ) ) < 0 ) { route = q; break; }

		lastRoute = route;
		route.Enqueue( this );
	}

	private static Model SharedModel( int colorIndex )
	{
		sharedModels ??= BuildModels();
		return sharedModels[((colorIndex % Palette.Length) + Palette.Length) % Palette.Length];
	}

	// One shared upright billboard per clothing colour — the yaw + per-entity scale do the rest.
	private static Model[] BuildModels()
	{
		var models = new Model[Palette.Length];
		for ( int i = 0; i < Palette.Length; i++ )
		{
			var (r, g, b) = Palette[i];
			models[i] = Billboard.Make( r, g, b );
		}
		return models;
	}
}
