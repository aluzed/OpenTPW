namespace OpenTPW;

/// <summary>
/// A slow high orbit framing the whole park terrain — used by the dev park demo to show the real
/// jungle landscape (which is far larger than the lobby's island cluster, so <see cref="LobbyCameraMode"/>
/// sits too close). Set <see cref="Target"/> to the terrain centre.
/// </summary>
public class ParkOverviewCameraMode : CameraMode
{
	public static Vector3 Target = new( 228, 0, 0 );
	public static float Radius = 950f;
	public static float Height = 560f;

	private const float Speed = 0.08f;

	public override void Update()
	{
		float x = MathF.Sin( Time.Now * Speed ) * Radius;
		float y = MathF.Cos( Time.Now * Speed ) * Radius;

		Position = new Vector3( Target.X + x, Target.Y + y, Height );
		Rotation = Rotation.LookAt( Target - Position );
		FieldOfView = 55;
	}
}
