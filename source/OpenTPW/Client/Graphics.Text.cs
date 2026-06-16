using System.Numerics;
using Veldrid;

namespace OpenTPW;

internal static partial class Graphics
{
	/// <summary>
	/// Draws a string with a bitmap <see cref="Font"/>. All glyph quads are batched into the
	/// shared vertex/index buffer and issued as a single draw — drawing them one Quad at a time
	/// would reuse the same dynamic buffer N times in a frame and corrupt the glyphs.
	/// UI-space coordinates; <paramref name="x"/>/<paramref name="y"/> is the pen origin.
	/// </summary>
	public static void DrawText( Font font, string text, float x, float y, TextAlign align = TextAlign.Left, float scale = 1f )
	{
		var placed = font.Atlas.Layout( text, x, y, align, scale );
		if ( placed.Count == 0 )
			return;

		var screenMatrix = CreateScreenMatrix( BaseScreenSize );
		var vertices = new List<Vertex>( placed.Count * 4 );
		var indices = new List<uint>( placed.Count * 6 );

		foreach ( var g in placed )
		{
			var rect = new Rectangle( g.X, g.Y, g.Width, g.Height );
			// The UI shader samples (x, 1 - y), so the V we hand it must be 1 - atlasV for the
			// glyph to land on its own atlas rows (otherwise it samples a different row = garble).
			var uv = new Rectangle( g.U0, 1f - g.V1, g.U1 - g.U0, g.V1 - g.V0 );
			var b = (uint)vertices.Count;

			vertices.Add( new( new Vector3( rect.TopLeft ) * screenMatrix, uv.TopLeft ) );
			vertices.Add( new( new Vector3( rect.TopRight ) * screenMatrix, uv.TopRight ) );
			vertices.Add( new( new Vector3( rect.BottomLeft ) * screenMatrix, uv.BottomLeft ) );
			vertices.Add( new( new Vector3( rect.BottomRight ) * screenMatrix, uv.BottomRight ) );

			indices.AddRange( new[] { b + 3, b + 2, b + 1, b + 0, b + 1, b + 2 } );
		}

		var material = Material.UI;
		material.Set( "Color", font.Texture );

		var cmd = Render.CommandList;
		cmd.UpdateBuffer( vertexBuffer, 0, vertices.ToArray() );
		cmd.UpdateBuffer( indexBuffer, 0, indices.ToArray() );

		cmd.SetIndexBuffer( indexBuffer, IndexFormat.UInt32 );
		cmd.SetVertexBuffer( 0, vertexBuffer );
		cmd.SetPipeline( material.Pipeline );

		material.CreateEphemeralResourceSet( out var resourceSets );
		for ( uint i = 0; i < resourceSets.Length; ++i )
			cmd.SetGraphicsResourceSet( i, resourceSets[i] );

		cmd.DrawIndexed( (uint)indices.Count );
	}
}
