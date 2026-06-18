namespace OpenTPW;

/// <summary>What a member of staff does as they roam the park.</summary>
public enum StaffRole
{
	/// <summary>Lifts the mood of nearby visitors.</summary>
	Entertainer,
	/// <summary>Seeks out and picks up dropped litter.</summary>
	Handyman,
	/// <summary>Patrols the park; visitors near a guard don't drop litter.</summary>
	Guard,
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
	private const float WalkFps = 8f;          // walk-cycle frames per second
	private const float GuardDeterRadius = 45f; // visitors within this of a guard don't litter

	// One shared billboard model per role (built once), so the crowd reads staff roles by colour.
	private static readonly Dictionary<StaffRole, Model> roleModels = new();
	private static readonly string[] EntertainerSprites = { "SPR_FL", "SPR_GN", "SPR_LA" };

	// Active guards, so peeps can cheaply check whether one is nearby (litter deterrence).
	private static readonly List<Staff> guards = new();

	/// <summary>True if a patrolling guard is within <paramref name="radius"/> of the point.</summary>
	public static bool GuardNear( Vector3 p, float radius = GuardDeterRadius )
	{
		float r2 = radius * radius;
		foreach ( var g in guards )
		{
			float dx = g.Position.X - p.X, dy = g.Position.Y - p.Y;
			if ( dx * dx + dy * dy <= r2 )
				return true;
		}
		return false;
	}

	private readonly StaffRole role;
	private readonly ParkTerrain terrain;
	private readonly Vector3 center;
	private readonly float roam;
	private readonly float speed;
	private Vector3 target;

	// Sprite animation (real sprite path only); falls back to the flat role billboard if unavailable.
	private readonly SpriteSheet? sheet;
	private readonly Model fallback;
	private readonly float spriteHeight;
	private int facing;
	private float walkPhase;
	private bool movedThisFrame;

	public Staff( StaffRole role, ParkTerrain terrain, Vector3 center, float roam )
	{
		this.role = role;
		this.terrain = terrain;
		this.center = center;
		this.roam = roam;

		if ( role == StaffRole.Guard )
			guards.Add( this );

		sheet = RoleSprite( role );
		fallback = RoleModel( role );
		spriteHeight = 16f;
		Model = sheet != null ? sheet.FrameModel( 0 ) : fallback;
		Scale = new Vector3( 3f, 1f, 7f ); // overridden per-frame on the sprite path
		speed = 10f + (float)Random.Shared.NextDouble() * 4f;
		target = PickWanderTarget();
		Position = target;
		DropToGround();
	}

	protected override void OnUpdate()
	{
		ParkFinances.Current?.PayWages( WagePerSecond * Time.Delta );

		switch ( role )
		{
			case StaffRole.Handyman: DoHandyman(); break;
			case StaffRole.Guard: WanderStep(); break; // patrol
			default: DoEntertainer(); break;
		}

		// Cylindrical billboard: yaw about world up so the quad faces the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );

		ApplySprite();
	}

	private static SpriteSheet? RoleSprite( StaffRole role ) => role switch
	{
		StaffRole.Handyman => SpriteSheet.Load( "esprites/Generic/Handymen", "SPR_HA" ),
		StaffRole.Guard => SpriteSheet.Load( "esprites/Generic/Guards", "SPR_GU" ),
		_ => SpriteSheet.Load( "esprites/Fantasy/Entertainers", EntertainerSprites[Random.Shared.Next( EntertainerSprites.Length )] ),
	};

	// Picks the current sprite frame from the facing direction + walk phase, sizing the billboard.
	private void ApplySprite()
	{
		if ( sheet == null )
			return;

		var anims = sheet.Anims;
		int frame = 0;
		if ( anims.Count > 0 )
		{
			var a = anims[facing % anims.Count];
			walkPhase = movedThisFrame ? walkPhase + WalkFps * Time.Delta : 0f;
			frame = a.Start + ( a.Count > 0 ? (int)walkPhase % a.Count : 0 );
		}
		Model = sheet.FrameModel( frame );
		Scale = new Vector3( spriteHeight * sheet.FrameAspect( frame ), 1f, spriteHeight );
	}

	// World movement angle → one of 8 compass sectors.
	private static int DirSector( float dx, float dy )
	{
		int s = (int)MathF.Round( MathF.Atan2( dy, dx ) / (MathF.PI / 4f) );
		return ((s % 8) + 8) % 8;
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
		foreach ( var l in Litter.Active )
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
		movedThisFrame = !arrived;
		if ( !arrived )
		{
			Position += d.Normal * speed * Time.Delta;
			facing = DirSector( d.X, d.Y );
		}
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

		// Distinct fallback colours per role: entertainer orange, handyman blue, guard dark navy.
		var (r, g, b) = role switch
		{
			StaffRole.Handyman => ((byte)40, (byte)90, (byte)220),
			StaffRole.Guard => ((byte)30, (byte)30, (byte)70),
			_ => ((byte)255, (byte)140, (byte)0),
		};
		var model = Billboard.Make( r, g, b );
		roleModels[role] = model;
		return model;
	}
}
