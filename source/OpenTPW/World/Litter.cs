namespace OpenTPW;

/// <summary>
/// A piece of dropped litter lying on the ground (a small flat quad). Visitors drop it as they wander;
/// nearby litter sours their mood, and handymen (see <see cref="Staff"/>) roam to pick it up. Tracked in
/// <see cref="All"/> so both peeps and handymen can find it cheaply without scanning every entity.
/// </summary>
public sealed class Litter : ModelEntity
{
	/// <summary>Every piece of litter currently on the ground.</summary>
	public static readonly List<Litter> Active = new();

	private static Model? sharedModel;

	public Litter( Vector3 groundPos )
	{
		Model = SharedModel();
		Position = groundPos.WithZ( groundPos.Z + 0.2f ); // just above the terrain to avoid z-fighting
		Scale = new Vector3( 1.5f );
		Active.Add( this );
	}

	/// <summary>Remove this litter from the world (a handyman cleaned it up).</summary>
	public void PickUp()
	{
		Active.Remove( this );
		Entity.All.Remove( this );
	}

	// A small brown ground quad, shared so dropping litter allocates nothing extra.
	private static Model SharedModel()
	{
		if ( sharedModel != null )
			return sharedModel;

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
		material.Set( "Color", new Texture( [120, 90, 60, 255], 1, 1 ) ); // muddy brown
		sharedModel = Primitives.Plane.GenerateModel( material );
		return sharedModel;
	}
}
