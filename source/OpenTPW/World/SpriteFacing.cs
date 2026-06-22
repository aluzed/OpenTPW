namespace OpenTPW;

/// <summary>
/// Maps a peep/staff world travel direction to one of the 8 directional sprite cycles, **relative to the
/// camera** (T-035): the sprite shown matches the on-screen travel direction, so a peep walking left on
/// screen shows its left-facing frames whatever way the build camera is orbited. The direction is
/// projected onto the camera's horizontal right/forward axes, then quantised to a compass sector.
/// </summary>
public static class SpriteFacing
{
	/// <summary>
	/// The 0..7 directional sector for <paramref name="worldDir"/> (a world XY movement direction) seen
	/// through a camera whose horizontal right / forward axes are <paramref name="camRight"/> /
	/// <paramref name="camForward"/>. Sector 0 is screen-right, increasing counter-clockwise. Pure (no
	/// global state) so it is unit-testable.
	/// </summary>
	public static int Sector( Vector3 worldDir, Vector3 camRight, Vector3 camForward )
	{
		float sx = worldDir.X * camRight.X + worldDir.Y * camRight.Y;   // screen-right component
		float sy = worldDir.X * camForward.X + worldDir.Y * camForward.Y; // screen-into-scene component
		int s = (int)MathF.Round( MathF.Atan2( sy, sx ) / (MathF.PI / 4f) );
		return ((s % 8) + 8) % 8;
	}

	/// <summary>The sector for <paramref name="worldDir"/> seen through the live <see cref="Camera"/>.</summary>
	public static int Sector( Vector3 worldDir ) => Sector( worldDir, Camera.Rotation.Right, Camera.Rotation.Forward );
}
