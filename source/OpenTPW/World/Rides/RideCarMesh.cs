namespace OpenTPW;

/// <summary>
/// Builds a ride car's renderable mesh from a ride-WAD <c>.md2</c> (the coaster's <c>CrocCar.MD2</c>, the
/// bumper's <c>b_car.MD2</c>, …): one mesh, per-material <c>.wct</c> textures (the same path the ride body
/// uses), Y/Z-swizzled, and <b>centred on its own centroid</b> so it sits on the point it's placed at.
/// Shared by <see cref="CoasterTrain"/> and <see cref="RideVehicle"/>.
/// </summary>
public static class RideCarMesh
{
	/// <summary>The car mesh + its decomposed scale + half-extent (the larger of its X/Y half-spans, for
	/// fitting it to a tile). Returns null if the mesh won't load (caller falls back to a placeholder box).</summary>
	public static (Model Model, Vector3 DecompScale, float HalfExtent)? Build( string archive, string name )
	{
		try
		{
			var mesh = new ModelFile( $"{archive}/{name}" ).Meshes[0];

			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();
			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i ) { textures.Add( Texture.Missing ); continue; }
				textures.Add( Ride.LoadRideTexture( archive, mesh.Materials[i].Name ) );
			}
			material.Set( "Color", [.. textures] );

			System.Numerics.Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out _, out _ );

			var centroid = Vector3.Zero;
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
				centroid += new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y );
			centroid *= 1f / mesh.Vertices.Length;

			var vertices = new List<Vertex>( mesh.Vertices.Length );
			float halfExtent = 0f;
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				var p = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ) - centroid;
				halfExtent = MathF.Max( halfExtent, MathF.Max( MathF.Abs( p.X ), MathF.Abs( p.Y ) ) );
				vertices.Add( new Vertex
				{
					Position = p,
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags,
				} );
			}

			var model = new Model( [.. vertices], mesh.Indices, material );
			return (model, new Vector3( scl.X, scl.Z, scl.Y ), halfExtent);
		}
		catch
		{
			return null;
		}
	}
}
