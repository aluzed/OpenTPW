using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class ParticleLibraryFileTests
{
	// Mirrors the real layout: 16-byte header (count, recordSize) then fixed records,
	// each = param block + a 48-byte null-padded name, then a trailing block.
	private static byte[] BuildPlb()
	{
		const int count = 2, recordSize = 64, nameField = 48;
		const int paramLen = recordSize - nameField; // 16
		var trailing = new byte[] { 0xAA, 0xBB, 0xCC };

		var buf = new byte[16 + count * recordSize + trailing.Length];
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), count );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), recordSize );

		// Record 0: params 1..16, name "NULL".
		for ( var i = 0; i < paramLen; i++ )
			buf[16 + i] = (byte)(i + 1);
		Encoding.ASCII.GetBytes( "NULL" ).CopyTo( buf, 16 + paramLen );

		// Record 1: name "Sparks".
		Encoding.ASCII.GetBytes( "Sparks" ).CopyTo( buf, 16 + recordSize + paramLen );

		trailing.CopyTo( buf, 16 + count * recordSize );
		return buf;
	}

	[TestMethod]
	public void ParsesHeaderRecordsAndNames()
	{
		using var stream = new MemoryStream( BuildPlb() );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( 64, plb.RecordSize );
		Assert.AreEqual( 2, plb.Effects.Count );

		Assert.AreEqual( "NULL", plb.Effects[0].Name );
		Assert.AreEqual( 0, plb.Effects[0].Index );
		Assert.AreEqual( 16, plb.Effects[0].Parameters.Length );
		Assert.AreEqual( 1, plb.Effects[0].Parameters[0] );

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
	}
}
