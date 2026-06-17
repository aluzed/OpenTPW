namespace OpenTPW;

/// <summary>What a member of staff does as they roam the park.</summary>
public enum StaffRole
{
	/// <summary>Lifts the mood of nearby visitors.</summary>
	Entertainer,
	/// <summary>Seeks out and picks up dropped litter.</summary>
	Handyman,
}

/// <summary>
/// A member of park staff: wanders the park (a camera-facing billboard in a role colour), draws a wage
/// every second, and does its job — an <see cref="StaffRole.Entertainer"/> lifts the mood of nearby
/// visitors (<see cref="Peep.Cheer"/>), a <see cref="StaffRole.Handyman"/> heads for the nearest litter
/// and picks it up (<see cref="Litter"/>). Guards (safety) follow.
/// </summary>
public sealed class Staff : ModelEntity
{
	private const float WagePerSecond = 1.5f; // ongoing cost
	private const float CheerRadius = 35f;     // entertainer reach
	private const float CheerPerSecond = 8f;   // happiness lifted per second for visitors in range
	private const float ReachDistance = 3f;    // how close counts as "arrived" / "picked up"

	// One shared billboard model per role (built once), so the crowd reads staff roles by colour.
	private static readonly Dictionary<StaffRole, Model> roleModels = new();

	private readonly StaffRole role;
	private readonly ParkTerrain terrain;
	private readonly Vector3 center;
	private readonly float roam;
	private readonly float speed;
	private Vector3 target;

	public Staff( StaffRole role, ParkTerrain terrain, Vector3 center, float roam )
	{
		this.role = role;
		this.terrain = terrain;
		this.center = center;
		this.roam = roam;

		Model = RoleModel( role );
		Scale = new Vector3( 3f, 1f, 7f ); // a touch taller than a peep so staff stand out
		speed = 10f + (float)Random.Shared.NextDouble() * 4f;
		target = PickWanderTarget();
		Position = target;
		DropToGround();
	}

	protected override void OnUpdate()
	{
		ParkFinances.Current?.PayWages( WagePerSecond * Time.Delta );

		if ( role == StaffRole.Handyman )
			DoHandyman();
		else
			DoEntertainer();

		// Cylindrical billboard: yaw about world up so the quad faces the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	// Wander the park and lift the mood of every visitor within reach.
	private void DoEntertainer()
	{
		WanderStep();

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

	// Head for the nearest litter and pick it up; wander if the park is clean.
	private void DoHandyman()
	{
		var litter = NearestLitter();
		if ( litter == null )
		{
			WanderStep();
			return;
		}

		if ( WalkToward( litter.Position ) )
			litter.PickUp();
	}

	private Litter? NearestLitter()
	{
		Litter? best = null;
		float bestD2 = float.MaxValue;
		foreach ( var l in Litter.All )
		{
			float dx = l.Position.X - Position.X, dy = l.Position.Y - Position.Y;
			float d2 = dx * dx + dy * dy;
			if ( d2 < bestD2 )
			{
				bestD2 = d2;
				best = l;
			}
		}
		return best;
	}

	// Walk toward the current wander target on the ground; pick a new one on arrival.
	private void WanderStep()
	{
		if ( WalkToward( target ) )
			target = PickWanderTarget();
	}

	// Walk toward a world point (XY only), dropping onto the terrain. Returns true once within reach.
	private bool WalkToward( Vector3 to )
	{
		var d = new Vector3( to.X - Position.X, to.Y - Position.Y, 0 );
		bool arrived = d.Length < ReachDistance;
		if ( !arrived )
			Position += d.Normal * speed * Time.Delta;
		DropToGround();
		return arrived;
	}

	private Vector3 PickWanderTarget()
	{
		float a = (float)Random.Shared.NextDouble() * MathF.PI * 2f;
		float d = (float)Random.Shared.NextDouble() * roam;
		return new Vector3( center.X + MathF.Cos( a ) * d, center.Y + MathF.Sin( a ) * d, 0 );
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

	private static Model RoleModel( StaffRole role )
	{
		if ( roleModels.TryGetValue( role, out var m ) )
			return m;

		// Entertainer = bright orange, Handyman = blue, so roles are distinguishable in the crowd.
		var (r, g, b) = role == StaffRole.Handyman ? ((byte)40, (byte)90, (byte)220) : ((byte)255, (byte)140, (byte)0);
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
		material.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );

		var vertices = new[]
		{
			new Vertex { Position = new Vector3( -0.5f, 0, 0 ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 0 ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( -0.5f, 0, 1 ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 1 ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		};
		uint[] indices = { 0, 2, 1, 1, 2, 3 };

		var model = new Model( vertices, indices, material );
		roleModels[role] = model;
		return model;
	}
}
