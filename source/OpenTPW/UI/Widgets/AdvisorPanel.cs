using OpenTPW.UI;

namespace OpenTPW;

/// <summary>
/// Small HUD label for the advisor lip-sync demo (T-046): shows the speaking clip, elapsed time, and the
/// live viseme + the advisor mesh-part it drives. The 3D character itself is the <see cref="Advisor"/>
/// world entity. Enabled with it by <c>OPENTPW_ADVISOR_DEMO=1</c>.
/// </summary>
internal sealed class AdvisorPanel : HudPanel
{
	protected override void OnRender()
	{
		if ( Advisor.Current is not { } advisor || string.IsNullOrEmpty( advisor.ClipName ) )
			return;

		var shape = advisor.CurrentShape;
		var part = shape.MeshPartName() ?? "(closed)";
		Graphics.DrawText( Font, "ADVISOR LIP-SYNC", 30f, 120f, TextAlign.Left, 1.3f );
		Graphics.DrawText( Font, $"{advisor.ClipName}  {advisor.Elapsed:0.0}/{advisor.ClipLength:0.0}s",
			30f, 96f, TextAlign.Left, 1.1f );
		Graphics.DrawText( Font, $"{shape}  [{part}]", 30f, 72f, TextAlign.Left, 1.2f );
	}
}
