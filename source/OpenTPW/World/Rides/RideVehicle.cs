namespace OpenTPW;

/// <summary>
/// A ride car for the "car" rides — tour rides, go-karts, water rides, bumpers (their scripts use the
/// <c>TOUR</c>/<c>BUMP</c> opcodes). It re-implements the two motion behaviours of the original car engine
/// (Ghidra <c>FUN_0054a040</c>, docs/08), occupancy-driven, giving these rides visible moving cars:
/// <list type="bullet">
///   <item><b>Circuit</b> (tour / kart / water): the cars follow a loop around the footprint in sequence,
///   like the coaster's <see cref="CoasterTrain"/>.</item>
///   <item><b>Arena</b> (bumper / dodgem — the ride's script uses <c>BUMP</c>): the cars roam to random
///   waypoints inside the footprint and bounce off each other and the walls, via <see cref="CarSim"/>.</item>
/// </list>
///
/// <para>The circuit path follows the ride's <b>authored footprint</b> (its <c>.sam</c> shape) and passes
/// the entrance (<see cref="RidePath.FootprintRing"/>), falling back to a generic ellipse for a degenerate
/// footprint. The motion <i>behaviour</i> is faithful to the original; the waypoint <i>positions</i> are a
/// footprint-shaped stand-in, because the real ones are runtime sim output, not file data — the original
/// reads car/seat node positions off matrices bound at runtime (no static path in the assets; docs/08 /
/// T-048). So like the light/particle proxies, the cars move while the ride runs and carry their riders.</para>
/// </summary>
public sealed class RideVehicle : Entity
{
	private const int DefaultSeats = 4;  // when the model declares no car/seat nodes
	private const int MaxSeats = 12;     // cap the visible markers regardless of the authored count
	private const float Speed = 14f;     // world units / second along the loop
	private const float SeatSpacing = 6f; // arc-length gap between trailing riders
	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );

	/// <summary>The visible seat/rider count for a ride with <paramref name="authoredCarNodes"/> car/seat
	/// nodes in its model (T-048): the authored count clamped to [1, <see cref="MaxSeats"/>], or the default
	/// when the model declares none. So a tour ride with nine seat nodes shows nine riders, not a fixed four.</summary>
	public static int SeatCountFor( int authoredCarNodes )
		=> authoredCarNodes <= 0 ? DefaultSeats : Math.Clamp( authoredCarNodes, 1, MaxSeats );

	private readonly Ride ride;
	private readonly int seatCount;
	private readonly Vector3[] loop; // closed ring of ground points
	private readonly float[] cum;    // cumulative arc length
	private readonly float length;
	private readonly ModelEntity car;
	private readonly ModelEntity[] seats;
	private readonly IReadOnlyList<int> seatNodeIds; // car/seat node ids this vehicle drives (T-048)
	private float dist;

	// Bumper/dodgem rides run the arena collision sim instead of the circuit loop (docs/08 — the BUMP
	// branch of the original car engine). Tour/kart/water rides keep the loop (`carSim` stays null).
	private readonly CarSim? carSim;
	private readonly ParkTerrain terrain;
	private readonly Model[]? carFrames; // the bumper's real b_car.MD2 mesh (+ b_carm wobble frame), if loaded
	private float carAnimT;
	private const float CarAnimFps = 6f;

	public RideVehicle( Ride ride, float tileSize, ParkTerrain terrain )
	{
		this.ride = ride;
		this.terrain = terrain;
		seatCount = SeatCountFor( ride.CarNodeCount );
		// The model's car/seat node ids, one per visible seat — we publish each one's live world position
		// to the ride's node field every frame (T-048), so EVENT/SPARK effects addressed to a seat node
		// follow the moving car instead of the ride centre.
		seatNodeIds = ride.NodeField.CarSeatNodeIds.Take( seatCount ).ToList();

		// Prefer a loop that traces the ride's authored footprint (its .sam shape) and passes the entrance;
		// fall back to a generic ellipse for a degenerate footprint (T-048).
		loop = BuildFootprintLoop( ride, tileSize, terrain ) ?? BuildEllipseLoop( ride, tileSize, terrain );
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

		// A bumper ride uses the real dodgem car mesh (b_car.MD2) driven by the arena collision sim; tour/
		// kart seats are small rider markers along the loop.
		bool isBumper = ride.IsBumperRide;
		seats = new ModelEntity[seatCount];

		Model carModel; Vector3 carScale;
		if ( isBumper )
		{
			carSim = new CarSim( seatCount,
				ride.Shape.Width * tileSize * 0.5f, ride.Shape.Height * tileSize * 0.5f,
				radius: tileSize * 0.3f, speed: Speed );

			var (frames, scale) = LoadDodgemFrames( ride.Archive, tileSize );
			carFrames = frames; // null if b_car.MD2 didn't load → fall back to a red box below
			if ( frames != null ) { carModel = frames[0]; carScale = scale; }
			else { carModel = ColourCube( 220, 40, 40 ); carScale = new Vector3( tileSize * 0.22f ); }
		}
		else
		{
			carModel = ColourCube( 250, 220, 90 ); carScale = new Vector3( tileSize * 0.06f ); // rider markers
		}

		for ( int i = 0; i < seatCount; i++ )
			seats[i] = new ModelEntity { Model = carModel, Scale = carScale, Position = Offscreen };
	}

	private static Model ColourCube( byte r, byte g, byte b )
	{
		var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		mat.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
		return Primitives.Cube.GenerateModel( mat );
	}

	// The bumper's real dodgem car (b_car.MD2) + its motion frame (b_carm.MD2), sized to ~0.9 tile so the
	// cars read at dodgem scale. Null if the mesh doesn't load (caller uses a red box stand-in).
	private static (Model[]? Frames, Vector3 Scale) LoadDodgemFrames( string archive, float tileSize )
	{
		var frames = new List<Model>();
		Vector3 scale = Vector3.One;
		foreach ( var name in new[] { "b_car.MD2", "b_carm.MD2" } )
		{
			var built = RideCarMesh.Build( archive, name );
			if ( built == null )
				continue;
			frames.Add( built.Value.Model );
			if ( frames.Count == 1 )
			{
				float he = built.Value.HalfExtent;
				float fit = he > 1e-3f ? (tileSize * 0.9f) / (2f * he) : 1f;
				scale = built.Value.DecompScale * fit;
			}
		}
		return frames.Count > 0 ? (frames.ToArray(), scale) : (null, scale);
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
		if ( carSim != null )
		{
			UpdateArena();
			return;
		}

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

		// Riders trail the lead car along the loop, one per occupied seat, so the authored seat count reads
		// as a train of cars rather than markers stacked beside one box. Each seat's path position is the
		// car/seat node's live world position — published to the node field (T-048) whether or not it's
		// occupied (the node exists physically), while the visible marker hides when empty.
		int occupied = Math.Min( ride.Riders, seatCount );
		for ( int i = 0; i < seatCount; i++ )
		{
			float d = ((dist - (i + 1) * SeatSpacing) % length + length) % length;
			var (sp, st) = Sample( d );
			var seatPos = sp + Vector3.Up * 4f;

			if ( i < seatNodeIds.Count )
				ride.NodeField.PublishMoving( seatNodeIds[i], seatPos );

			if ( i < occupied )
			{
				seats[i].Position = seatPos;
				float syaw = MathF.Atan2( st.Y, st.X ) - MathF.PI / 2f;
				seats[i].Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, syaw );
			}
			else
			{
				seats[i].Position = Offscreen;
			}
		}
	}

	// Bumper/dodgem update: advance the arena collision sim and place one car per occupied seat at its
	// local arena position mapped to world + terrain. There's no lead car or circuit loop — the dodgems
	// roam and bump (docs/08, the BUMP branch). Each car's live world position feeds the node field (T-048).
	// Dev visualisation: drive + show every dodgem regardless of occupancy, so the arena sim is visible
	// without waiting for peeps to board (OPENTPW_BUMPER_DEMO=1). Off by default — normal play is occupancy-driven.
	private static readonly bool DemoAllCars = Environment.GetEnvironmentVariable( "OPENTPW_BUMPER_DEMO" ) == "1";

	private void UpdateArena()
	{
		if ( DemoAllCars || (ride.Riders > 0 && !ride.IsBroken) )
			carSim!.Step( Time.Delta );

		car.Position = Offscreen; // no lead car in bumper mode

		// Cycle the dodgem's animation frames (b_car ↔ b_carm) for its wobble.
		carAnimT += Time.Delta;
		int frame = carFrames is { Length: > 1 } ? (int)(carAnimT * CarAnimFps) % carFrames.Length : 0;

		int occupied = DemoAllCars ? seatCount : Math.Min( ride.Riders, seatCount );
		var c = ride.Position;
		for ( int i = 0; i < seatCount; i++ )
		{
			var cs = carSim!.Cars[i];
			float wx = c.X + cs.Pos.X, wy = c.Y + cs.Pos.Y;
			var pos = new Vector3( wx, wy, 0 ).WithZ( terrain.SampleHeight( wx, wy ) + 2f );

			if ( i < seatNodeIds.Count )
				ride.NodeField.PublishMoving( seatNodeIds[i], pos );

			if ( i < occupied )
			{
				seats[i].Position = pos;
				// The car mesh's long axis is +Y (the ride-car convention), so face it along travel by -90°;
				// the plain marker box has no front, so it uses the raw heading.
				float yaw = carFrames != null ? cs.Heading - MathF.PI / 2f : cs.Heading;
				seats[i].Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
				if ( carFrames != null )
					seats[i].Model = carFrames[frame];
			}
			else
			{
				seats[i].Position = Offscreen;
			}
		}
	}

	// A closed loop tracing the ride's authored footprint perimeter (its .sam shape), pulled slightly
	// inside the edge and smoothed into a curve; null when the footprint is degenerate (caller uses the
	// ellipse). Uses real authored shape data + the entrance, so the loop adapts to non-rectangular rides
	// and passes the boarding tile. See RidePath.
	private static Vector3[]? BuildFootprintLoop( Ride ride, float tileSize, ParkTerrain terrain )
	{
		var ring = RidePath.FootprintRing( ride.Shape.Cells, ride.Shape.Entrance );
		if ( ring.Count < 3 )
			return null;

		const float Inset = 0.82f; // pull the ring inside the footprint edge so the car reads as "on" the ride
		float halfW = ride.Shape.Width / 2f, halfH = ride.Shape.Height / 2f;
		var ctr = ride.Position;
		var ctrl = new List<Vector3>( ring.Count );
		foreach ( var (tx, ty) in ring )
		{
			float wx = ctr.X + (tx + 0.5f - halfW) * Inset * tileSize;
			float wy = ctr.Y + (ty + 0.5f - halfH) * Inset * tileSize;
			ctrl.Add( new Vector3( wx, wy, 0 ).WithZ( terrain.SampleHeight( wx, wy ) + 2f ) );
		}
		return RidePath.Smooth( ctrl, closed: true, sub: 6 ).ToArray();
	}

	// The original generic fallback: an elliptical ring inside the footprint, sampled onto the terrain.
	private static Vector3[] BuildEllipseLoop( Ride ride, float tileSize, ParkTerrain terrain )
	{
		const int n = 48;
		float rx = MathF.Max( tileSize, ride.TileW * tileSize * 0.32f );
		float ry = MathF.Max( tileSize, ride.TileH * tileSize * 0.32f );
		var c = ride.Position;
		var l = new Vector3[n + 1];
		for ( int i = 0; i <= n; i++ )
		{
			float a = i / (float)n * MathF.Tau;
			var p = new Vector3( c.X + MathF.Cos( a ) * rx, c.Y + MathF.Sin( a ) * ry, 0 );
			l[i] = p.WithZ( terrain.SampleHeight( p.X, p.Y ) + 2f );
		}
		return l;
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
