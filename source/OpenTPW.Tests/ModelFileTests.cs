using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ModelFileTests
{
	// An out-of-range offset (as in unsupported .MD2 variants like GARROW.MD2) must fail
	// with a clear InvalidDataException, not an opaque EndOfStreamException. See T-012.
	[TestMethod]
	public void RejectsOutOfRangeOffsets()
	{
		var data = new byte[0x80];
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0, 4 ), 0x1CD15D46 ); // magic
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x36, 2 ), 0 );        // frameCount
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x44, 2 ), 1 );        // meshCnt
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x50, 4 ), 0 );        // off2
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x54, 4 ), 0 );        // frameList
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x70, 4 ), 0x1000_0000 ); // bogus meshPtr

		using var stream = new MemoryStream( data );
		Assert.ThrowsExactly<InvalidDataException>( () => new ModelFile( stream ) );
	}

	[TestMethod]
	public void RejectsBadMagic()
	{
		using var stream = new MemoryStream( new byte[0x80] ); // magic 0, not 0x1CD15D46
		Assert.ThrowsExactly<InvalidDataException>( () => new ModelFile( stream ) );
	}

	// Optional validation of the .MD2 parser against a real model. Set TPW_MODEL_SAMPLE.
	[TestMethod]
	public void ParsesRealModelSample()
	{
		Log = new();

		var path = Environment.GetEnvironmentVariable( "TPW_MODEL_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_MODEL_SAMPLE to a Theme Park World .MD2 file to run this test." );

		using var stream = File.OpenRead( path );
		var model = new ModelFile( stream );

		Assert.IsTrue( model.Meshes.Count > 0, "model should have meshes" );
		foreach ( var mesh in model.Meshes )
		{
			Assert.IsNotNull( mesh.Vertices );
			Assert.IsTrue( mesh.Vertices.Length > 0, "mesh should have vertices" );
			Assert.IsNotNull( mesh.Indices );
			Assert.IsTrue( mesh.Indices.Length > 0 && mesh.Indices.Length % 3 == 0, "indices form triangles" );
			Assert.IsTrue( mesh.Indices.All( i => i < mesh.Vertices.Length ), "indices reference valid vertices" );
			Assert.IsTrue(
				mesh.Vertices.All( v => float.IsFinite( v.Position.X ) && float.IsFinite( v.Position.Y ) && float.IsFinite( v.Position.Z ) ),
				"vertex positions are finite" );
		}

		// Verified by rendering: a real PAUSED.MD2 reconstructs as the 3D "PAUSED" sign.
		// Note: the static .MD2 variant (frameCount 0, e.g. GARROW.MD2) is detected and
		// rejected with a clear InvalidDataException; full decode is future work (T-012).
	}
}
