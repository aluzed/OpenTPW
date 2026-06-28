namespace OpenTPW;

/// <summary>
/// A real peep-sprite billboard used as the visible figure for a ride's WALKON peeps and ADDHEAD heads
/// (T-048 art swap). Wraps a shared <see cref="SpriteSheet"/> (a kid sprite) exactly the way <see cref="Peep"/>
/// does — a camera-facing quad showing the directional walk-cycle frame for the travel/forward direction — so
/// a ride's walk/head nodes now show an animated peep instead of an emissive cube stand-in. Falls back to a
/// flat-colour billboard when the sprite can't be decoded (headless / missing assets), so the engine always
/// has something to draw and the pure node-positioning path is unchanged.
/// </summary>
internal sealed class RideSpriteMarker
{
	private const float WalkFps = 8f; // walk-cycle frames per second (matches Peep.WalkFps)

	private readonly SpriteSheet? sheet;
	private readonly float spriteHeight;
	private float walkPhase;
	private Vector3 moveDir = new( 0, -1, 0 ); // last travel/forward direction, for the directional frame

	/// <summary>The renderable entity (auto-registered in <see cref="Entity.All"/> on construction).</summary>
	public ModelEntity Entity { get; }

	/// <summary>Build a marker from the kid sprite <paramref name="dir"/>/<paramref name="name"/>, scaled to
	/// <paramref name="height"/> world units; a flat <paramref name="fr"/>/<paramref name="fg"/>/<paramref name="fb"/>
	/// billboard is used when the sprite can't load.</summary>
	public RideSpriteMarker( string dir, string name, float height, byte fr, byte fg, byte fb )
	{
		spriteHeight = height;
		sheet = SpriteSheet.Load( dir, name );
		Entity = new ModelEntity
		{
			Model = sheet != null ? sheet.FrameModel( 0 ) : Billboard.Make( fr, fg, fb ),
			Scale = sheet != null
				? new Vector3( spriteHeight / sheet.RefHeight, 1f, spriteHeight / sheet.RefHeight )
				: new Vector3( 3f, 1f, 6f ),
		};
	}

	/// <summary>Place + animate the marker this frame: stand at world <paramref name="position"/>, derive the
	/// directional frame from world travel/forward <paramref name="dir"/>, and advance the walk cycle while
	/// <paramref name="moving"/> (idle holds the standing frame). The quad yaws to face the live camera.</summary>
	public void Update( Vector3 position, Vector3 dir, bool moving )
	{
		Entity.Position = position;
		if ( dir.LengthSquared > 1e-6f )
			moveDir = dir;
		FaceCamera( position );

		if ( sheet == null )
			return;
		walkPhase = moving ? walkPhase + WalkFps * Time.Delta : 0f; // idle holds the standing frame
		int frame = FrameFor( sheet.Anims, SpriteFacing.Sector( moveDir ), walkPhase );
		Entity.Model = sheet.FrameModel( frame );
		float p2w = spriteHeight / sheet.RefHeight; // one px→world factor for the whole sheet (no jitter)
		Entity.Scale = new Vector3( p2w, 1f, p2w );
	}

	// Cylindrical billboard yaw about world up so the quad's +Y face points at the camera (matches Peep).
	private void FaceCamera( Vector3 position )
	{
		var cam = Camera.Position;
		float yaw = MathF.Atan2( -(cam.X - position.X), cam.Y - position.Y );
		Entity.Rotation = System.Numerics.Quaternion.CreateFromAxisAngle( System.Numerics.Vector3.UnitZ, yaw );
	}

	/// <summary>The frame index for directional sector <paramref name="facing"/> at walk phase
	/// <paramref name="walkPhase"/>: the matching directional cycle's start plus the looped phase, or frame 0
	/// when the sheet ships no directional anims. Pure (unit-tested).</summary>
	internal static int FrameFor( IReadOnlyList<SpriteSheet.Anim> anims, int facing, float walkPhase )
	{
		if ( anims.Count == 0 )
			return 0;
		var a = anims[((facing % anims.Count) + anims.Count) % anims.Count];
		return a.Start + ( a.Count > 0 ? (int)walkPhase % a.Count : 0 );
	}
}
