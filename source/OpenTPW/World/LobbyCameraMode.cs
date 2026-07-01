namespace OpenTPW;

public class LobbyCameraMode : CameraMode
{
	private float Height => 70f;
	private float Distance => 150f;
	private float Speed => 0.15f;

	// Orbit the centroid of the four island spots (X,Y in 400..600) so the whole world cluster is framed, not
	// just one island — the world-select needs the islands on-screen to click them (T-063).
	private Vector3 Target => new Vector3( 500, 500, 12.5f );

	public override void Update()
	{
		float x = MathF.Sin( Time.Now * Speed ) * Distance;
		float y = MathF.Cos( Time.Now * Speed ) * Distance;

		Position = new Vector3( Target.X + x, Target.Y + y, Height );
		Rotation = Rotation.LookAt( Target - Position );

		FieldOfView = 60;
	}
}
