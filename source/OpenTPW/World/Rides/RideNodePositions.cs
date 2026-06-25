namespace OpenTPW;

using ModelNode = OpenTPW.ModelFile.Node;

/// <summary>
/// Runtime resolver for a ride's <b>node world positions</b> — the seam T-048/T-047 needed so EVENT
/// effects, REPAIREFFECT/SPARK sparks, and (later) WALKON peeps / ADDHEAD heads spawn at the ride's
/// addressed node instead of dead-centre.
///
/// <para>The ride model's node graph (<see cref="ModelFile.Nodes"/>) carries each node's <i>type + id</i>
/// but <b>not</b> its position: in every shipped model the node entry's transform pointers are null and
/// the position binds to a bone transform produced at runtime by the skeleton + the ride's motion VM
/// (see docs/tickets/T-048). So node positions are <b>simulation output</b>, not file data, and this
/// resolver supplies them in two regimes:</para>
/// <list type="bullet">
///   <item><b>Moving nodes</b> (car/seat — object <c>0x80</c> + car <c>0x100</c>): the
///   <see cref="RideVehicle"/> drives them along the ride's path and <see cref="PublishMoving"/>es each
///   one's live world position every frame. These are <b>real</b> positions (the car genuinely moves
///   there).</item>
///   <item><b>Static nodes</b> (walk/head/particle): a deterministic, footprint-derived layout
///   (<see cref="BuildLayout"/>) transformed into world space by the ride's placement transform. This is
///   an engine-side <b>stand-in</b> — like the procedural ellipse path and the light/particle proxies —
///   not decoded geometry, because the authored positions don't exist statically in the file.</item>
/// </list>
/// A node not covered by either falls through (<see cref="TryResolve"/> returns false) so the caller can
/// default to the ride body. The layout math is pure and unit-tested.
/// </summary>
public sealed class RideNodePositions
{
	/// <summary>One node placed in the ride's footprint-normalised local frame (extents ±1 in X/Y).
	/// <see cref="Raised"/> lifts it off the ground (head/structural height) when worldised.</summary>
	public readonly record struct LocalNode( int NodeId, float NormX, float NormY, bool Raised );

	private readonly List<ModelNode> nodes;
	private readonly List<LocalNode> layout;
	private readonly Dictionary<int, Vector3> moving = new(); // live car/seat positions, fed by the vehicle

	private bool configured;
	private Vector3 origin;
	private int rotationStep;
	private float halfW = 0.5f, halfH = 0.5f, raiseZ = 3f;

	public RideNodePositions( IEnumerable<ModelNode>? graph )
	{
		nodes = graph?.ToList() ?? new List<ModelNode>();
		layout = BuildLayout( nodes );
	}

	/// <summary>The ride model's node graph this resolver was built from.</summary>
	public IReadOnlyList<ModelNode> Nodes => nodes;

	/// <summary>The deterministic static local layout (footprint-normalised), for tests + diagnostics.</summary>
	public IReadOnlyList<LocalNode> Layout => layout;

	/// <summary>True once <see cref="Configure"/> has supplied the ride's world transform — static-node
	/// resolution is inert until then (moving nodes still resolve, they carry absolute world positions).</summary>
	public bool IsConfigured => configured;

	/// <summary>Node ids of the ride's car/seat nodes (object <c>0x80</c> + car <c>0x100</c>), in graph
	/// order — the ones a <see cref="RideVehicle"/> drives along its path and publishes live positions for.</summary>
	public IReadOnlyList<int> CarSeatNodeIds =>
		nodes.Where( n => n.IsObject || n.IsCar ).Select( n => n.NodeId ).ToList();

	/// <summary>Fix the ride's world placement (origin, 90°-step orientation, footprint size in world
	/// units) so static nodes can be worldised. Called when the ride is placed (see Level.SpawnRideAt).</summary>
	public void Configure( Vector3 worldOrigin, int rotation, float worldWidth, float worldHeight )
	{
		origin = worldOrigin;
		rotationStep = ((rotation % 4) + 4) % 4;
		halfW = MathF.Max( worldWidth, 1f ) * 0.5f;
		halfH = MathF.Max( worldHeight, 1f ) * 0.5f;
		raiseZ = MathF.Max( 3f, 0.5f * MathF.Min( halfW, halfH ) );
		configured = true;
	}

	/// <summary>Publish a moving node's current world position (the vehicle calls this per frame for each
	/// car/seat node it carries). Overrides the static layout for that node id.</summary>
	public void PublishMoving( int nodeId, Vector3 worldPos ) => moving[nodeId] = worldPos;

	/// <summary>Forget all published moving positions (the vehicle re-publishes each frame).</summary>
	public void ClearMoving() => moving.Clear();

	/// <summary>Resolve a node id to a world position: a live moving position if the vehicle owns it,
	/// else the deterministic static layout (when configured), else false.</summary>
	public bool TryResolve( int nodeId, out Vector3 world )
	{
		if ( moving.TryGetValue( nodeId, out world ) )
			return true;
		if ( configured )
		{
			foreach ( var ln in layout )
			{
				if ( ln.NodeId != nodeId )
					continue;
				world = WorldOf( ln );
				return true;
			}
		}
		world = Vector3.Zero;
		return false;
	}

	/// <summary>Resolve a node id, or return <paramref name="fallback"/> if it can't be placed.</summary>
	public Vector3 ResolveOrDefault( int nodeId, Vector3 fallback )
		=> TryResolve( nodeId, out var w ) ? w : fallback;

	// Worldise a local node: scale by the footprint half-extents, rotate by the placement orientation
	// (the exact per-quarter-turn mapping Ride.BuildMeshEntities uses), then offset from the ride origin.
	private Vector3 WorldOf( LocalNode ln )
	{
		float x = ln.NormX * halfW;
		float y = ln.NormY * halfH;
		(float c, float s) = rotationStep switch { 1 => (0f, -1f), 2 => (-1f, 0f), 3 => (0f, 1f), _ => (1f, 0f) };
		float rx = x * c - y * s;
		float ry = x * s + y * c;
		return origin + new Vector3( rx, ry, ln.Raised ? raiseZ : 0f );
	}

	// Deterministic static layout: walk nodes spread evenly around the footprint perimeter (ground
	// level), every other node on a raised inner ring. A stand-in for the authored positions (which are
	// runtime sim output, not file data — see docs/tickets/T-048), made stable by ordering on node id.
	internal static List<LocalNode> BuildLayout( IReadOnlyList<ModelNode> graph )
	{
		var ordered = graph.OrderBy( n => n.NodeId ).ThenBy( n => n.TypeMask ).ToList();
		var walk = ordered.Where( n => n.IsWalk ).ToList();
		var inner = ordered.Where( n => !n.IsWalk ).ToList();

		var result = new List<LocalNode>( ordered.Count );
		Ring( walk, 1f, raised: false, result );  // perimeter, ground (walk paths)
		Ring( inner, 0.5f, raised: true, result ); // inner ring, raised (car/seat/head/particle attach)
		return result;
	}

	private static void Ring( List<ModelNode> group, float radius, bool raised, List<LocalNode> into )
	{
		for ( int i = 0; i < group.Count; i++ )
		{
			float a = group.Count <= 1 ? 0f : MathF.Tau * i / group.Count;
			into.Add( new LocalNode( group[i].NodeId, MathF.Cos( a ) * radius, MathF.Sin( a ) * radius, raised ) );
		}
	}
}
