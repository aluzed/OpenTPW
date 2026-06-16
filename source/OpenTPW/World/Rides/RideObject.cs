namespace OpenTPW;

/// <summary>
/// An object a ride script spawned via ADDOBJ, tracked by the <see cref="RideEngine"/> so later
/// opcodes (KILLOBJ / SETOBJPARAM / animation) can find it by its script handle. Kind-specific
/// payload (model entity, sound key, …) is filled in as stages land; slice 1 uses the sound key.
/// </summary>
public sealed class RideObject
{
	public int Id { get; init; }
	public int Slot { get; init; }
	public int Type { get; init; }

	/// <summary>The world model for visual objects (null for pure sound/particle objects).</summary>
	public ModelEntity? Model { get; set; }

	/// <summary>The audio cache key for sound objects (null otherwise).</summary>
	public string? SoundKey { get; set; }
}
