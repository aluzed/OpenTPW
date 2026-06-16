namespace OpenTPW;

/// <summary>
/// A renderable bitmap font: a <see cref="FontAtlas"/> built from a <c>.BF4</c> and uploaded as
/// a GPU texture. Draw strings with <see cref="Graphics.DrawText"/>.
/// </summary>
public sealed class Font
{
	/// <summary>The CPU-side atlas (glyph table + layout/measure).</summary>
	public FontAtlas Atlas { get; }

	/// <summary>The atlas uploaded as a texture (point-filtered for crisp bitmap glyphs).</summary>
	public Texture Texture { get; }

	public Font( BF4File file )
	{
		Atlas = new FontAtlas( file );
		Texture = new Texture( Atlas.Pixels, Atlas.Width, Atlas.Height, TextureFlags.PointFilter );
	}

	public Font( string path ) : this( new BF4File( path ) ) { }
}
