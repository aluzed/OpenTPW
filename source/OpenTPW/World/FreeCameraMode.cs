using Veldrid;

namespace OpenTPW;

/// <summary>
/// A free-fly debug camera: move with WASD (+ Space/Ctrl for up/down), hold the right mouse button
/// and drag to look around, mouse wheel changes speed. Lets you explore the rendered scene and
/// inspect objects (rides, models) from any angle, instead of the fixed lobby orbit. Toggle it in
/// the lobby with F2 (see Renderer.Update).
/// </summary>
public class FreeCameraMode : CameraMode
{
	private float yaw = 225f;   // start looking down-left toward the island cluster
	private float pitch = -25f;
	private float speed = 60f;
	private bool wasRight;
	private Vector2 lastMouse;

	public FreeCameraMode()
	{
		Position = new Vector3( 480, 480, 45 );
		FieldOfView = 70f;
	}

	public override void Update()
	{
		// Mouse-look while holding the right button (relative drag, reset each frame).
		if ( Input.Mouse.Right && !wasRight )
			lastMouse = Input.Mouse.Position;

		if ( Input.Mouse.Right )
		{
			var delta = Input.Mouse.Position - lastMouse;
			yaw -= delta.X * 0.2f;
			pitch = ( pitch - delta.Y * 0.2f ).Clamp( -89f, 89f );
			lastMouse = Input.Mouse.Position;
		}

		wasRight = Input.Mouse.Right;

		// Facing direction from yaw/pitch (world up is +Z).
		var yr = yaw.DegreesToRadians();
		var pr = pitch.DegreesToRadians();
		var dir = new Vector3(
			MathF.Cos( pr ) * MathF.Cos( yr ),
			MathF.Cos( pr ) * MathF.Sin( yr ),
			MathF.Sin( pr ) );
		Rotation = Rotation.LookAt( dir );

		// Wheel adjusts move speed.
		speed = ( speed + Input.Mouse.Wheel * 10f ).Clamp( 10f, 500f );

		// WASD moves along the look direction; Space / Ctrl|Shift move straight up / down.
		var move = Rotation.Forward * Input.Forward + Rotation.Right * Input.Right;
		if ( Input.Keyboard.KeysDown.Contains( Key.Space ) )
			move += Vector3.Up;
		if ( Input.Keyboard.KeysDown.Contains( Key.ControlLeft ) || Input.Keyboard.KeysDown.Contains( Key.ShiftLeft ) )
			move -= Vector3.Up;

		Position += move.Normal * speed * Time.Delta;
	}
}
