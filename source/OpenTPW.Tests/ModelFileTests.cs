using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class ModelFileTests
{
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
		// Note: the parser is NOT robust to every .MD2 variant yet (e.g. the small
		// GARROW.MD2 throws EndOfStreamException) — see docs/tickets/T-012.
	}
}
