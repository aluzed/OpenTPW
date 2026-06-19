namespace OpenTPW;

/// <summary>
/// A small train that runs a laid <see cref="CoasterTrack"/> (T-045 slice 3): a few car bodies spaced
/// along the track's smoothed centre-line, advancing by arc length and orienting to the local tangent
/// (shuttling on an open track, looping on a closed circuit). The car is the ride's real
/// <c>CrocCar.MD2</c> mesh when it loads (single mesh, 6 materials), falling back to a procedural box.
/// The train carries **visible riders** — seat markers reflecting the coaster's live occupancy
/// (<see cref="CoasterTrack.Riders"/>). Scream SFX awaits the ride engine (T-032).
/// </summary>
public sealed class CoasterTrain : Entity
{
	private const int CarCount = 3;
	private const int RidersPerCar = 2;
	private const float Speed = 16f;   // world units / second along the track
	private const float AnimFps = 8f;  // CrocCar frame cadence
	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );

	private readonly CoasterTrack track;
	private readonly float tileSize;
	private readonly float spacing;     // arc-length gap between cars
	private readonly ModelEntity[] cars;
	private readonly ModelEntity[] seats; // CarCount*RidersPerCar rider markers
	private readonly Model[] frames;      // CrocCar animation frames (base + CrocCarM1..3)
	private float dist;                 // lead car's distance travelled along the shuttle cycle

	public CoasterTrain( CoasterTrack track, float tileSize, string archive )
	{
		this.track = track;
		this.tileSize = tileSize;
		spacing = tileSize * 0.9f;

		var (loaded, scale) = LoadCarFrames( archive, tileSize );
		frames = loaded;

		cars = new ModelEntity[CarCount];
		for ( int i = 0; i < CarCount; i++ )
			cars[i] = new ModelEntity { Model = frames[0], Scale = scale, Position = Offscreen };

		// Bright little markers standing in for seated riders (shown per occupied seat).
		var seatMat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		seatMat.Set( "Color", new Texture( [250, 210, 80, 255], 1, 1 ) );
		seats = new ModelEntity[CarCount * RidersPerCar];
		for ( int i = 0; i < seats.Length; i++ )
			seats[i] = new ModelEntity { Model = Primitives.Cube.GenerateModel( seatMat ), Scale = new Vector3( tileSize * 0.08f ), Position = Offscreen };
	}

	/// <summary>Despawn the train's car + rider entities (called when the track is torn down).</summary>
	public void Despawn()
	{
		foreach ( var c in cars )
			Entity.All.Remove( c );
		foreach ( var s in seats )
			Entity.All.Remove( s );
		Entity.All.Remove( this );
	}

	protected override void OnUpdate()
	{
		// The ridden centre-line is the track's Catmull-Rom spline (already a closed ring when complete),
		// so the cars glide through corners. A closed circuit loops; an open track shuttles (down + back).
		var path = track.SmoothedPath();
		if ( path.Count < 2 )
		{
			foreach ( var c in cars )
				c.Position = Offscreen;
			return;
		}
		bool closed = track.IsClosed;

		var cum = new float[path.Count];
		for ( int i = 1; i < path.Count; i++ )
			cum[i] = cum[i - 1] + path[i].Distance( path[i - 1] );
		float length = cum[^1];
		if ( length < 1e-3f )
			return;

		float period = closed ? length : 2f * length;
		dist = (dist + Speed * Time.Delta) % period;

		int occupied = Math.Min( track.Riders, seats.Length ); // how many seat markers to show
		var frame = frames[CurrentFrame()];
		for ( int i = 0; i < cars.Length; i++ )
		{
			cars[i].Model = frame; // advance the CrocCar's chomp animation (all cars in sync)
			float u = (dist - i * spacing) % period;
			if ( u < 0 ) u += period;

			// Open track: reflect the second half so the car retraces it, facing travel direction.
			bool returning = !closed && u > length;
			float d = returning ? period - u : u;

			var (pos, tan) = Sample( path, cum, d );
			if ( returning ) tan = -tan;

			cars[i].Position = pos;
			// The car body is long along its local +Y, so align +Y (not +X) with the travel tangent.
			float yaw = MathF.Atan2( tan.Y, tan.X ) - MathF.PI / 2f;
			var rot = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
			cars[i].Rotation = rot;

			// Seat the riders this car carries, side by side across its width (perp to travel).
			var side = new Vector3( -tan.Y, tan.X, 0f ).Normal;
			for ( int r = 0; r < RidersPerCar; r++ )
			{
				int seat = i * RidersPerCar + r;
				if ( seat < occupied )
				{
					float off = (r == 0 ? 1f : -1f) * tileSize * 0.12f;
					seats[seat].Position = pos + Vector3.Up * (tileSize * 0.10f) + side * off;
					seats[seat].Rotation = rot;
				}
				else
				{
					seats[seat].Position = Offscreen;
				}
			}
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

	// Ping-pong over the animation frames (0 → n-1 → 1) at AnimFps, so the croc chomps as it runs.
	private int CurrentFrame()
	{
		int n = frames.Length;
		if ( n <= 1 )
			return 0;
		int cycle = 2 * (n - 1);
		int step = (int)(Time.Now * AnimFps) % cycle;
		return step < n ? step : cycle - step;
	}

	// The CrocCar mesh + its animation frames (CrocCarM1..3), all at the base frame's scale so they only
	// animate, not pulse. Falls back to a single procedural box frame if nothing loads.
	private static (Model[] Frames, Vector3 Scale) LoadCarFrames( string archive, float tileSize )
	{
		var frames = new List<Model>();
		Vector3 scale = Vector3.One;
		foreach ( var name in new[] { "CrocCar.MD2", "CrocCarM1.MD2", "CrocCarM2.MD2", "CrocCarM3.MD2" } )
		{
			var built = BuildCarMesh( archive, name );
			if ( built == null )
				continue;
			frames.Add( built.Value.Model );
			if ( frames.Count == 1 ) // size the whole train off the base frame
			{
				float halfExtent = built.Value.HalfExtent;
				float fit = halfExtent > 1e-3f ? (tileSize * 1.3f) / (2f * halfExtent) : 1f;
				scale = built.Value.DecompScale * fit;
			}
		}

		if ( frames.Count > 0 )
		{
			Log.Info( $"[coaster] CrocCar loaded ({frames.Count} frame(s))" );
			return (frames.ToArray(), scale);
		}

		Log.Warning( "[coaster] CrocCar load failed; using a placeholder box" );
		var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
		mat.Set( "Color", new Texture( [60, 165, 75, 255], 1, 1 ) );
		// Long axis along local +Y to match the orientation convention (see OnUpdate).
		return ([Primitives.Cube.GenerateModel( mat )], new Vector3( tileSize * 0.22f, tileSize * 0.36f, 1.4f ));
	}

	// One CrocCar frame mesh, built the same way ride meshes are (per-material .wct textures, Y/Z swizzle),
	// centred on its own centroid so it sits on the track point. Returns null if the mesh won't load.
	private static (Model Model, Vector3 DecompScale, float HalfExtent)? BuildCarMesh( string archive, string name )
	{
		try
		{
			var mesh = new ModelFile( $"{archive}/{name}" ).Meshes[0];

			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();
			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i ) { textures.Add( Texture.Missing ); continue; }
				try { textures.Add( new Texture( $"{archive}/textures/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) ); }
				catch { textures.Add( Texture.Missing ); }
			}
			material.Set( "Color", [.. textures] );

			System.Numerics.Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out _, out _ );

			var centroid = Vector3.Zero;
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
				centroid += new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y );
			centroid *= 1f / mesh.Vertices.Length;

			var vertices = new List<Vertex>( mesh.Vertices.Length );
			float halfExtent = 0f;
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				var p = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ) - centroid;
				halfExtent = MathF.Max( halfExtent, MathF.Max( MathF.Abs( p.X ), MathF.Abs( p.Y ) ) );
				vertices.Add( new Vertex
				{
					Position = p,
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags,
				} );
			}

			var model = new Model( [.. vertices], mesh.Indices, material );
			return (model, new Vector3( scl.X, scl.Z, scl.Y ), halfExtent);
		}
		catch
		{
			return null;
		}
	}
}
