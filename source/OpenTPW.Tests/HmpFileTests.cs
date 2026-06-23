using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class HmpFileTests
{
	// A minimal 2×1 .HMP: header (0x30) + two 25-byte tile records + a 2-byte code grid + a 2-byte footprint.
	private static byte[] BuildHmp()
	{
		const int dataOff = 0x30, codeOff = dataOff + 2 * 25, footOff = codeOff + 2;
		var d = new byte[footOff + 2];
		void U16( int o, ushort v ) => BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( o, 2 ), v );
		void U32( int o, uint v ) => BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), v );
		void F32( int o, float v ) => BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o, 4 ), v );

		U16( 0x00, 3 );             // version
		U32( 0x02, HmpFile.Magic ); // magic
		U16( 0x06, 100 );           // scale
		U16( 0x08, 2 );             // cols
		U16( 0x0a, 1 );             // rows
		U32( 0x0c, dataOff );
		U32( 0x10, codeOff );
		U32( 0x14, footOff );
		F32( 0x18, 0f ); F32( 0x1c, 0f ); F32( 0x20, 1.5f );

		// tile 0/1 sub-grids: fill tile0 with 0x71, tile1 with 0x75 (distinct so we can tell them apart).
		for ( int i = 0; i < 25; i++ ) { d[dataOff + i] = 0x71; d[dataOff + 25 + i] = 0x75; }
		d[codeOff] = 0x75; d[codeOff + 1] = 0x76;   // code grid
		d[footOff] = 0x01; d[footOff + 1] = 0x00;   // footprint: tile0 solid, tile1 passable
		return d;
	}

	[TestMethod]
	public void ParsesSyntheticHmp()
	{
		var hmp = new HmpFile( new MemoryStream( BuildHmp() ) );

		Assert.AreEqual( 3, hmp.Version );
		Assert.AreEqual( 100, hmp.Scale );
		Assert.AreEqual( 2, hmp.Cols );
		Assert.AreEqual( 1, hmp.Rows );
		Assert.AreEqual( 1.5f, hmp.Anchor[2], 1e-4f );

		Assert.AreEqual( 2, hmp.Tiles.Count );
		Assert.AreEqual( 25, hmp.Tiles[0].Length );
		Assert.IsTrue( hmp.Tiles[0].All( b => b == 0x71 ) );
		Assert.IsTrue( hmp.Tiles[1].All( b => b == 0x75 ) );

		CollectionAssert.AreEqual( new byte[] { 0x75, 0x76 }, hmp.CodeGrid );
		CollectionAssert.AreEqual( new byte[] { 0x01, 0x00 }, hmp.Footprint );
		Assert.IsTrue( hmp.IsSolid( 0, 0 ) );
		Assert.IsFalse( hmp.IsSolid( 1, 0 ) );
	}

	[TestMethod]
	public void RejectsBadMagic()
	{
		var d = new byte[0x40];
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( 0x02, 4 ), 0xDEADBEEF );
		Assert.ThrowsExactly<InvalidDataException>( () => new HmpFile( new MemoryStream( d ) ) );
	}

	// Optional validation against a real .HMP from a coaster1.wad. Set TPW_HMP_WAD to the wad path.
	[TestMethod]
	public void ParsesRealHmpSample()
	{
		var wad = Environment.GetEnvironmentVariable( "TPW_HMP_WAD" );
		if ( string.IsNullOrEmpty( wad ) || !File.Exists( wad ) )
			Assert.Inconclusive( "Set TPW_HMP_WAD to a .wad containing coaster1.hmp to run this test." );

		var data = new WadArchive( wad ).GetFile( "coaster1.hmp" ).GetData();
		var hmp = new HmpFile( new MemoryStream( data ) );
		Assert.AreEqual( 2, hmp.Cols );
		Assert.AreEqual( 3, hmp.Rows );
		Assert.AreEqual( 6, hmp.Tiles.Count );
		Assert.IsTrue( hmp.Footprint.All( b => b == 1 ), "the coaster station footprint is fully solid" );
	}
}
