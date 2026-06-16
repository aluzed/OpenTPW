using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class ParticleLibraryFileTests
{
	// Mirrors the real layout: 16-byte header (count, recordSize) then fixed records,
	// each = param block + a 48-byte null-padded name, then a trailing block.
	// recordSize must exceed the 48-byte name + 64-byte colour ramp.
	private const int RecordSize = 64 + 48; // 64-byte param block (= the colour ramp) + name
	private const int ParamLen = RecordSize - 48;

	private static byte[] BuildPlb()
	{
		const int count = 2;
		var trailing = new byte[] { 0xAA, 0xBB, 0xCC };

		var buf = new byte[16 + count * RecordSize + trailing.Length];
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), count );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), RecordSize );

		// Record 0: a recognisable colour ramp (param block is exactly the 64 ramp bytes),
		// name "NULL". Stop k = D3DCOLOR 0xAARRGGBB with R=k, G=2k, B=3k, A=4k.
		var rampStart = 16; // paramLen == 64 == ramp, so the ramp is the whole param block
		for ( var k = 0; k < 16; k++ )
		{
			uint argb = (uint)((4 * k) << 24 | (k) << 16 | (2 * k) << 8 | (3 * k));
			BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( rampStart + k * 4, 4 ), argb );
		}
		Encoding.ASCII.GetBytes( "NULL" ).CopyTo( buf, 16 + ParamLen );

		// Record 1: name "Sparks".
		Encoding.ASCII.GetBytes( "Sparks" ).CopyTo( buf, 16 + RecordSize + ParamLen );

		trailing.CopyTo( buf, 16 + count * RecordSize );
		return buf;
	}

	[TestMethod]
	public void ParsesHeaderRecordsAndNames()
	{
		using var stream = new MemoryStream( BuildPlb() );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( RecordSize, plb.RecordSize );
		Assert.AreEqual( 2, plb.Effects.Count );

		Assert.AreEqual( "NULL", plb.Effects[0].Name );
		Assert.AreEqual( 0, plb.Effects[0].Index );
		Assert.AreEqual( ParamLen, plb.Effects[0].Parameters.Length );

		// Colour ramp decoded from the D3DCOLOR words (R=k, G=2k, B=3k, A=4k).
		var ramp = plb.Effects[0].ColorRamp;
		Assert.AreEqual( 16, ramp.Count );
		Assert.AreEqual( new ParticleColor( 0, 0, 0, 0 ), ramp[0] );
		Assert.AreEqual( new ParticleColor( 5, 10, 15, 20 ), ramp[5] );

		Assert.AreEqual( "Sparks", plb.Effects[1].Name );
		Assert.AreEqual( 3, plb.TrailingData.Length );
	}

	[TestMethod]
	public void RejectsTruncatedRecords()
	{
		// Header claims 10 records of 320 bytes, but the file is far too small.
		var buf = new byte[16];
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), 10 );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), 320 );

		using var stream = new MemoryStream( buf );
		Assert.ThrowsExactly<InvalidDataException>( () => new ParticleLibraryFile( stream ) );
	}

	// Optional validation against a real .PLB. Set TPW_PLB_SAMPLE (e.g. Data/Particle/Tp2.plb).
	[TestMethod]
	public void ParsesRealPlbSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_PLB_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_PLB_SAMPLE to a Theme Park World .PLB file to run this test." );

		using var stream = File.OpenRead( path );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( 320, plb.RecordSize, "observed record size on Tp2.plb" );
		Assert.IsTrue( plb.Effects.Count >= 101, "should expose the full effect catalogue" );

		// Names must line up with par_lib.h's P_EFFECT_* order.
		Assert.AreEqual( "NULL", plb.Effects[0].Name );
		Assert.AreEqual( "Sparks", plb.Effects[1].Name );
		Assert.AreEqual( "Smoke", plb.Effects[2].Name );
		Assert.AreEqual( "Test2D", plb.Effects[100].Name );

		// Every effect carries a 16-stop colour ramp.
		Assert.AreEqual( 16, plb.Effects[1].ColorRamp.Count );

		// Smoke is white with an alpha that fades in from 0 — a sanity check that the
		// channel order (D3DCOLOR ARGB) is decoded correctly.
		var smoke = plb.Effects[2].ColorRamp;
		Assert.AreEqual( new ParticleColor( 255, 255, 255, 0 ), smoke[0] );
		Assert.IsTrue( smoke[8].A > smoke[0].A, "smoke alpha should ramp up from 0" );
		Assert.IsTrue( smoke.All( c => c is { R: 255, G: 255, B: 255 } ), "smoke ramp is white" );
	}
}
