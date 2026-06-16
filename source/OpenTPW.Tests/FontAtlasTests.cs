using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class FontAtlasTests
{
	// A glyph block with full metrics: char@0, width@16, height@18, xbearing@20, ybearing@21,
	// advance@22 (u16), 1bpp bitmap @24.
	private static byte[] GlyphBlock( int charCode, int width, int height, byte[] bitmap,
		int xBearing = 0, int yBearing = 0, int advance = 0 )
	{
		var block = new byte[24 + bitmap.Length];
		BinaryPrimitives.WriteUInt32LittleEndian( block.AsSpan( 0, 4 ), (uint)charCode );
		BinaryPrimitives.WriteUInt16LittleEndian( block.AsSpan( 16, 2 ), (ushort)width );
		BinaryPrimitives.WriteUInt16LittleEndian( block.AsSpan( 18, 2 ), (ushort)height );
		block[20] = (byte)xBearing;
		block[21] = (byte)yBearing;
		BinaryPrimitives.WriteUInt16LittleEndian( block.AsSpan( 22, 2 ), (ushort)advance );
		bitmap.CopyTo( block, 24 );
		return block;
	}

	private static byte[] BuildFont( params byte[][] blocks )
	{
		const int headerSize = 8;
		var tableEnd = headerSize + blocks.Length * 4;
		var data = new byte[tableEnd + blocks.Sum( b => b.Length )];
		Encoding.ASCII.GetBytes( "F4FB" ).CopyTo( data, 0 );
		data[4] = 2;
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 6, 2 ), (ushort)blocks.Length );
		var offset = tableEnd;
		for ( var i = 0; i < blocks.Length; i++ )
		{
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( headerSize + i * 4, 4 ), (uint)offset );
			blocks[i].CopyTo( data, offset );
			offset += blocks[i].Length;
		}
		return data;
	}

	private static FontAtlas BuildAtlas()
	{
		// 'L' 4x5 (0x88,0x88,0xF0 = left bar + full bottom row), 'I' 1x5 (all set).
		var bytes = BuildFont(
			GlyphBlock( 'L', 4, 5, new byte[] { 0x88, 0x88, 0xF0 }, xBearing: 1, yBearing: 2, advance: 6 ),
			GlyphBlock( 'I', 1, 5, new byte[] { 0xF8 }, xBearing: 0, yBearing: 2, advance: 4 ) );
		using var stream = new MemoryStream( bytes );
		return new FontAtlas( new BF4File( stream ) );
	}

	private static byte Alpha( FontAtlas atlas, int x, int y ) => atlas.Pixels[(y * atlas.Width + x) * 4 + 3];

	[TestMethod]
	public void PacksGlyphsIntoTheAtlas()
	{
		var atlas = BuildAtlas();

		Assert.AreEqual( 256, atlas.Width );
		Assert.AreEqual( 5, atlas.Height, "atlas height = tallest shelf" );

		// 'L' packs at (0,0); 'I' follows at x = 4 + 1 padding = 5.
		var l = atlas.Glyphs['L'];
		var i = atlas.Glyphs['I'];
		Assert.AreEqual( 0, l.X );
		Assert.AreEqual( 5, i.X );
		Assert.AreEqual( 4f / 256f, l.U1, 1e-6 );

		// The 'L' mask: top-left pixel set, the pixel to its right clear, bottom row all set.
		Assert.AreEqual( 255, Alpha( atlas, 0, 0 ), "L top-left" );
		Assert.AreEqual( 0, Alpha( atlas, 1, 0 ), "L top, second column clear" );
		Assert.AreEqual( 255, Alpha( atlas, 3, 4 ), "L bottom-right" );
	}

	[TestMethod]
	public void MeasuresAndLaysOutByAdvance()
	{
		var atlas = BuildAtlas();

		Assert.AreEqual( 10, atlas.Measure( "LI" ) ); // 6 + 4
		Assert.AreEqual( 0, atlas.Measure( "??" ), "unknown chars contribute nothing" );

		var placed = atlas.Layout( "LI" );
		Assert.AreEqual( 2, placed.Count );

		// 'L' at penX 0 + xbearing 1; 'I' at penX 6 (after L's advance) + xbearing 0.
		Assert.AreEqual( 1f, placed[0].X, 1e-6 );
		Assert.AreEqual( 2f, placed[0].Y, 1e-6 );
		Assert.AreEqual( 6f, placed[1].X, 1e-6 );

		// Layout UVs match the packed glyph.
		Assert.AreEqual( atlas.Glyphs['L'].U0, placed[0].U0, 1e-6 );
	}

	[TestMethod]
	public void LaysOutMultipleLines()
	{
		var atlas = BuildAtlas();
		Assert.AreEqual( 5, atlas.LineHeight, "line height = tallest glyph" );

		// "LI" on line 0, "L" on line 1 (drops by LineHeight).
		var placed = atlas.Layout( "LI\nL" );
		Assert.AreEqual( 3, placed.Count );
		Assert.AreEqual( 2f, placed[0].Y, 1e-6 );      // line 0: originY 0 + yBearing 2
		Assert.AreEqual( 5f + 2f, placed[2].Y, 1e-6 ); // line 1: + LineHeight 5
		Assert.AreEqual( 1f, placed[2].X, 1e-6 );      // line 1 pen reset to origin
	}

	[TestMethod]
	public void AlignsLinesHorizontally()
	{
		var atlas = BuildAtlas(); // "LI" measures 10 (6 + 4)

		// Centered on x=100 -> line starts at 100 - 10/2 = 95; 'L' adds xbearing 1.
		Assert.AreEqual( 96f, atlas.Layout( "LI", 100, 0, TextAlign.Center )[0].X, 1e-6 );

		// Right-anchored at x=100 -> line starts at 100 - 10 = 90; 'L' -> 91.
		Assert.AreEqual( 91f, atlas.Layout( "LI", 100, 0, TextAlign.Right )[0].X, 1e-6 );
	}

	// Optional: build an atlas from a real font and sanity-check it. Set TPW_FONT_SAMPLE.
	[TestMethod]
	public void BuildsAtlasFromRealFont()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_FONT_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_FONT_SAMPLE to a Theme Park World .BF4 file to run this test." );

		using var stream = File.OpenRead( path );
		var atlas = new FontAtlas( new BF4File( stream ) );

		Assert.IsTrue( atlas.Width > 0 && atlas.Height > 0 );
		Assert.AreEqual( atlas.Width * atlas.Height * 4, atlas.Pixels.Length );
		Assert.IsTrue( atlas.Glyphs.ContainsKey( '0' ) && atlas.Glyphs.ContainsKey( 'A' ) );
		Assert.IsTrue( atlas.Measure( "GAME OVER" ) > 0 );
		Assert.IsTrue( atlas.Pixels.Any( b => b != 0 ), "atlas should contain rasterized glyphs" );
	}
}
