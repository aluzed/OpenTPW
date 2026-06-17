namespace OpenTPW;

/// <summary>
/// A park visitor. Renders as an upright camera-facing billboard (a placeholder until the authentic
/// peep sprites — <c>esprites.wad</c>'s <c>.TPC</c>/<c>.FPC</c>, a custom encoded image format — are
/// decoded). It follows a ride's <see cref="RideQueue"/> to the entrance, <b>boards</b> when a slot is
/// free (hides while the ride runs, occupying a rider slot so a queue forms when the ride is full),
/// then <b>reappears at the exit</b> and heads off to another ride. Always dropped onto the terrain
/// surface; the billboard yaws (about world up) to face the camera.
/// </summary>
public sealed class Peep : ModelEntity
{
	// A small palette of clothing colours so the crowd reads as varied people.
	private static readonly (byte R, byte G, byte B)[] Palette =
	{
		(220, 80, 80), (70, 140, 225), (240, 205, 70), (110, 200, 110), (205, 120, 205), (235, 235, 235)
	};

	private static Model[]? sharedModels;

	private readonly ParkTerrain terrain;
	private readonly IReadOnlyList<RideQueue> queues;
	private readonly float speed;
	private readonly Model billboard;

	private RideQueue? route;
	private RideQueue? lastRoute;
	private float rideTimer;
	private bool riding;

	public Peep( ParkTerrain terrain, IReadOnlyList<RideQueue> queues, Vector3 spawn, int colorIndex )
	{
		this.terrain = terrain;
		this.queues = queues;

		billboard = SharedModel( colorIndex );
		Model = billboard;
		Scale = new Vector3( 3f, 1f, 5f + (float)Random.Shared.NextDouble() * 2f );
		speed = 8f + (float)Random.Shared.NextDouble() * 7f;
		Position = spawn;
		PickRoute();
		DropToGround();
	}

	protected override void OnUpdate()
	{
		// On the ride: hidden, waiting out the ride duration, then reappear at the exit and re-route.
		if ( riding )
		{
			rideTimer -= Time.Delta;
			if ( rideTimer <= 0f )
			{
				riding = false;
				route!.Leave();
				Model = billboard;
				Position = route.ExitPoint;
				PickRoute();
			}
			return;
		}

		if ( route == null )
		{
			PickRoute();
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

		var dest = route.StandPoint( pos );
		var to = new Vector3( dest.X - Position.X, dest.Y - Position.Y, 0 );
		bool atSpot = to.Length < 2.5f;
		if ( !atSpot )
			Position += to.Normal * speed * Time.Delta;

		// Only the front peep boards, and only once it has reached the entrance and a slot is free.
		if ( pos == 0 && atSpot && route.HasFreeSlot )
		{
			route.Board( this );
			riding = true;
			rideTimer = route.RideDuration;
			Model = null;
			return;
		}

		DropToGround();

		// Cylindrical billboard: yaw about world up so the quad's +Y face points at the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

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

	// One upright unit quad (local XZ plane, z = 0..1 standing on the ground, facing +Y), shared per
	// colour — the billboard yaw + per-entity scale do the rest. Double-sided so it shows from any yaw.
	private static Model[] BuildModels()
	{
		var vertices = new[]
		{
			new Vertex { Position = new Vector3( -0.5f, 0, 0 ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 0 ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( -0.5f, 0, 1 ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 1 ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		};
		uint[] indices = { 0, 2, 1, 1, 2, 3 };

		var models = new Model[Palette.Length];
		for ( int i = 0; i < Palette.Length; i++ )
		{
			var (r, g, b) = Palette[i];
			var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
			material.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
			models[i] = new Model( vertices, indices, material );
		}
		return models;
	}
}
