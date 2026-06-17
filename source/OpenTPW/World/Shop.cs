namespace OpenTPW;

/// <summary>
/// A food/drink stall. Hungry visitors detour to the nearest shop, pay for a snack (park income) and
/// have their hunger satisfied. Rendered as a tall green billboard; tracked in <see cref="All"/> so
/// peeps can find the closest one cheaply.
/// </summary>
public sealed class Shop : ModelEntity
{
	/// <summary>Every shop in the park.</summary>
	public static readonly List<Shop> Stalls = new();

	/// <summary>What a snack costs (park income when bought).</summary>
	public float Price { get; }

	private static Model? sharedModel;

	public Shop( ParkTerrain terrain, Vector3 position, float price = 8f )
	{
		Price = price;
		Model = sharedModel ??= Billboard.Make( 40, 170, 70 ); // green stall
		Scale = new Vector3( 9f, 1f, 12f ); // bigger than a peep so a stall reads as a building
		Position = position.WithZ( terrain.SampleHeight( position.X, position.Y ) );
		Stalls.Add( this );
	}

	protected override void OnUpdate()
	{
		// Stalls face the camera like the other billboards.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}
}
