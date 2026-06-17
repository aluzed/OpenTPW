namespace OpenTPW;

/// <summary>
/// Builds the shared upright camera-facing quad used by peeps, staff and shop signs: a unit quad on the
/// local XZ plane (z = 0..1, standing on the ground, facing +Y), double-sided and unlit, in a flat
/// colour. The per-entity yaw and scale do the rest.
/// </summary>
internal static class Billboard
{
	private static readonly Vertex[] Quad =
	{
		new Vertex { Position = new Vector3( -0.5f, 0, 0 ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
		new Vertex { Position = new Vector3( 0.5f, 0, 0 ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
		new Vertex { Position = new Vector3( -0.5f, 0, 1 ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		new Vertex { Position = new Vector3( 0.5f, 0, 1 ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
	};
	private static readonly uint[] Indices = { 0, 2, 1, 1, 2, 3 };

	public static Model Make( byte r, byte g, byte b ) => Build( new Texture( [r, g, b, 255], 1, 1 ) );

	/// <summary>A billboard textured with a sprite frame (transparent where the texture's alpha is 0).</summary>
	public static Model Make( Texture texture ) => Build( texture );

	private static Model Build( Texture texture )
	{
		// Double-sided so it shows from any yaw; depth-write off so transparent edges don't punch holes
		// in whatever is drawn behind them (depth test stays on, so terrain/rides still occlude peeps).
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
			MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
		material.Set( "Color", texture );
		return new Model( Quad, Indices, material );
	}
}
