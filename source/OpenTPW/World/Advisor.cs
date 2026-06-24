using System.IO;
using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The Theme Park World advisor — the "Bug Head" character (<c>global/advisor.wad</c> → <c>Advisor.MD2</c>)
/// that gives the player tips. T-046: renders the real model and lip-syncs it. The model carries the five
/// viseme mouths as named sub-meshes — <c>Mouth - Normal/Aah/Eee/Ooh/Sss</c> (RE'd from the engine's
/// mouth-mesh selector <c>FUN_0044b2e0</c>, see <see cref="MouthShapeExtensions.MeshPartName"/>); we show
/// the one matching the current viseme and hide the other four. The viseme is driven from a real speech
/// clip + its companion <c>.LIP</c> (the timing wired in T-020). The advisor is anchored upright in front
/// of the camera so it reads as a corner overlay. Enabled by <c>OPENTPW_ADVISOR_DEMO=1</c>.
/// </summary>
public sealed class Advisor : Entity
{
	public static Advisor? Current { get; private set; }

	// Base parts always shown (the bug face + body); everything else (hats, hands, spatula, shut-eyes,
	// the inactive mouths) is hidden so the character reads cleanly.
	private static readonly string[] BaseParts =
	{
		"bug head", "right eye", "left eye", "body", "right antennae", "left antennae",
	};

	private sealed class Part
	{
		public ModelEntity Entity = null!;
		public Model Model = null!;
		public Vector3 LocalOffset;
		public Quaternion LocalRotation;
		public Vector3 LocalScale;
		public bool AlwaysShown;
		public MouthShape? Viseme; // set for the five "Mouth - *" parts
	}

	private readonly List<Part> parts = new();
	private LipSyncFile? lip;
	private string clipName = "";
	private float startTime = -1f;

	// On-screen anchoring (in front of the camera).
	private const float Distance = 26f, RightOffset = 16f, UpOffset = -9f, GroupScale = 2.4f;

	/// <summary>The viseme currently shown (for the HUD label); Closed when not speaking.</summary>
	public MouthShape CurrentShape { get; private set; } = MouthShape.Closed;
	public string ClipName => clipName;
	public float Elapsed => startTime < 0f ? 0f : Time.Now - startTime;
	public float ClipLength => (float)(lip?.Duration.TotalSeconds ?? 0);

	public Advisor()
	{
		Current = this;
		Name = "Advisor";
		BuildParts();
		StartSpeech();
	}

