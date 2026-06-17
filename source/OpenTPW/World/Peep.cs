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
	private int routeIndex;
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

		if ( routeIndex < route.Waypoints.Count )
		{
			// Walk toward the next queue waypoint.
			var dest = route.Waypoints[routeIndex];
			var to = new Vector3( dest.X - Position.X, dest.Y - Position.Y, 0 );
			if ( to.Length < 2.5f )
				routeIndex++;
			else
				Position += to.Normal * speed * Time.Delta;
		}
		else if ( route.HasFreeSlot )
		{
			// At the entrance with a free slot — board: occupy a rider slot and hide for the ride.
			route.Board();
			riding = true;
			rideTimer = route.RideDuration;
			Model = null;
			return;
		}
		// else: ride full — wait at the entrance (a queue builds up).

		DropToGround();

		// Cylindrical billboard: yaw about world up so the quad's +Y face points at the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

	// Pick a random ride's queue to head for; its waypoints run from the outer end up to the entrance.
	private void PickRoute()
	{
		route = queues.Count > 0 ? queues[Random.Shared.Next( queues.Count )] : null;
		routeIndex = 0;
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
