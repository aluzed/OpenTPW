namespace OpenTPW;

/// <summary>
/// A park visitor. Renders as an upright camera-facing billboard (a placeholder until the authentic
/// peep sprites — <c>esprites.wad</c>'s <c>.TPC</c>/<c>.FPC</c>, a custom encoded image format — are
/// decoded) and wanders the park terrain: it walks toward a random point within its home radius, then
/// picks a new one, staying dropped onto the terrain surface. The billboard yaws (about world up) to
/// face the camera each frame.
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
	private readonly Vector3 home;
	private readonly float wanderRadius;
	private readonly float speed;
	private Vector3 target;

	public Peep( ParkTerrain terrain, Vector3 home, float wanderRadius, int colorIndex )
	{
		this.terrain = terrain;
		this.home = home;
		this.wanderRadius = wanderRadius;

		Model = SharedModel( colorIndex );
		Scale = new Vector3( 3f, 1f, 5f + (float)Random.Shared.NextDouble() * 2f );
		speed = 6f + (float)Random.Shared.NextDouble() * 8f;
		Position = home;
		target = PickTarget();
		DropToGround();
	}

	protected override void OnUpdate()
	{
		// Walk toward the target on the ground plane; pick a new one once close.
		var to = new Vector3( target.X - Position.X, target.Y - Position.Y, 0 );
		if ( to.Length < 2f )
			target = PickTarget();
		else
			Position += to.Normal * speed * Time.Delta;

		DropToGround();

		// Cylindrical billboard: yaw about world up so the quad's +Y face points at the camera.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	private void DropToGround() => Position = Position.WithZ( terrain.SampleHeight( Position.X, Position.Y ) );

	private Vector3 PickTarget()
	{
		double a = Random.Shared.NextDouble() * Math.PI * 2.0;
		double r = Random.Shared.NextDouble() * wanderRadius;
		return new Vector3( home.X + (float)(Math.Cos( a ) * r), home.Y + (float)(Math.Sin( a ) * r), 0 );
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
