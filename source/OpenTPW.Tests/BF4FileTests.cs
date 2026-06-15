using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class BF4FileTests
{
	// Builds a minimal valid BF4: header + offset table + two glyph blocks.
	private static byte[] BuildFont( params int[] charCodes )
	{
		const int headerSize = 8;
		const int blockSize = 12; // 4-byte char code + 8 bytes of (placeholder) glyph data
		var tableEnd = headerSize + charCodes.Length * 4;

		var total = tableEnd + charCodes.Length * blockSize;
		var data = new byte[total];

		Encoding.ASCII.GetBytes( "F4FB" ).CopyTo( data, 0 );
		data[4] = 2; // version
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 6, 2 ), (ushort)charCodes.Length );

		for ( var i = 0; i < charCodes.Length; i++ )
		{
			var blockOffset = tableEnd + i * blockSize;
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( headerSize + i * 4, 4 ), (uint)blockOffset );
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( blockOffset, 4 ), (uint)charCodes[i] );
		}

		return data;
	}

	[TestMethod]
	public void ParsesHeaderAndGlyphCharCodes()
	{
		var bytes = BuildFont( '*', '1', 'A' );

		using var stream = new MemoryStream( bytes );
		var font = new BF4File( stream );

		Assert.AreEqual( 2, font.Version );
		Assert.AreEqual( 3, font.Glyphs.Count );
		CollectionAssert.AreEqual(
			new[] { (int)'*', '1', 'A' },
			font.Glyphs.Select( g => g.CharCode ).ToArray() );
		// Data is the full raw block (char code + 8 placeholder bytes).
		Assert.AreEqual( 12, font.Glyphs[0].Data.Length );
	}

	[TestMethod]
	public void RejectsBadMagic()
	{
		var bytes = new byte[16];
		Encoding.ASCII.GetBytes( "XXXX" ).CopyTo( bytes, 0 );

		using var stream = new MemoryStream( bytes );
		Assert.ThrowsExactly<InvalidDataException>( () => new BF4File( stream ) );
	}

	// Optional validation against a real font. Set TPW_FONT_SAMPLE to a .BF4 path to run.
	[TestMethod]
	public void ParsesRealFontSample()
	{
		Log = new();

		var path = Environment.GetEnvironmentVariable( "TPW_FONT_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_FONT_SAMPLE to a Theme Park World .BF4 file to run this test." );

		using var stream = File.OpenRead( path );
		var font = new BF4File( stream );

		Assert.IsTrue( font.Glyphs.Count > 0, "a font should contain glyphs" );
		var codes = font.Glyphs.Select( g => g.CharCode ).ToHashSet();
		// Real TPW fonts include the digits and common punctuation.
		Assert.IsTrue( codes.Contains( '1' ) && codes.Contains( '0' ),
			"font should contain digit glyphs" );
		Assert.IsTrue( font.Glyphs.All( g => g.Data.Length >= 4 ) );
	}
}
