namespace OpenTPW;

/// <summary>
/// A small train that runs along a laid <see cref="CoasterTrack"/> (T-045 slice 3): a few car bodies
/// spaced along the track's elevated polyline, advancing by arc length and orienting to the local
/// tangent. The track is open (not a closed loop yet), so the train **shuttles** — it reflects at each
/// end and faces its actual travel direction. The car is the ride's real <c>CrocCar.MD2</c> mesh when
/// it loads (single mesh, 6 materials), falling back to a procedural croc-green box. Peep boarding +
/// scream are still later in slice 3.
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

	public CoasterTrain( CoasterTrack track, float tileSize, string archive )
	{
		this.track = track;
		spacing = tileSize * 0.9f;

		var (body, scale) = LoadCar( archive, tileSize );

		cars = new ModelEntity[CarCount];
		for ( int i = 0; i < CarCount; i++ )
			cars[i] = new ModelEntity { Model = body, Scale = scale, Position = Offscreen };
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

		for ( int i = 0; i < cars.Length; i++ )
		{
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

	// The ride's real CrocCar.MD2 (single mesh) built the same way ride meshes are (the LobbyIsland
	// pattern: per-material .wct textures, Y/Z swizzle). Falls back to a green box if it won't load.
	private static (Model Body, Vector3 Scale) LoadCar( string archive, float tileSize )
	{
		try
		{
			var mesh = new ModelFile( $"{archive}/CrocCar.MD2" ).Meshes[0];

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
			var scale = new Vector3( scl.X, scl.Z, scl.Y );

			// Centre the mesh on its own centroid (in scaled model space) so the car sits *on* the track
			// point — the MD2 authors the body at its in-ride location, which we don't want here.
			var centroid = Vector3.Zero;
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
				centroid += new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y );
			centroid *= 1f / mesh.Vertices.Length;

			var vertices = new List<Vertex>( mesh.Vertices.Length );
			float halfExtent = 0f; // largest horizontal half-extent, for scaling the car to the grid
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

			// Scale the car so its long axis spans ~1.3 of a 16-unit tile (a believable car length).
			float fit = halfExtent > 1e-3f ? (tileSize * 1.3f) / (2f * halfExtent) : 1f;
			scale *= fit;

			var model = new Model( [.. vertices], mesh.Indices, material );
			Log.Info( $"[coaster] CrocCar loaded ({mesh.Vertices.Length} verts, {mesh.Indices.Length / 3} tris, fit x{fit:0.00})" );
			return (model, scale);
		}
		catch ( Exception e )
		{
			Log.Warning( $"[coaster] CrocCar load failed ({e.Message}); using a placeholder box" );
			var mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader" );
			mat.Set( "Color", new Texture( [60, 165, 75, 255], 1, 1 ) );
			// Long axis along local +Y to match the orientation convention (see OnUpdate).
			return (Primitives.Cube.GenerateModel( mat ), new Vector3( tileSize * 0.22f, tileSize * 0.36f, 1.4f ));
		}
	}
}
