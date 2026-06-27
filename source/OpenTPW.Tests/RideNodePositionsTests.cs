using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RideNodePositionsTests
{
	// T-048/T-047: the runtime node→world-position resolver. Car/seat nodes get a live position published
	// by the vehicle; the rest get a deterministic footprint layout, worldised by the ride's placement
	// transform. EVENT/SPARK effects resolve their target node through it instead of the ride centre.

	private const float Eps = 1e-3f;

	private static ModelFile.Node Node( uint typeMask, int id ) => new() { TypeMask = typeMask, NodeId = id };

	private static RideNodePositions Field( params ModelFile.Node[] nodes ) => new( nodes );

	[TestMethod]
	public void LayoutSplitsWalkAndInnerNodes()
	{
		// Two walk nodes (0x800) ring the perimeter at radius 1, ground level; an object node (0x80) sits
		// on the raised inner ring at radius 0.5.
		var f = Field( Node( 0x800, 5 ), Node( 0x800, 3 ), Node( 0x80, 1 ) );
		var layout = f.Layout;

		Assert.AreEqual( 3, layout.Count, "every node is placed" );

		var walk3 = layout.Single( l => l.NodeId == 3 );
		var walk5 = layout.Single( l => l.NodeId == 5 );
		var obj = layout.Single( l => l.NodeId == 1 );

		// Walk nodes: radius 1, not raised; ordered by id so node 3 leads (angle 0) and node 5 trails (π).
		Assert.IsFalse( walk3.Raised );
		Assert.AreEqual( 1f, MathF.Sqrt( walk3.NormX * walk3.NormX + walk3.NormY * walk3.NormY ), Eps, "walk on the unit perimeter" );
		Assert.AreEqual( 1f, walk3.NormX, Eps, "first walk node at angle 0" );
		Assert.AreEqual( -1f, walk5.NormX, Eps, "second walk node at angle π" );

		// Inner node: radius 0.5, raised.
		Assert.IsTrue( obj.Raised );
		Assert.AreEqual( 0.5f, MathF.Sqrt( obj.NormX * obj.NormX + obj.NormY * obj.NormY ), Eps, "inner ring at half radius" );
	}

	[TestMethod]
	public void CarSeatNodeIdsAreTheObjectAndCarNodesInOrder()
	{
		var f = Field( Node( 0x80, 1 ), Node( 0x800, 9 ), Node( 0x100, 2 ), Node( 0x80, 3 ) );
		CollectionAssert.AreEqual( new[] { 1, 2, 3 }, f.CarSeatNodeIds.ToArray(),
			"object (0x80) + car (0x100) nodes, in graph order; the walk node is excluded" );
	}

	[TestMethod]
	public void UnconfiguredStaticNodesDoNotResolve()
	{
		var f = Field( Node( 0x80, 1 ) );
		Assert.IsFalse( f.IsConfigured );
		Assert.IsFalse( f.TryResolve( 1, out _ ), "static nodes are inert until the ride is placed" );
		Assert.AreEqual( Vector3.Up, f.ResolveOrDefault( 1, Vector3.Up ), "falls back to the caller's default" );
	}

	[TestMethod]
	public void MovingPositionResolvesEvenWhenUnconfigured()
	{
		// The vehicle publishes absolute world positions, so a moving node resolves without Configure.
		var f = Field( Node( 0x80, 7 ) );
		var p = new Vector3( 1f, 2f, 3f );
		f.PublishMoving( 7, p );

		Assert.IsTrue( f.TryResolve( 7, out var got ) );
		Assert.AreEqual( p, got );

		f.ClearMoving();
		Assert.IsFalse( f.TryResolve( 7, out _ ), "cleared each frame; re-published by the vehicle" );
	}

	[TestMethod]
	public void MovingPositionOverridesStaticLayout()
	{
		var f = Field( Node( 0x80, 1 ) );
		f.Configure( Vector3.Zero, rotation: 0, worldWidth: 8f, worldHeight: 8f );
		Assert.IsTrue( f.TryResolve( 1, out var stat ), "configured → static layout resolves" );

		var live = new Vector3( 100f, 200f, 5f );
		f.PublishMoving( 1, live );
		Assert.IsTrue( f.TryResolve( 1, out var got ) );
		Assert.AreEqual( live, got, "the live car/seat position wins over the static layout" );
		Assert.AreNotEqual( stat, got );
	}

	[TestMethod]
	public void BodyNodeIdsAreEveryNonWalkNodeOrderedById()
	{
		// The engine binds these onto the animated body surfaces; walk paths are excluded (they ring the
		// footprint on the ground). Ordered by id (then type) for a stable node→surface assignment.
		var f = Field( Node( 0x80, 3 ), Node( 0x800, 9 ), Node( 0x100, 1 ), Node( 0x80, 2 ) );
		CollectionAssert.AreEqual( new[] { 1, 2, 3 }, f.BodyNodeIds.ToArray(),
			"non-walk nodes (object/car) ordered by id; the walk node (9) is excluded" );
	}

	[TestMethod]
	public void BodyPositionResolvesAboveStaticLayout()
	{
		// The engine publishes a live body-part position (real per-frame keyframe transform) — it beats the
		// static footprint ring but yields to a vehicle-owned moving position.
		var f = Field( Node( 0x80, 1 ) );
		f.Configure( Vector3.Zero, rotation: 0, worldWidth: 8f, worldHeight: 8f );
		Assert.IsTrue( f.TryResolve( 1, out var stat ), "configured → static layout resolves" );

		var bodyPos = new Vector3( 3f, 4f, 9f );
		f.PublishBody( 1, bodyPos );
		Assert.IsTrue( f.TryResolve( 1, out var got ) );
		Assert.AreEqual( bodyPos, got, "the live body-part position wins over the static layout" );
		Assert.AreNotEqual( stat, got );

		f.ClearBody();
		Assert.IsTrue( f.TryResolve( 1, out var back ), "cleared → falls back to the static layout" );
		Assert.AreEqual( stat, back );
	}

	[TestMethod]
	public void VehicleMovingPositionWinsOverBodyPosition()
	{
		// Priority: vehicle-moving > body (keyframe) > static layout. A car/seat node the vehicle owns keeps
		// its sim position even if the body anim also published it.
		var f = Field( Node( 0x80, 1 ) );
		f.PublishBody( 1, new Vector3( 3f, 4f, 9f ) );
		var seat = new Vector3( 50f, 60f, 7f );
		f.PublishMoving( 1, seat );

		Assert.IsTrue( f.TryResolve( 1, out var got ) );
		Assert.AreEqual( seat, got, "the vehicle's live seat position wins" );
	}

	[TestMethod]
	public void BodyPositionResolvesEvenWhenUnconfigured()
	{
		// Like moving positions, body positions are absolute world coords, so they resolve before Configure.
		var f = Field( Node( 0x80, 2 ) );
		var p = new Vector3( 1f, 2f, 3f );
		f.PublishBody( 2, p );
		Assert.IsTrue( f.TryResolve( 2, out var got ) );
		Assert.AreEqual( p, got );
	}

	[TestMethod]
	public void FacingResolvesVehicleTangentOverBodyForward()
	{
		// The original reads a node's forward (matrix +0x20) alongside its position. Priority mirrors
		// TryResolve: a live vehicle tangent wins over the animated part's forward.
		var f = Field( Node( 0x80, 1 ) );

		f.PublishBody( 1, Vector3.Zero, new Vector3( 0f, 1f, 0f ) );
		Assert.IsTrue( f.TryResolveFacing( 1, out var body ) );
		Assert.AreEqual( new Vector3( 0f, 1f, 0f ), body, "body forward when only the engine published" );

		f.PublishMoving( 1, Vector3.Zero, new Vector3( 1f, 0f, 0f ) );
		Assert.IsTrue( f.TryResolveFacing( 1, out var moving ) );
		Assert.AreEqual( new Vector3( 1f, 0f, 0f ), moving, "the vehicle tangent wins" );
	}

	[TestMethod]
	public void FacingIsUnsetWithoutAPublishedDirection()
	{
		// A position-only publish (or the static layout) carries no facing → TryResolveFacing is false.
		var f = Field( Node( 0x80, 1 ) );
		f.Configure( Vector3.Zero, rotation: 0, worldWidth: 8f, worldHeight: 8f );
		f.PublishBody( 1, new Vector3( 1f, 2f, 3f ) ); // no forward
		Assert.IsTrue( f.TryResolve( 1, out _ ), "position still resolves" );
		Assert.IsFalse( f.TryResolveFacing( 1, out _ ), "no facing was published" );
	}

	[TestMethod]
	public void ClearDropsBothPositionAndFacing()
	{
		var f = Field( Node( 0x80, 1 ) );
		f.PublishMoving( 1, Vector3.One, new Vector3( 1f, 0f, 0f ) );
		f.ClearMoving();
		Assert.IsFalse( f.TryResolveFacing( 1, out _ ), "moving facing cleared with the position" );

		f.PublishBody( 1, Vector3.One, new Vector3( 0f, 1f, 0f ) );
		f.ClearBody();
		Assert.IsFalse( f.TryResolveFacing( 1, out _ ), "body facing cleared with the position" );
	}

	[TestMethod]
	public void StaticNodeWorldisedAtOriginAndFootprint()
	{
		// One walk node (angle 0 → +X), footprint 8×4 → half-extents 4×2, origin (10,20,5), no rotation.
		var f = Field( Node( 0x800, 1 ) );
		f.Configure( new Vector3( 10f, 20f, 5f ), rotation: 0, worldWidth: 8f, worldHeight: 4f );

		Assert.IsTrue( f.TryResolve( 1, out var w ) );
		Assert.AreEqual( 14f, w.X, Eps, "+X by half the footprint width" );
		Assert.AreEqual( 20f, w.Y, Eps );
		Assert.AreEqual( 5f, w.Z, Eps, "walk node stays on the ground" );
	}

	[TestMethod]
	public void RaisedInnerNodeLiftsOffTheGround()
	{
		var f = Field( Node( 0x80, 1 ) );
		f.Configure( new Vector3( 0f, 0f, 0f ), rotation: 0, worldWidth: 8f, worldHeight: 4f );
		Assert.IsTrue( f.TryResolve( 1, out var w ) );
		Assert.IsTrue( w.Z > 0f, "object/head nodes sit above the footprint" );
	}

	[TestMethod]
	public void PlacementRotationRotatesTheNode()
	{
		// Walk node at +X (angle 0); a quarter-turn (rotation 1) maps it onto -Y, mirroring Ride's footprint
		// rotation. Footprint 8×8 → half-extent 4.
		var f = Field( Node( 0x800, 1 ) );
		f.Configure( Vector3.Zero, rotation: 1, worldWidth: 8f, worldHeight: 8f );

		Assert.IsTrue( f.TryResolve( 1, out var w ) );
		Assert.AreEqual( 0f, w.X, Eps );
		Assert.AreEqual( -4f, w.Y, Eps, "rotation step 1 sends +X to -Y" );
	}
}
