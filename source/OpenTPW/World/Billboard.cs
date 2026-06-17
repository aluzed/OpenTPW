namespace OpenTPW;

/// <summary>
/// Builds the shared upright camera-facing quad used by peeps, staff and shop signs: a unit quad on the
/// local XZ plane (z = 0..1, standing on the ground, facing +Y), double-sided and unlit, in a flat
/// colour. The per-entity yaw and scale do the rest.
/// </summary>
internal static class Billboard
{
	public static Model Make( byte r, byte g, byte b )
	{
		var vertices = new[]
		{
			new Vertex { Position = new Vector3( -0.5f, 0, 0 ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 0 ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( -0.5f, 0, 1 ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( 0.5f, 0, 1 ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		};
		uint[] indices = { 0, 2, 1, 1, 2, 3 };

		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader", MaterialFlags.DoubleSided );
		material.Set( "Color", new Texture( [r, g, b, 255], 1, 1 ) );
		return new Model( vertices, indices, material );
	}
}
