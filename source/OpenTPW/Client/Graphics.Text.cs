namespace OpenTPW;

internal static partial class Graphics
{
	/// <summary>
	/// Draws a string with a bitmap <see cref="Font"/>: one textured quad per glyph, using the
	/// atlas UVs and the BF4 advance/bearing metrics (via <see cref="FontAtlas.Layout"/>).
	/// UI-space coordinates; <paramref name="x"/>/<paramref name="y"/> is the pen origin.
	/// </summary>
	public static void DrawText( Font font, string text, float x, float y )
	{
		var material = Material.UI;
		material.Set( "Color", font.Texture );

		foreach ( var g in font.Atlas.Layout( text, x, y ) )
		{
			Quad(
				new Rectangle( g.X, g.Y, g.Width, g.Height ),
				new Rectangle( g.U0, g.V0, g.U1 - g.U0, g.V1 - g.V0 ),
				material );
		}
	}
}
