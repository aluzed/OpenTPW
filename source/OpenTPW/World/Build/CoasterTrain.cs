namespace OpenTPW;

/// <summary>
/// A small train that runs along a laid <see cref="CoasterTrack"/> (T-045 slice 3): a few car bodies
/// spaced along the track's elevated polyline, advancing by arc length and orienting to the local
/// tangent. The track is open (not a closed loop yet), so the train **shuttles** — it reflects at each
/// end and faces its actual travel direction. Procedural croc-green boxes for now; the real
/// <c>CrocCar.MD2</c> mesh + peep boarding + scream are later in slice 3.
/// </summary>
public sealed class CoasterTrain : Entity
{
	private const int CarCount = 3;
	private const float Speed = 16f; // world units / second along the track
	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );

	private readonly CoasterTrack track;
	private readonly float spacing;     // arc-length gap between cars
	private readonly ModelEntity[] cars;
	private float dist;                 // lead car's distance travelled along the shuttle cycle

	public CoasterTrain( CoasterTrack track, float tileSize )
	{
		this.track = track;
		spacing = tileSize * 0.9f;

		var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		mat.Set( "Color", new Texture( [60, 165, 75, 255], 1, 1 ) );

		cars = new ModelEntity[CarCount];
		for ( int i = 0; i < CarCount; i++ )
			cars[i] = new ModelEntity
			{
				Model = Primitives.Cube.GenerateModel( mat ),
				Scale = new Vector3( tileSize * 0.36f, tileSize * 0.22f, 1.4f ),
				Position = Offscreen,
			};
	}

	/// <summary>Despawn the train's car entities (called when the track is torn down).</summary>
	public void Despawn()
	{
		foreach ( var c in cars )
			Entity.All.Remove( c );
		Entity.All.Remove( this );
	}

	protected override void OnUpdate()
	{
		var path = track.WorldPath();
		if ( path.Count < 2 )
		{
			foreach ( var c in cars )
				c.Position = Offscreen;
			return;
		}

		// Cumulative arc length along the polyline.
		var cum = new float[path.Count];
		for ( int i = 1; i < path.Count; i++ )
			cum[i] = cum[i - 1] + path[i].Distance( path[i - 1] );
		float length = cum[^1];
		if ( length < 1e-3f )
			return;

		float period = 2f * length; // down the track and back (shuttle)
		dist = (dist + Speed * Time.Delta) % period;

		for ( int i = 0; i < cars.Length; i++ )
		{
			float u = (dist - i * spacing) % period;
			if ( u < 0 ) u += period;

			// Reflect the second half so the car retraces the open track, facing travel direction.
			bool returning = u > length;
			float d = returning ? period - u : u;

			var (pos, tan) = Sample( path, cum, d );
			if ( returning ) tan = -tan;

			cars[i].Position = pos;
			float yaw = MathF.Atan2( tan.Y, tan.X );
			cars[i].Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
		}
	}

	// Position + forward tangent at arc-length d along the polyline.
	private static (Vector3 Pos, Vector3 Tangent) Sample( IReadOnlyList<Vector3> path, float[] cum, float d )
	{
		int s = 0;
		while ( s < path.Count - 2 && cum[s + 1] < d )
			s++;

		float segLen = cum[s + 1] - cum[s];
		float f = segLen > 1e-4f ? (d - cum[s]) / segLen : 0f;
		var a = path[s];
		var b = path[s + 1];
		return (a + (b - a) * f, (b - a).Normal);
	}
}
