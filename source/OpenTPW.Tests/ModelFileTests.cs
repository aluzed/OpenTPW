using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

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
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0, 4 ), 0x1CD15D46 );  // magic
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 4, 4 ), 0xDD );        // version
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 8, 4 ), 0xCB );        // subVersion
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x36, 2 ), 1 );        // frameCount
		BinaryPrimitives.WriteUInt16LittleEndian( data.AsSpan( 0x44, 2 ), 1 );        // meshCnt
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x50, 4 ), 0 );        // off2
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x54, 4 ), 0 );        // frameList
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0x70, 4 ), 0x1000_0000 ); // bogus meshPtr

		using var stream = new MemoryStream( data );
		Assert.ThrowsExactly<InvalidDataException>( () => new ModelFile( stream ) );
	}

	// The original loader gates on the version fields at offsets 4/8 (current = 0xDD/0xCB). A genuinely
	// unsupported version (not the current one and not the legacy static 0x18/0x17) is rejected.
	[TestMethod]
	public void RejectsUnknownVersion()
	{
		var data = new byte[0x80];
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 0, 4 ), 0x1CD15D46 ); // magic
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 4, 4 ), 0x99 );       // unknown version
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( 8, 4 ), 0x99 );

		using var stream = new MemoryStream( data );
		Assert.ThrowsExactly<InvalidDataException>( () => new ModelFile( stream ) );
	}

	// The legacy static variant (GARROW.MD2 / RARROW.MD2, version 0x18/0x17) is a 2-byte-packed layout
	// (T-015): header ptr @0x72 -> mesh table {u32 count, 12B pad, descriptors}; each descriptor
	// {u16 numVerts, u16 numFaces, u32 vertPtr, u32 facePtr, float}; verts = 32B (pos,normal,uv);
	// faces = 24B with the 3 indices at +2/+4/+6. This builds a minimal one and checks it decodes.
	[TestMethod]
	public void DecodesLegacyStaticVariant()
	{
		const int meshTbl = 0x80, descOff = meshTbl + 16, vertPtr = 0xA0, facePtr = 0x100, end = 0x118;
		var d = new byte[end];
		void U32( int o, uint v ) => BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), v );
		void U16( int o, ushort v ) => BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( o, 2 ), v );
		void F32( int o, float v ) => BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o, 4 ), v );

		U32( 0, 0x1CD15D46 ); U32( 4, 0x18 ); U32( 8, 0x17 );
		Encoding.ASCII.GetBytes( "arrow\0" ).CopyTo( d, 0x18 );
		U32( 0x72, meshTbl );             // header -> mesh table
		U32( meshTbl, 1 );                // mesh count
		U16( descOff, 3 );                // numVerts
		U16( descOff + 2, 1 );            // numFaces
		U32( descOff + 4, vertPtr );
		U32( descOff + 8, facePtr );
		F32( descOff + 12, 1f );          // scale

		// 3 vertices: pos / normal / uv.
		var pos = new[] { (0f, 0f, 0f), (1f, 0f, 0f), (0f, 1f, 0f) };
		for ( int i = 0; i < 3; i++ )
		{
			int o = vertPtr + i * 32;
			F32( o, pos[i].Item1 ); F32( o + 4, pos[i].Item2 ); F32( o + 8, pos[i].Item3 );
			F32( o + 12, 0f ); F32( o + 16, 0f ); F32( o + 20, 1f ); // normal
			F32( o + 24, i == 1 ? 1f : 0f ); F32( o + 28, i == 2 ? 1f : 0f ); // uv
		}
		// 1 face: flag, then indices 0,1,2.
		U16( facePtr, 12 ); U16( facePtr + 2, 0 ); U16( facePtr + 4, 1 ); U16( facePtr + 6, 2 );

		var model = new ModelFile( new MemoryStream( d ) );
		Assert.AreEqual( 1, model.Meshes.Count );
		var mesh = model.Meshes[0];
		Assert.AreEqual( "arrow", mesh.Name );
		Assert.AreEqual( 3, mesh.Vertices.Length );
		CollectionAssert.AreEqual( new uint[] { 0, 1, 2 }, mesh.Indices );
		Assert.AreEqual( 1f, mesh.Vertices[1].Position.X, 1e-4f );
		Assert.AreEqual( 0f, mesh.Vertices[1].Position.Y, 1e-4f );
		Assert.AreEqual( 1f, mesh.Vertices[2].Position.Y, 1e-4f );
		Assert.AreEqual( 1f, mesh.TexCoords[1].X, 1e-4f );
		Assert.AreEqual( 0f, mesh.TexCoords[1].Y, 1e-4f );
		Assert.IsTrue( mesh.Indices.All( i => i < mesh.Vertices.Length ) );
	}

	[TestMethod]
	public void RejectsBadMagic()
	{
		using var stream = new MemoryStream( new byte[0x80] ); // magic 0, not 0x1CD15D46
		Assert.ThrowsExactly<InvalidDataException>( () => new ModelFile( stream ) );
	}

	// The node graph (T-048): count u16 @ 0x48, table file-offset u32 @ 0x7c, 0x14 bytes/node
	// {u32 typeMask, u32 nodeId, u32 extra, ...}. Mirrors the real ride models (Bird.MD2 = 11 nodes, etc.).
	[TestMethod]
	public void ParsesNodeTable()
	{
		const int tableOff = 0x90;
		var d = new byte[tableOff + 2 * 0x14];
		void U16( int o, ushort v ) => BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( o, 2 ), v );
		void U32( int o, uint v ) => BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), v );

		U16( 0x48, 2 );              // node count
		U32( 0x7c, tableOff );       // node table file offset
		// node 0: a walk-ish node (type 0xB1, id 9); node 1: a coaster car/track node (type 0x811, id 2)
		U32( tableOff + 0, 0xB1 ); U32( tableOff + 4, 9 ); U32( tableOff + 8, 0 );
		U32( tableOff + 0x14 + 0, 0x811 ); U32( tableOff + 0x14 + 4, 2 ); U32( tableOff + 0x14 + 8, 0 );

		var nodes = ModelFile.ParseNodeTable( d );
		Assert.AreEqual( 2, nodes.Count );
		Assert.AreEqual( 0xB1u, nodes[0].TypeMask );
		Assert.AreEqual( 9, nodes[0].NodeId );
		Assert.AreEqual( 0x811u, nodes[1].TypeMask );
		Assert.AreEqual( 2, nodes[1].NodeId );

		// Type classification (RE'd selectors): 0x80 object/head, 0x800 walk, 0x100 car.
		Assert.IsTrue( nodes[0].IsObject, "0xB1 carries the 0x80 object/head bit" );
		Assert.IsFalse( nodes[0].IsWalk );
		Assert.IsTrue( nodes[1].IsWalk, "0x811 carries the 0x800 walk bit" );
		Assert.IsFalse( nodes[1].IsObject );
		// A coaster car/track node (0x111) carries the 0x100 car bit.
		var carNode = new ModelFile.Node { TypeMask = 0x111 };
		Assert.IsTrue( carNode.IsCar );

		// Absent / implausible table → empty (not a throw).
		Assert.AreEqual( 0, ModelFile.ParseNodeTable( new byte[0x80] ).Count );
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

		// The model carries its own texture binding (the runtime never loads the companion
		// .MTR — Ghidra confirms no .mtr loader): each material names a texture embedded in
		// the .MD2 (e.g. PAUSED.MD2 -> "paws_grad"). See T-018.
		Assert.IsTrue(
			model.Meshes.Any( m => m.Materials.Any( mat => !string.IsNullOrEmpty( mat.Name ) ) ),
			"a model should bind at least one texture name from the .MD2 itself" );

		// Verified by rendering: a real PAUSED.MD2 reconstructs as the 3D "PAUSED" sign.
		// Note: legacy .MD2 versions (offset-4 != 0xDD, e.g. GARROW.MD2 = 0x18) are rejected
		// with a clear InvalidDataException, matching the original loader; see T-015.
	}
}
