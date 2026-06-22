namespace OpenTPW;

/// <summary>What a stall sells — and so which peep need it satisfies (T-039).</summary>
public enum ShopKind
{
	/// <summary>A food stall: satisfies hunger.</summary>
	Food,
	/// <summary>A drink stall: satisfies thirst.</summary>
	Drink,
}

/// <summary>
/// A food/drink stall. A hungry or thirsty visitor detours to the nearest stall of the matching
/// <see cref="ShopKind"/>, pays (park income) and has that need satisfied. Rendered as a tall billboard
/// (green = food, blue = drink); tracked in <see cref="Stalls"/> so peeps can find the closest cheaply.
/// </summary>
public sealed class Shop : ModelEntity
{
	/// <summary>Every shop in the park.</summary>
	public static readonly List<Shop> Stalls = new();

	/// <summary>What a snack/drink costs (park income when bought).</summary>
	public float Price { get; }

	/// <summary>What this stall sells (which need it satisfies).</summary>
	public ShopKind Kind { get; }

	private static readonly Dictionary<ShopKind, Model> sharedModels = new();

	public Shop( ParkTerrain terrain, Vector3 position, ShopKind kind = ShopKind.Food, float price = 8f )
	{
		Price = price;
		Kind = kind;
		Model = ModelFor( kind );
		Scale = new Vector3( 9f, 1f, 12f ); // bigger than a peep so a stall reads as a building
		Position = position.WithZ( terrain.SampleHeight( position.X, position.Y ) );
		Stalls.Add( this );
	}

	// The closest stall of a given kind to a point, or null if the park has none of that kind.
	public static Shop? Nearest( ShopKind kind, Vector3 from )
	{
		Shop? best = null;
		float bestD2 = float.MaxValue;
		foreach ( var s in Stalls )
		{
			if ( s.Kind != kind )
				continue;
			float dx = s.Position.X - from.X, dy = s.Position.Y - from.Y;
			float d2 = dx * dx + dy * dy;
			if ( d2 < bestD2 )
			{
				bestD2 = d2;
				best = s;
			}
		}
		return best;
	}

	private static Model ModelFor( ShopKind kind )
	{
		if ( sharedModels.TryGetValue( kind, out var m ) )
			return m;
		var model = kind == ShopKind.Drink ? Billboard.Make( 50, 130, 220 ) : Billboard.Make( 40, 170, 70 );
		sharedModels[kind] = model;
		return model;
	}

	protected override void OnUpdate()
	{
		// Stalls face the camera like the other billboards.
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - Position.X), cam.Y - Position.Y );
		Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}
}