	private void BuildParts()
	{
		ModelFile model;
		try
		{
			var wadPath = Path.Join( GameDir.GamePath, "data", "global", "advisor.wad" );
			var wad = new WadArchive( wadPath );
			model = new ModelFile( new MemoryStream( wad.GetFile( "Advisor.MD2" ).GetData() ) );
			Log.Info( $"[advisor] Advisor.MD2 loaded: {model.Meshes.Count} mesh(es)" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[advisor] model load failed: {e.Message}" );
			return;
		}

		foreach ( var mesh in model.Meshes )
		{
			var name = (mesh.Name ?? "").Replace( "\0", "" ).Trim();
			var viseme = VisemeForMesh( name );
			var isBase = Array.Exists( BaseParts, p => string.Equals( p, name, StringComparison.OrdinalIgnoreCase ) );
			if ( viseme == null && !isBase )
				continue; // skip hats/hands/spatula/shut-eyes

			var built = BuildMesh( mesh );
			if ( built == null )
				continue;

			Matrix4x4.Decompose( mesh.TransformMatrix, out var scl, out var rot, out var pos );
			parts.Add( new Part
			{
				Entity = new ModelEntity { Model = isBase ? built : null },
				Model = built,
				// Match Ride.BuildMeshEntities' Y<->Z swap so local placement is consistent with the verts.
				LocalOffset = new Vector3( pos.X, pos.Z, pos.Y ),
				LocalRotation = new Quaternion( rot.X, rot.Z, rot.Y, -rot.W ),
				LocalScale = new Vector3( scl.X, scl.Z, scl.Y ),
				AlwaysShown = isBase,
				Viseme = viseme,
			} );
		}
		Log.Info( $"[advisor] built {parts.Count} part(s) from Advisor.MD2 (5 visemes + bug face)" );
	}

	private static MouthShape? VisemeForMesh( string name )
	{
		foreach ( MouthShape s in new[] { MouthShape.Normal, MouthShape.Aah, MouthShape.Eee, MouthShape.Ooh, MouthShape.Sss } )
			if ( string.Equals( name, s.MeshPartName(), StringComparison.OrdinalIgnoreCase ) )
				return s;
		return null;
	}

	// Builds a renderable Model from one MD2 mesh (the Ride.BuildMeshEntities texture/vertex pattern).
	private static Model? BuildMesh( ModelFile.Mesh mesh )
	{
		try
		{
			var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader" );
			var textures = new List<Texture>();
			for ( int i = 0; i < 16; ++i )
			{
				if ( mesh.Materials.Length <= i ) { textures.Add( Texture.Missing ); continue; }
				try { textures.Add( new Texture( $"global/advisor/textures/{mesh.Materials[i].Name}.wct", TextureFlags.Repeat ) ); }
				catch { textures.Add( Texture.Missing ); }
			}
			material.Set( "Color", [.. textures] );

			var vertices = new List<Vertex>( mesh.Vertices.Length );
			for ( int i = 0; i < mesh.Vertices.Length; ++i )
			{
				vertices.Add( new Vertex
				{
					Position = new Vector3( mesh.Vertices[i].Position.X, mesh.Vertices[i].Position.Z, mesh.Vertices[i].Position.Y ),
					Normal = mesh.Normals[i],
					TexCoords = mesh.TexCoords[i],
					TexIndex = (int)mesh.Vertices[i].TextureIndex,
					MatFlags = mesh.Materials.Length == 0 ? 0 : mesh.Materials[(int)mesh.Vertices[i].TextureIndex].Flags,
				} );
			}
			return new Model( [.. vertices], mesh.Indices, material );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[advisor] mesh '{mesh.Name}' build failed: {e.Message}" );
			return null;
		}
	}

	private void StartSpeech()
	{
		try
		{
			var speechDir = Path.Join( GameDir.GamePath, "data", "levels", "jungle", "Speech" );
			var lipPath = Path.Join( speechDir, "lips", "sp_001.LIP" );
			var sdtPath = Path.Join( speechDir, "speechHD.SDT" );
			if ( !File.Exists( lipPath ) || !File.Exists( sdtPath ) )
				return;

			lip = new LipSyncFile( File.OpenRead( lipPath ) );
			var clip = new SdtArchive( sdtPath ).soundFiles
				.FirstOrDefault( f => f.Name.StartsWith( "sp_001", StringComparison.OrdinalIgnoreCase ) );
			if ( clip == null )
				return;
			clipName = clip.Name;
			Audio.PlaySpeech( $"advisor_{clip.Name}", clip.SoundData );
			startTime = Time.Now;
			Log.Info( $"[advisor] speaking '{clipName}', {lip.Keyframes.Count} keyframes, {ClipLength:0.0}s" );
		}
		catch ( Exception e ) { Log.Warning( $"[advisor] speech failed: {e.Message}" ); }
	}

	protected override void OnUpdate()
	{
		if ( parts.Count == 0 )
			return;

		// Advance lip-sync; loop the clip so the demo keeps talking.
		CurrentShape = MouthShape.Closed;
		if ( lip != null && startTime >= 0f )
		{
			var elapsed = Time.Now - startTime;
			if ( elapsed > ClipLength + 1f )
			{
				ReplaySpeech();
				elapsed = 0f;
			}
			CurrentShape = lip.ShapeAt( TimeSpan.FromSeconds( elapsed ) );
		}

		// Anchor the whole assembly upright, in front of and facing the camera.
		var cam = Camera.Position;
		var fwd = Camera.Rotation.Forward;
		var right = Camera.Rotation.Right;
		var up = Camera.Rotation.Up;
		var groupPos = cam + fwd * Distance + right * RightOffset + up * UpOffset;

		// Face the camera horizontally, staying upright (world Z up). +MathF.PI aligns the model's front.
		var toCam = cam - groupPos;
		var yaw = MathF.Atan2( toCam.Y, toCam.X ) + MathF.PI / 2f;
		var groupRot = Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );

		foreach ( var part in parts )
		{
			// Only the active viseme's mouth is visible.
			if ( part.Viseme is { } v )
				part.Entity.Model = v == CurrentShape ? part.Model : null;
			else if ( part.AlwaysShown )
				part.Entity.Model = part.Model;

			var offset = System.Numerics.Vector3.Transform( (part.LocalOffset * GroupScale).GetSystemVector3(), groupRot );
			part.Entity.Position = groupPos + (Vector3)offset;
			part.Entity.Rotation = groupRot * part.LocalRotation;
			part.Entity.Scale = part.LocalScale * GroupScale;
		}
	}

	private void ReplaySpeech()
	{
		try
		{
			var sdtPath = Path.Join( GameDir.GamePath, "data", "levels", "jungle", "Speech", "speechHD.SDT" );
			var clip = new SdtArchive( sdtPath ).soundFiles.FirstOrDefault( f => f.Name == clipName );
			if ( clip != null )
				Audio.PlaySpeech( $"advisor_{clipName}", clip.SoundData );
			startTime = Time.Now;
		}
		catch { /* keep the visemes cycling even if audio replay fails */ }
	}

	protected override void OnDelete()
	{
		foreach ( var part in parts )
			Entity.All.Remove( part.Entity );
		parts.Clear();
		if ( Current == this )
			Current = null;
	}
}
