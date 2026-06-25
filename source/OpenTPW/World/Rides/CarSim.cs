namespace OpenTPW;

using SVec2 = System.Numerics.Vector2;

/// <summary>
/// The bumper-car ("dodgem") arena simulation — the re-implementation of the original car engine's
/// <c>BUMP</c> branch (Ghidra <c>FUN_0054a040</c>, ride model decode in docs/08): each car steers toward a
/// <b>random target</b> inside the arena, picks a new one on arrival, bounces off the arena walls, and
/// <b>collides pairwise</b> with the other cars (proximity test → push apart + reflect) — exactly the
/// random-node + proximity-collision logic the original runs (`FUN_00516330 % count`, then the per-pair
/// distance check). Distinct from the tour/kart <i>circuit</i> motion (a sequential loop), which
/// <see cref="RideVehicle"/> drives directly.
///
/// <para>Cars live in <b>local arena coordinates</b> (centred on the ride, world units), so the sim is pure
/// and unit-testable; <see cref="RideVehicle"/> maps each car to world + terrain. The waypoint world
/// positions remain a stand-in (they're runtime sim output, not file data — docs/08/T-048); what this
/// re-implements faithfully is the <b>motion behaviour</b>.</para>
/// </summary>
public sealed class CarSim
{
	/// <summary>One dodgem: position + velocity in local arena coords, its current random target, and the
	/// heading (radians) its visual faces.</summary>
	public struct Car
	{
		public SVec2 Pos;
		public SVec2 Vel;
		public SVec2 Target;
		public float Heading;
	}

	private static readonly Random Shared = new();

	private readonly Car[] cars;
	private readonly float halfW, halfH; // arena half-extents (world units)
	private readonly float radius;       // car collision radius
	private readonly float speed;        // cruising speed (world units / s)
	private const float SteerRate = 3f;  // how fast a car eases toward its desired velocity
	private const float Eps = 1e-4f;

	/// <summary>Random source in [0,1) — injectable so tests are deterministic (default: shared RNG).</summary>
	public Func<float> NextUnit { get; set; }

	public CarSim( int count, float halfW, float halfH, float radius, float speed, Func<float>? rng = null )
	{
		this.halfW = MathF.Max( halfW, 1f );
		this.halfH = MathF.Max( halfH, 1f );
		this.radius = MathF.Max( radius, 0.1f );
		this.speed = speed;
		NextUnit = rng ?? (() => (float)Shared.NextDouble());

		cars = new Car[Math.Max( 0, count )];
		for ( int i = 0; i < cars.Length; i++ )
			cars[i] = new Car { Pos = RandomPoint(), Target = RandomPoint() };
	}

	public IReadOnlyList<Car> Cars => cars;

	/// <summary>Test hook: deterministically place a car (visible to OpenTPW.Tests only).</summary>
	internal void PlaceForTest( int i, SVec2 pos, SVec2 target )
	{
		cars[i].Pos = pos;
		cars[i].Vel = SVec2.Zero;
		cars[i].Target = target;
		cars[i].Heading = 0f;
	}

	/// <summary>Advance the arena by <paramref name="dt"/> seconds: steer each car toward its target (new
	/// random target on arrival), integrate, bounce off the walls, then resolve pairwise collisions.</summary>
	public void Step( float dt )
	{
		if ( dt <= 0f )
			return;

		for ( int i = 0; i < cars.Length; i++ )
		{
			ref var c = ref cars[i];

			var toTarget = c.Target - c.Pos;
			float d = toTarget.Length();
			if ( d < radius )
			{
				c.Target = RandomPoint(); // reached → pick a new random waypoint (the BUMP random-node pick)
				toTarget = c.Target - c.Pos;
				d = toTarget.Length();
			}

			var desired = d > Eps ? toTarget / d * speed : SVec2.Zero;
			c.Vel += (desired - c.Vel) * MathF.Min( 1f, SteerRate * dt ); // ease toward desired (momentum)
			c.Pos += c.Vel * dt;

			// Bounce off the arena walls (reflect the normal velocity component, clamp inside).
			if ( c.Pos.X < -halfW ) { c.Pos.X = -halfW; c.Vel.X = MathF.Abs( c.Vel.X ); }
			else if ( c.Pos.X > halfW ) { c.Pos.X = halfW; c.Vel.X = -MathF.Abs( c.Vel.X ); }
			if ( c.Pos.Y < -halfH ) { c.Pos.Y = -halfH; c.Vel.Y = MathF.Abs( c.Vel.Y ); }
			else if ( c.Pos.Y > halfH ) { c.Pos.Y = halfH; c.Vel.Y = -MathF.Abs( c.Vel.Y ); }

			if ( c.Vel.LengthSquared() > Eps )
				c.Heading = MathF.Atan2( c.Vel.Y, c.Vel.X );
		}

		ResolveCollisions();

		// A collision can shove a car past a wall — keep every car inside the arena after resolving.
		for ( int i = 0; i < cars.Length; i++ )
		{
			cars[i].Pos.X = Math.Clamp( cars[i].Pos.X, -halfW, halfW );
			cars[i].Pos.Y = Math.Clamp( cars[i].Pos.Y, -halfH, halfH );
		}
	}

	// Pairwise proximity collision (the dodgem "bump"): when two cars overlap, push them apart along the
	// contact normal and reflect the normal velocity component so they visibly bounce off each other.
	private void ResolveCollisions()
	{
		float minDist = radius * 2f;
		for ( int i = 0; i < cars.Length; i++ )
			for ( int j = i + 1; j < cars.Length; j++ )
			{
				var delta = cars[j].Pos - cars[i].Pos;
				float dist = delta.Length();
				if ( dist >= minDist || dist < Eps )
					continue;

				var n = delta / dist;
				float push = (minDist - dist) * 0.5f;
				cars[i].Pos -= n * push;
				cars[j].Pos += n * push;

				// Reflect each car's velocity component along the normal (swap of normal momentum, damped).
				float vi = SVec2.Dot( cars[i].Vel, n );
				float vj = SVec2.Dot( cars[j].Vel, n );
				cars[i].Vel += n * (vj - vi) * 0.8f;
				cars[j].Vel += n * (vi - vj) * 0.8f;
			}
	}

	private SVec2 RandomPoint()
		=> new( (NextUnit() * 2f - 1f) * halfW, (NextUnit() * 2f - 1f) * halfH );
}
