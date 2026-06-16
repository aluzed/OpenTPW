using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class MTRFileTests
{
	[TestMethod]
	public void ReadsHeaderNameAndData()
	{
		var name = Encoding.ASCII.GetBytes( "s_test\0" );
		var dataLen = 8;                    // bytes 36..44
		var nameOffset = 36 + dataLen;      // 44
		var bytes = new byte[nameOffset + name.Length];

		BinaryPrimitives.WriteUInt32LittleEndian( bytes.AsSpan( 0, 4 ), 0x2E5915AF ); // magic
		BinaryPrimitives.WriteUInt32LittleEndian( bytes.AsSpan( 4, 4 ), 6 );          // version
		BinaryPrimitives.WriteUInt32LittleEndian( bytes.AsSpan( 20, 4 ), (uint)nameOffset );
		for ( var i = 0; i < dataLen; i++ )
			bytes[36 + i] = (byte)(i + 1);
		name.CopyTo( bytes, nameOffset );

		using var stream = new MemoryStream( bytes );
		var mtr = new MTRFile( stream );

		Assert.AreEqual( 6u, mtr.Version );
		Assert.AreEqual( "s_test", mtr.Name );
		Assert.AreEqual( dataLen, mtr.Data.Length );

		// The body decodes as whole uint32s: bytes 1..8 -> {0x04030201, 0x08070605}.
		CollectionAssert.AreEqual( new uint[] { 0x04030201, 0x08070605 }, mtr.Indices );
	}

	[TestMethod]
	public void RejectsBadMagic()
	{
		using var stream = new MemoryStream( new byte[40] );
		Assert.ThrowsExactly<InvalidDataException>( () => new MTRFile( stream ) );
	}

	// Optional validation against a real .MTR. Set TPW_MTR_SAMPLE.
	[TestMethod]
	public void ParsesRealMtrSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_MTR_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_MTR_SAMPLE to a Theme Park World .MTR file to run this test." );

		using var stream = File.OpenRead( path );
		var mtr = new MTRFile( stream );

		Assert.IsTrue( mtr.Name.Length > 0, "should read a material name" );
		StringAssert.StartsWith( mtr.Name, "s_" ); // observed naming, e.g. s_bkrupt
		Assert.IsTrue( mtr.Data.Length > 0 );

		// The body is a whole-uint32 index array.
		Assert.AreEqual( mtr.Data.Length / 4, mtr.Indices.Length );
		Assert.IsTrue( mtr.Indices.Length > 0, "should decode an index array" );
	}
}
