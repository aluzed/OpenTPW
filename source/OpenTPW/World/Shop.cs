namespace OpenTPW;

/// <summary>What a stall sells — and so which peep need it satisfies (T-039).</summary>
public enum ShopKind
{
	/// <summary>A food stall: satisfies hunger.</summary>
	Food,
	/// <summary>A drink stall: satisfies thirst.</summary>
	Drink,
	/// <summary>A toilet: relieves the bladder (a free facility — no income).</summary>
	Toilet,
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

	/// <summary>Display name (for the manage UI).</summary>
	public string Name => Kind switch { ShopKind.Drink => "Drink Stall", ShopKind.Toilet => "Toilet", _ => "Food Stall" };

	/// <summary>Grid footprint (set when placed) — used to select it by clicking a covered tile, and to
	/// free its cells on sell/demolish (T-041).</summary>
	public int TileX { get; set; }
	public int TileY { get; set; }
	public int TileW { get; set; }
	public int TileH { get; set; }

	/// <summary>What the player paid to build this stall — used for the sell refund (T-041).</summary>
	public float BuildCost { get; set; }

	/// <summary>True if the grid tile (tx,ty) is within this stall's footprint.</summary>
	public bool Covers( int tx, int ty ) => tx >= TileX && tx < TileX + TileW && ty >= TileY && ty < TileY + TileH;

	/// <summary>Remove this stall from the world (sold/demolished) — caller frees its grid cells + refunds.</summary>
	public void Despawn()
	{
		Stalls.Remove( this );
		Entity.All.Remove( this );
	}

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
		var model = kind switch
		{
			ShopKind.Drink => Billboard.Make( 50, 130, 220 ),   // blue
			ShopKind.Toilet => Billboard.Make( 210, 210, 220 ), // pale grey/white
			_ => Billboard.Make( 40, 170, 70 ),                 // green food
		};
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
