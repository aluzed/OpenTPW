using System.IO;
using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// Advisor lip-sync demo (T-020): plays a real advisor speech clip (<c>sp_001.mp2</c> from
/// <c>speechHD.SDT</c>) and drives a mouth that changes shape in sync, from the companion
/// <c>sp_001.LIP</c> keyframes via <see cref="LipSyncFile.ShapeAt"/>. This wires the decoded lip-sync
/// format + the five engine visemes (RE'd from <c>FUN_0044b2e0</c>) to real audio playback with visible
/// output. The five mouth shapes are drawn procedurally here; swapping the real advisor model's named
/// "mouth - *" sub-meshes (see <see cref="MouthShapeExtensions.MeshPartName"/>) is the remaining 3D-render
/// step. Enabled by <c>OPENTPW_ADVISOR_DEMO=1</c>. See docs/tickets/T-020.
/// </summary>
internal sealed class AdvisorPanel : HudPanel
{
	private static Texture? face, eye, mouth, bg;
	private static Texture Face => face ??= Solid( 120, 180, 90, 255 );   // bug-head green
	private static Texture Eye => eye ??= Solid( 245, 245, 245, 255 );
	private static Texture Mouth => mouth ??= Solid( 30, 14, 18, 255 );   // dark mouth interior
	private static Texture Bg => bg ??= Solid( 16, 16, 26, 180 );

	// Face placed bottom-centre of the 1280×720 base space (Y-up, origin bottom-left).
	private const float FaceX = 540f, FaceY = 30f, FaceW = 200f, FaceH = 200f;

	private LipSyncFile? lip;
	private string clipName = "";
	private float startTime = -1f;
	private bool loadTried;

	private void EnsureLoaded()
	{
		if ( loadTried )
			return;
		loadTried = true;
		try
		{
			var speechDir = Path.Join( GameDir.GamePath, "data", "levels", "jungle", "Speech" );
			var sdtPath = Path.Join( speechDir, "speechHD.SDT" );
			var lipPath = Path.Join( speechDir, "lips", "sp_001.LIP" );
			if ( !File.Exists( sdtPath ) || !File.Exists( lipPath ) )
			{
				Log.Warning( "[advisor] speech assets not found; demo disabled" );
				return;
			}

			lip = new LipSyncFile( File.OpenRead( lipPath ) );

			var sdt = new SdtArchive( sdtPath );
			var clip = sdt.soundFiles.FirstOrDefault( f => f.Name.StartsWith( "sp_001", StringComparison.OrdinalIgnoreCase ) )
				?? sdt.soundFiles.FirstOrDefault();
			if ( clip == null )
			{
				Log.Warning( "[advisor] no speech clip in speechHD.SDT; demo disabled" );
				return;
			}

			clipName = clip.Name;
			Audio.PlaySfx( $"advisor_{clip.Name}", clip.SoundData );
			startTime = Time.Now;
			Log.Info( $"[advisor] lip-sync demo: clip '{clipName}', {lip.Keyframes.Count} keyframes, {lip.Duration.TotalSeconds:0.0}s" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[advisor] demo load failed: {e.Message}" );
		}
	}

	protected override void OnRender()
	{
		EnsureLoaded();
		if ( lip == null || startTime < 0f )
			return;

		var elapsed = Time.Now - startTime;
		// Loop the clip so the demo is easy to observe (replay shortly after it ends).
		if ( elapsed > (float)lip.Duration.TotalSeconds + 1f )
		{
			Audio.PlaySfx( $"advisor_{clipName}", new SdtArchive(
				Path.Join( GameDir.GamePath, "data", "levels", "jungle", "Speech", "speechHD.SDT" ) )
				.soundFiles.First( f => f.Name == clipName ).SoundData );
			startTime = Time.Now;
			elapsed = 0f;
		}

		var shape = lip.ShapeAt( TimeSpan.FromSeconds( elapsed ) );

		var mat = Material.UI;

		// Backing plate + head.
		mat.Set( "Color", Bg );
		Graphics.Quad( new Rectangle( FaceX - 14f, FaceY - 14f, FaceW + 28f, FaceH + 64f ), mat );
		mat.Set( "Color", Face );
		Graphics.Quad( new Rectangle( FaceX, FaceY, FaceW, FaceH ), mat );

		// Eyes.
		mat.Set( "Color", Eye );
		Graphics.Quad( new Rectangle( FaceX + 45f, FaceY + 130f, 34f, 34f ), mat );
		Graphics.Quad( new Rectangle( FaceX + 120f, FaceY + 130f, 34f, 34f ), mat );

		// Mouth — sized/shaped per viseme, centred under the eyes.
		var (mw, mh) = MouthSize( shape );
		var cx = FaceX + FaceW / 2f;
		var cy = FaceY + 70f;
		mat.Set( "Color", Mouth );
		Graphics.Quad( new Rectangle( cx - mw / 2f, cy - mh / 2f, mw, mh ), mat );

		// Labels: the live viseme + the RE'd advisor mesh-part name it maps to.
		Graphics.DrawText( Font, "ADVISOR LIP-SYNC", FaceX - 10f, FaceY + FaceH + 40f, TextAlign.Left, 1.3f );
		Graphics.DrawText( Font, $"{clipName}  {elapsed:0.0}/{lip.Duration.TotalSeconds:0.0}s",
			FaceX - 10f, FaceY + FaceH + 16f, TextAlign.Left, 1.1f );
		var part = shape.MeshPartName() ?? "(closed)";
		Graphics.DrawText( Font, $"{shape}  [{part}]", FaceX - 10f, FaceY - 6f, TextAlign.Left, 1.2f );
	}

	// The procedural mouth footprint (width, height) for each viseme.
	private static (float W, float H) MouthSize( MouthShape shape ) => shape switch
	{
		MouthShape.Aah => (70f, 60f),    // wide open
		MouthShape.Eee => (96f, 18f),    // wide, thin
		MouthShape.Ooh => (44f, 48f),    // small round
		MouthShape.Sss => (60f, 24f),    // medium narrow
		MouthShape.Normal => (64f, 30f), // relaxed open
		_ => (70f, 8f),                  // Closed — thin line
	};
}
