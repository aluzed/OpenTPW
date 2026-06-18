namespace OpenTPW;

/// <summary>
/// Foundation of the build/manage mode (T-040): each frame it projects the cursor into the park, finds
/// the <see cref="PlacementGrid"/> tile under it, highlights that tile, and dispatches left-clicks to
/// the current tool. For now there is only a Default tool (click logs/raises a selection); placement
/// and management tools (T-041+) plug in via <see cref="HoveredTile"/> and the click hook.
/// </summary>
public sealed class BuildMode : Entity
{
	private readonly PlacementGrid grid;
	private readonly ParkTerrain terrain;
	private readonly ModelEntity highlight;
	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );

	/// <summary>The grid tile currently under the cursor (null when off-grid / no ground hit).</summary>
	public (int X, int Y)? HoveredTile { get; private set; }

	public BuildMode( PlacementGrid grid, ParkTerrain terrain )
	{
		this.grid = grid;
		this.terrain = terrain;

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
			MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
		material.Set( "Color", new Texture( [255, 235, 40, 110], 1, 1 ) ); // translucent yellow
		highlight = new ModelEntity
		{
			Model = Primitives.Plane.GenerateModel( material ),
			Scale = new Vector3( grid.TileSize / 2f ),
			Position = Offscreen,
		};
	}

	protected override void OnUpdate()
	{
		if ( !TryPickGround( out var hit ) )
		{
			HoveredTile = null;
			highlight.Position = Offscreen;
			return;
		}

		var (tx, ty) = grid.WorldToTile( hit );
		if ( !grid.InBounds( tx, ty ) )
		{
			HoveredTile = null;
			highlight.Position = Offscreen;
			return;
		}

		HoveredTile = (tx, ty);
		var c = grid.TileToWorld( tx, ty );
		highlight.Position = c.WithZ( terrain.SampleHeight( c.X, c.Y ) + 0.3f );

		if ( Input.MouseLeftPressed )
			Log.Info( $"[build] click tile ({tx},{ty})" ); // Default tool: selection hook for T-041+
	}

	// Projects the cursor to a world ray from the camera basis + vertical FOV, and intersects the park
	// ground (terrain height ≈ centroid plane, then refined by sampling at the hit). Avoids matrix
	// inversion / convention pitfalls by reconstructing the ray from the camera's own axes.
	private bool TryPickGround( out Vector3 hit )
	{
		hit = default;
		var pos = Camera.Position;
		var rot = Camera.Rotation;
		var fwd = rot.Forward;
		var right = rot.Right;
		var up = rot.Up;

		float nx = 2f * Input.Mouse.Position.X / Screen.Width - 1f;
		float ny = 1f - 2f * Input.Mouse.Position.Y / Screen.Height; // window Y is top-down
		float tanY = MathF.Tan( Camera.FieldOfView.DegreesToRadians() * 0.5f );
		float tanX = tanY * Screen.Aspect;

		var dir = (fwd + right * (nx * tanX) + up * (ny * tanY)).Normal;
		if ( MathF.Abs( dir.Z ) < 1e-4f )
			return false;

		// First hit against the approximate ground plane, then refine against the real terrain height.
		float groundZ = terrain.Centroid.Z;
		float t = (groundZ - pos.Z) / dir.Z;
		if ( t <= 0f )
			return false;
		hit = pos + dir * t;

		groundZ = terrain.SampleHeight( hit.X, hit.Y );
		t = (groundZ - pos.Z) / dir.Z;
		if ( t <= 0f )
			return false;
		hit = pos + dir * t;
		return true;
	}
}
