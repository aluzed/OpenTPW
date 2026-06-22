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

	/// <summary>
	/// A sprite-frame billboard built in <b>pixel units</b> and anchored at the frame's hotspot (T-035):
	/// the quad spans frame-local pixels with the hotspot pinned to the local origin, so the caller scales
	/// the whole sprite by one pixels-to-world factor — frames of different sizes then sit at correct
	/// relative sizes (no per-frame width pulsing) with the anchor (feet) planted. The hotspot is the
	/// frame's top-left offset from the anchor (image X right, Y down); world Z is up, so image Y negates.
	/// </summary>
	public static Model MakeSprite( Texture texture, int w, int h, int hotspotX, int hotspotY )
	{
		float x0 = hotspotX, x1 = hotspotX + w;       // local X (pixels), hotspot at x = 0
		float zTop = -hotspotY, zBot = -(hotspotY + h); // local Z (pixels): image top row highest
		var quad = new[]
		{
			new Vertex { Position = new Vector3( x0, 0, zBot ), TexCoords = new Vector2( 0, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( x1, 0, zBot ), TexCoords = new Vector2( 1, 1 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( x0, 0, zTop ), TexCoords = new Vector2( 0, 0 ), Normal = new Vector3( 0, 1, 0 ) },
			new Vertex { Position = new Vector3( x1, 0, zTop ), TexCoords = new Vector2( 1, 0 ), Normal = new Vector3( 0, 1, 0 ) },
		};
		var material = new Material<ObjectUniformBuffer>( "content/shaders/unlit.shader",
			MaterialFlags.DoubleSided | MaterialFlags.DisableDepthWrite );
		material.Set( "Color", texture );
		return new Model( quad, Indices, material );
	}

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
