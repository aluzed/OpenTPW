namespace OpenTPW;

/// <summary>
/// An object a ride script spawned (or the ride body), tracked by the <see cref="RideEngine"/> so
/// later opcodes (KILLOBJ / SETOBJPARAM / TRIGANIM / WAITANIM) can find it by its script handle.
/// Stage 1: sound key, params, and procedural animation state over its visual parts (real MD2
/// keyframe playback is stage 2-3).
/// </summary>
public sealed class RideObject
{
	public int Id { get; init; }
	public int Slot { get; init; }
	public int Type { get; init; }

	/// <summary>The audio cache key for sound objects (null otherwise).</summary>
	public string? SoundKey { get; set; }

	/// <summary>Script-set parameters (SETOBJPARAM), by parameter index.</summary>
	public Dictionary<int, int> Params { get; } = new();

	// Animation state (procedural for now). AnimId null = not animating.
	public int? AnimId { get; set; }
	public bool AnimLoop { get; set; }
	public float AnimStart { get; set; }
	public float AnimSpeed { get; set; } = 1f;

	/// <summary>The object's visual parts and their rest positions (the ride body has several meshes).</summary>
	public List<(ModelEntity Entity, Vector3 BasePos)> Parts { get; } = new();
}
