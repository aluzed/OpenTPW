using System.Numerics;

namespace OpenTPW;

/// <summary>
/// A ride instance: loads its RSE script + settings + model from the ride's `.wad`, drives the
/// script through a <see cref="RideEngine"/>, and renders the ride's geometry. Slice 1 of the ride
/// engine — the model is static and engine opcodes cover sound (see the plan / docs/tickets/T-007).
/// </summary>
public class Ride : Entity
{
	public RideVM VM { get; private set; }

	private readonly RideEngine engine = new();

	public Ride( string rideArchive, Vector3 position )
	{
		Position = position;
		var rideName = Path.GetFileNameWithoutExtension( rideArchive );

		// Script (the VFS resolves the path into the .wad; matching is case-insensitive — T-014).
		VM = new RideVM( FileSystem.OpenRead( $"{rideArchive}/{rideName}.rse" ) );
		VM.Engine = engine;

		// SPAWNCHILD loads sibling child scripts from the same WAD, sharing this ride's engine.
		VM.ChildLoader = name =>
		{
			try
			{
				return new RideVM( FileSystem.OpenRead( $"{rideArchive}/{name}.rse" ) ) { Engine = VM.Engine };
			}
			catch ( Exception e )
			{
				Log.Warning( $"[ride] child script '{name}' not found: {e.Message}" );
				return null;
			}
		};

		try
		{
			var settings = new SettingsFile( FileSystem.OpenRead( $"{rideArchive}/{rideName}.sam" ) );
			var rideTitle = settings.Entries.Where( x => x.Key == "Info.Name" ).Select( x => x.Value ).FirstOrDefault();
			Name = string.IsNullOrEmpty( rideTitle ) ? rideName : rideTitle;
			Log.Info( $"[ride] loaded '{Name}' from {rideArchive}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] settings unavailable: {e.Message}" );
		}

		LoadModel( $"{rideArchive}/{rideName}.md2", rideArchive );

		VM.IsRunning = true;
	}

	// Loads the ride's main model and spawns a ModelEntity per mesh (the LobbyIsland pattern). Ride
	// textures live in the WAD under textures/; missing ones fall back to Texture.Missing. Any failure
	// is non-fatal — the VM still runs (so the sound/engine proof holds) even if geometry doesn't load.
	private void LoadModel( string md2Path, string rideArchive )
	{
		try
		{
			var modelFile = new ModelFile( md2Path );
			Log.Info( $"[ride] model {md2Path}: {modelFile.Meshes.Count} mesh(es)" );

			var bodyParts = new List<ModelEntity>();
			foreach ( var mesh in modelFile.Meshes )
			{
				var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
				var textures = new List<Texture>();
				for ( int i = 0; i < 16; ++i )
				{
					if ( mesh.Materials.Length <= i )
					{
						textures.Add( Texture.Missing );
						continue;
					}

					try { textures.Add( new Texture( $"{rideArchive}/textures/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) ); }
					catch { textures.Add( Texture.Missing ); }
				}
				material.Set( "Color", [.. textures] );

				var vertices = new List<Vertex>();
				for ( int i = 0; i < mesh.Vertices.Length; ++i )
				{
					vertices.Add( new Vertex
					{
						Position = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ),
						Normal = mesh.Normals[i],
						TexCoords = mesh.TexCoords[i],
						TexIndex = (int)mesh.Vertices[i].TextureIndex,
						MatFlags = mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags
					} );
				}

				var model = new Model( [.. vertices], mesh.Indices, material );
				Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );

				bodyParts.Add( new ModelEntity
				{
					Model = model,
					Scale = new Vector3( scl.X, scl.Z, scl.Y ),
					Rotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W ),
					Position = new Vector3( pos.X, pos.Z, pos.Y ) + Position,
				} );
			}

			// Register the meshes as the ride body so the engine can animate them (it starts a looping
			// idle so the model visibly moves and is easy to pick out).
			engine.RegisterBody( bodyParts );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ride] model load failed ({md2Path}): {e.Message}" );
		}
	}

	protected override void OnUpdate()
	{
		// Null-guarded: the Entity base ctor registers this in Entity.All before the ctor body runs,
		// so a ride that failed to load (VM never assigned) would otherwise crash the update loop.
		VM?.Update();
		engine.Update( Time.Now );
	}
}
