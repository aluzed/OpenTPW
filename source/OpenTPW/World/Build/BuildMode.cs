using Veldrid;

namespace OpenTPW;

/// <summary>One placeable thing in the build catalog (a ride by WAD path, or a shop when RidePath is null).</summary>
public readonly record struct BuildCatalogItem( string Name, string? RidePath, int Width, int Height, float Cost );

/// <summary>
/// Build/manage mode (T-040 foundation + T-041 placement). Each frame it projects the cursor to the
/// <see cref="PlacementGrid"/> tile under it and highlights it. With a catalog item selected (number
/// keys), it shows the item's footprint preview (green = placeable & affordable, red = not) and, on
/// left-click, commits the placement via the supplied callback (which spawns + charges). Esc / 0 leaves
/// the placement tool (Default: click logs the tile — selection hook for later tools).
/// </summary>
public sealed class BuildMode : Entity
{
	public static BuildMode? Current { get; private set; }

	private static readonly Vector3 Offscreen = new( 0, 0, -100000f );
	private static readonly Key[] NumberKeys =
	{
		Key.Number1, Key.Number2, Key.Number3, Key.Number4, Key.Number5,
		Key.Number6, Key.Number7, Key.Number8, Key.Number9,
	};

	private readonly PlacementGrid grid;
	private readonly ParkTerrain terrain;
	private readonly Func<BuildCatalogItem, int, int, bool> commit;
	private readonly ModelEntity highlight;
	private readonly Material<ObjectUniformBuffer> mat;
	private readonly Texture yellow = new( [255, 235, 40, 110], 1, 1 );
	private readonly Texture green = new( [60, 220, 70, 120], 1, 1 );
	private readonly Texture red = new( [230, 50, 40, 120], 1, 1 );
	private readonly HashSet<Key> prevDown = new();

	/// <summary>The build catalog (rides + shops).</summary>
	public IReadOnlyList<BuildCatalogItem> Catalog { get; }

	/// <summary>Index of the selected catalog item, or -1 when no placement tool is active.</summary>
	public int Selected { get; private set; } = -1;

	public (int X, int Y)? HoveredTile { get; private set; }

	public BuildMode( PlacementGrid grid, ParkTerrain terrain, IReadOnlyList<BuildCatalogItem> catalog,
		Func<BuildCatalogItem, int, int, bool> commit )
	{
		this.grid = grid;
		this.terrain = terrain;
		this.commit = commit;
		Catalog = catalog;
		Current = this;

		mat = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
			MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
		mat.Set( "Color", yellow );
		highlight = new ModelEntity { Model = Primitives.Plane.GenerateModel( mat ), Position = Offscreen };
	}

	protected override void OnUpdate()
	{
		HandleSelectionKeys();

		if ( !TryPickGround( out var hit ) )
		{
			HoveredTile = null;
			highlight.Position = Offscreen;
			return;
		}

		var (tx, ty) = grid.WorldToTile( hit );
		HoveredTile = grid.InBounds( tx, ty ) ? (tx, ty) : null;

		if ( Selected >= 0 )
			UpdatePlacement( tx, ty );
		else
			UpdateDefault( tx, ty );
	}

	// Number keys pick a catalog item; Esc / 0 leaves the placement tool.
	private void HandleSelectionKeys()
	{
		var down = Input.Keyboard.KeysDown;
		for ( int i = 0; i < NumberKeys.Length && i < Catalog.Count; i++ )
		{
			var k = NumberKeys[i];
			if ( down.Contains( k ) && !prevDown.Contains( k ) )
				Selected = i;
		}
		if ( (down.Contains( Key.Escape ) && !prevDown.Contains( Key.Escape )) ||
			 (down.Contains( Key.Number0 ) && !prevDown.Contains( Key.Number0 )) )
			Selected = -1;

		prevDown.Clear();
		foreach ( var k in down )
			prevDown.Add( k );
	}

	// Default tool: highlight the single hovered tile; click logs it (selection hook).
	private void UpdateDefault( int tx, int ty )
	{
		if ( HoveredTile == null )
		{
			highlight.Position = Offscreen;
			return;
		}
		mat.Set( "Color", yellow );
		highlight.Scale = new Vector3( grid.TileSize / 2f );
		var c = grid.TileToWorld( tx, ty );
		highlight.Position = c.WithZ( terrain.SampleHeight( c.X, c.Y ) + 0.3f );
		if ( Input.MouseLeftPressed )
			Log.Info( $"[build] click tile ({tx},{ty})" );
	}

	// Placement tool: show the selected item's footprint, green/red by validity, place on click.
	private void UpdatePlacement( int tx, int ty )
	{
		var item = Catalog[Selected];
		if ( HoveredTile == null )
		{
			highlight.Position = Offscreen;
			return;
		}

		bool canPlace = grid.CanPlace( tx, ty, item.Width, item.Height );
		bool affordable = ParkFinances.Current?.CanAfford( item.Cost ) ?? true;
		bool ok = canPlace && affordable;

		mat.Set( "Color", ok ? green : red );
		highlight.Scale = new Vector3( item.Width * grid.TileSize / 2f, item.Height * grid.TileSize / 2f, 1f );
		var c = grid.TileToWorld( tx, ty, item.Width, item.Height );
		highlight.Position = c.WithZ( terrain.SampleHeight( c.X, c.Y ) + 0.3f );

		if ( Input.MouseLeftPressed && ok && commit( item, tx, ty ) )
			Log.Info( $"[build] placed {item.Name} at ({tx},{ty}) for ${item.Cost:0}" );
	}

	// Cursor → world ray (from the camera basis + vertical FOV) intersected with the park ground.
	private bool TryPickGround( out Vector3 hit )
	{
		hit = default;
		var pos = Camera.Position;
		var rot = Camera.Rotation;
		var dir = (rot.Forward
			+ rot.Right * ((2f * Input.Mouse.Position.X / Screen.Width - 1f) * MathF.Tan( Camera.FieldOfView.DegreesToRadians() * 0.5f ) * Screen.Aspect)
			+ rot.Up * ((1f - 2f * Input.Mouse.Position.Y / Screen.Height) * MathF.Tan( Camera.FieldOfView.DegreesToRadians() * 0.5f ))).Normal;
		if ( MathF.Abs( dir.Z ) < 1e-4f )
			return false;

		float groundZ = terrain.Centroid.Z;
		float t = (groundZ - pos.Z) / dir.Z;
		if ( t <= 0f ) return false;
		hit = pos + dir * t;
		groundZ = terrain.SampleHeight( hit.X, hit.Y );
		t = (groundZ - pos.Z) / dir.Z;
		if ( t <= 0f ) return false;
		hit = pos + dir * t;
		return true;
	}
}
