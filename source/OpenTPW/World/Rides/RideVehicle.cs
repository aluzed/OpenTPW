namespace OpenTPW;

/// <summary>
/// A generic ride car for the "car" rides — tour rides, go-karts, water rides, bumpers (their scripts use
/// the <c>TOUR</c>/<c>BUMP</c> opcodes). It runs a loop around the ride's footprint carrying the ride's
/// riders (occupancy-driven, like the coaster's <see cref="CoasterTrain"/>), giving these rides a visible
/// moving wagon instead of a static model. T-032.
///
/// <para>The path is a <b>generated</b> ring inside the footprint, not the ride's authored track: the real
/// car path lives in the ride's data (tour nodes / track geometry) which isn't decoded yet, and the
/// <c>TOUR</c>/<c>BUMP</c> opcodes that drive the authentic car-object engine are multiplexed commands
/// over a car class we don't model. So this is a visible stand-in (like the light/particle proxies); the
/// car moves while the ride is running and carries seat markers for its live occupancy.</para>
/// </summary>
public sealed class RideVehicle : Entity
{
	private const int Seats = 4;
	private const float Speed = 14f; // world units / second along the loop
	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );

	private readonly Ride ride;
	private readonly Vector3[] loop; // closed ring of ground points
	private readonly float[] cum;    // cumulative arc length
	private readonly float length;
	private readonly ModelEntity car;
	private readonly ModelEntity[] seats;
	private float dist;

	public RideVehicle( Ride ride, float tileSize, ParkTerrain terrain )
	{
		this.ride = ride;

		// An elliptical ring inside the footprint, sampled onto the terrain (a generated stand-in path).
		const int n = 48;
		float rx = MathF.Max( tileSize, ride.TileW * tileSize * 0.32f );
		float ry = MathF.Max( tileSize, ride.TileH * tileSize * 0.32f );
		var c = ride.Position;
		loop = new Vector3[n + 1];
		for ( int i = 0; i <= n; i++ )
		{
			float a = i / (float)n * MathF.Tau;
			var p = new Vector3( c.X + MathF.Cos( a ) * rx, c.Y + MathF.Sin( a ) * ry, 0 );
			loop[i] = p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f );
		}
		cum = new float[loop.Length];
		for ( int i = 1; i < loop.Length; i++ )
			cum[i] = cum[i - 1] + loop[i].Distance( loop[i - 1] );
		length = cum[^1];

		var carMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		carMat.Set( "Color", new Texture( [200, 60, 60, 255], 1, 1 ) ); // red car
		car = new ModelEntity
		{
			Model = Primitives.Cube.GenerateModel( carMat ),
			Scale = new Vector3( tileSize * 0.18f, tileSize * 0.34f, tileSize * 0.18f ), // long along local +Y
			Position = Offscreen,
		};

		var seatMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		seatMat.Set( "Color", new Texture( [250, 220, 90, 255], 1, 1 ) ); // bright rider markers
		seats = new ModelEntity[Seats];
		for ( int i = 0; i < Seats; i++ )
			seats[i] = new ModelEntity { Model = Primitives.Cube.GenerateModel( seatMat ), Scale = new Vector3( tileSize * 0.06f ), Position = Offscreen };
	}

	/// <summary>Despawn the car + rider markers (called when the ride is sold/demolished).</summary>
	public void Despawn()
	{
		Entity.All.Remove( car );
		foreach ( var s in seats )
			Entity.All.Remove( s );
		Entity.All.Remove( this );
	}

	protected override void OnUpdate()
	{
		if ( length < 1e-3f )
			return;

		// The car circulates only while the ride is actually running (has riders, not broken down).
		if ( ride.Riders > 0 && !ride.IsBroken )
			dist = (dist + Speed * Time.Delta) % length;

		var (pos, tan) = Sample( dist );
		car.Position = pos;
		// The box car is long along local +Y, so align +Y with the travel tangent.
		float yaw = MathF.Atan2( tan.Y, tan.X ) - MathF.PI / 2f;
		var rot = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
		car.Rotation = rot;

		int occupied = Math.Min( ride.Riders, Seats );
		var side = new Vector3( -tan.Y, tan.X, 0f ).Normal;
		for ( int i = 0; i < Seats; i++ )
		{
			if ( i < occupied )
			{
				float off = (i % 2 == 0 ? 1f : -1f) * 4f;
				seats[i].Position = pos + Vector3.Up * 4f + side * off;
				seats[i].Rotation = rot;
			}
			else
			{
				seats[i].Position = Offscreen;
			}
		}
	}

	// Position + forward tangent at arc-length d along the loop.
	private (Vector3 Pos, Vector3 Tan) Sample( float d )
	{
		int s = 0;
		while ( s < loop.Length - 2 && cum[s + 1] < d )
			s++;
		float seg = cum[s + 1] - cum[s];
		float f = seg > 1e-4f ? (d - cum[s]) / seg : 0f;
		var a = loop[s];
		var b = loop[s + 1];
		return (a + (b - a) * f, (b - a).Normal);
	}
}
