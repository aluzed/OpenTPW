namespace OpenTPW;

/// <summary>
/// A park entertainer: wanders the park (a camera-facing billboard, like <see cref="Peep"/>, in a bright
/// staff colour), draws a wage every second, and lifts the mood of nearby visitors — so a happier crowd
/// rides more and stays longer. A first slice of park staff; handymen/guards (litter, safety) follow.
/// </summary>
public sealed class Staff : ModelEntity
{
	private const float WagePerSecond = 1.5f; // ongoing cost
	private const float CheerRadius = 35f;     // how close a visitor must be to be cheered
	private const float CheerPerSecond = 8f;   // happiness lifted per second for visitors in range

	private static Model? sharedModel;

	private readonly ParkTerrain terrain;
	private readonly Vector3 center;
	private readonly float roam;
	private readonly float speed;
	private Vector3 target;

	public Staff( ParkTerrain terrain, Vector3 center, float roam )
	{
		this.terrain = terrain;
		this.center = center;
		this.roam = roam;

		Model = SharedModel();
		Scale = new Vector3( 3f, 1f, 7f ); // a touch taller than a peep so staff stand out
		speed = 10f + (float)Random.Shared.NextDouble() * 4f;
		target = PickTarget();
		Position = target;
		DropToGround();
	}

	protected override void OnUpdate()
	{
		ParkFinances.Current?.PayWages( WagePerSecond * Time.Delta );

		// Wander: walk toward the current target, pick a new one on arrival.
		var to = new Vector3( target.X - Position.X, target.Y - Position.Y, 0 );
		if ( to.Length < 3f )
			target = PickTarget();
		else
			Position += to.Normal * speed * Time.Delta;
		DropToGround();

		CheerNearbyPeeps();

		// Cylindrical billboard: yaw about world up so the quad faces the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	// Lift the mood of every visitor within reach (cheap: a handful of staff over ~40 peeps).
	private void CheerNearbyPeeps()
	{
		float lift = CheerPerSecond * Time.Delta;
		float r2 = CheerRadius * CheerRadius;
		foreach ( var e in Entity.All )
		{
			if ( e is not Peep peep )
				continue;
			float dx = peep.Position.X - Position.X, dy = peep.Position.Y - Position.Y;
			if ( dx * dx + dy * dy <= r2 )
				peep.Cheer( lift );
		}
	}

	private Vector3 PickTarget()
	{
		float a = (float)Random.Shared.NextDouble() * MathF.PI * 2f;
		float d = (float)Random.Shared.NextDouble() * roam;
		return new Vector3( center.X + MathF.Cos( a ) * d, center.Y + MathF.Sin( a ) * d, 0 );
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

	// One upright unit quad (local XZ plane, z = 0..1, facing +Y), bright staff colour, double-sided.
	private static Model SharedModel()
	{
		if ( sharedModel != null )
			return sharedModel;

		var vertices = new[]
		{
			new Vertex { Position = new Vector3( -0.5f, 0, 0 ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 0 ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( -0.5f, 0, 1 ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 1 ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		};
		uint[] indices = { 0, 2, 1, 1, 2, 3 };

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
		material.Set( "Color", new Texture( [255, 140, 0, 255], 1, 1 ) ); // bright orange = staff
		sharedModel = new Model( vertices, indices, material );
		return sharedModel;
	}
}
