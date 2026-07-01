namespace OpenTPW;

/// <summary>
/// Loads a shop's real building model from the active theme's WAD (a food/drink stall under <c>shops/</c>,
/// a toilet under <c>features/</c>) as a set of textured child entities — so a stall reads as a building
/// instead of a flat coloured billboard. TPW ships several stalls per theme under different names; the first
/// candidate that loads is used. Returns false (leaving the caller on its billboard) when none resolve, so a
/// theme without a matching model still renders something.
/// </summary>
internal static class ShopBuilding
{
	// Candidate archive paths per kind for a theme, tried in order. Food/drink stalls live under shops/ and
	// share the ride-model convention, so they drop in cleanly. Toilets are a "feature" under features/ whose
	// models use a different authored transform (they came out tiny/offset), so they stay on the billboard for
	// now — an empty candidate list makes TryLoad fall back.
	private static IEnumerable<string> Candidates( ShopKind kind, string theme ) => kind switch
	{
		ShopKind.Drink => new[] { "coconut", "drinks", "cola", "ices", "icecream" }.Select( n => $"levels/{theme}/shops/{n}" ),
		ShopKind.Toilet => Enumerable.Empty<string>(),
		_ => new[] { "burger", "fries", "steak", "hotdog", "sburger", "gchips" }.Select( n => $"levels/{theme}/shops/{n}" ),
	};

	/// <summary>Try to build the real shop model at <paramref name="origin"/>, appending its per-mesh entities
	/// to <paramref name="parts"/> (the caller removes them on demolish). True if a model loaded.</summary>
	public static bool TryLoad( ShopKind kind, Vector3 origin, List<ModelEntity> parts )
	{
		foreach ( var archive in Candidates( kind, Level.Current.Name ) )
		{
			var name = Path.GetFileName( archive );
			try
			{
				var modelFile = new ModelFile( $"{archive}/{name}.md2" );
				if ( modelFile.Meshes.Count == 0 )
					continue;
				BuildParts( modelFile, archive, origin, parts );
				if ( parts.Count > 0 )
				{
					Log.Info( $"[shop] {kind}: building '{name}' ({parts.Count} mesh(es))" );
					return true;
				}
			}
			catch { /* try the next candidate */ }
		}
		return false;
	}

	// One textured ModelEntity per mesh, Y/Z-swizzled and placed at its authored offset from the stall origin
	// (the LobbyIsland / Ride pattern). Textures load through the shared ride loader (stexture/, chroma-keyed).
	private static void BuildParts( ModelFile modelFile, string archive, Vector3 origin, List<ModelEntity> parts )
	{
		foreach ( var mesh in modelFile.Meshes )
		{
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();
			for ( int i = 0; i < 16; ++i )
				textures.Add( mesh.Materials.Length <= i ? Texture.Missing : Ride.LoadRideTexture( archive, mesh.Materials[i].Name ) );
			material.Set( "Color", [.. textures] );

			var vertices = new List<Vertex>( mesh.Vertices.Length );
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
				vertices.Add( new Vertex
				{
					Position = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ),
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags,
				} );

			var model = new Model( [.. vertices], mesh.Indices, material );
			System.Numerics.Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );
			parts.Add( new ModelEntity
			{
				Model = model,
				Scale = new Vector3( scl.X, scl.Z, scl.Y ),
				Rotation = new System.Numerics.Quaternion( rot.X, rot.Z, rot.Y, -rot.W ),
				Position = new Vector3( pos.X, pos.Z, pos.Y ) + origin,
			} );
		}
	}
}
