using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using SVec2 = System.Numerics.Vector2;

namespace OpenTPW.Tests;

[TestClass]
public class CarSimTests
{
	// T-048 / docs/08: the bumper-car arena sim — the BUMP branch of the original car engine. Cars steer to
	// random waypoints, bounce off the walls, and collide pairwise. These cover the behaviour faithfully
	// (waypoint positions are a stand-in; the motion is the re-implemented part).

	// A deterministic [0,1) source cycling through a fixed table (so RandomPoint placements are reproducible).
	private static Func<float> Cycle( params float[] vals )
	{
		int i = 0;
		return () => vals[i++ % vals.Length];
	}

	[TestMethod]
	public void CarsStayWithinTheArena()
	{
		const float HalfW = 10f, HalfH = 8f;
		var sim = new CarSim( 5, HalfW, HalfH, radius: 1.5f, speed: 14f,
			rng: Cycle( 0.1f, 0.9f, 0.5f, 0.2f, 0.8f, 0.35f, 0.65f ) );

		for ( int step = 0; step < 200; step++ )
			sim.Step( 0.05f );

		foreach ( var c in sim.Cars )
		{
			Assert.IsTrue( c.Pos.X >= -HalfW - 1e-3f && c.Pos.X <= HalfW + 1e-3f, $"X in arena ({c.Pos.X})" );
			Assert.IsTrue( c.Pos.Y >= -HalfH - 1e-3f && c.Pos.Y <= HalfH + 1e-3f, $"Y in arena ({c.Pos.Y})" );
		}
	}

	[TestMethod]
	public void ReachingTargetPicksANewWaypoint()
	{
		var sim = new CarSim( 1, 10f, 10f, radius: 1f, speed: 14f, rng: Cycle( 0.9f, 0.1f ) );
		// Sit the car exactly on its target → within the arrival radius → it must pick a new waypoint.
		sim.PlaceForTest( 0, new SVec2( 2f, 3f ), new SVec2( 2f, 3f ) );

		sim.Step( 0.1f );

		var t = sim.Cars[0].Target;
		Assert.IsFalse( t.X == 2f && t.Y == 3f, "a car on its target re-targets to a new random waypoint" );
	}

	[TestMethod]
	public void OverlappingCarsBounceApart()
	{
		// Two cars 1 unit apart with radius 2 (min separation 4) — the collision must push them to ≥ 2·radius.
		var sim = new CarSim( 2, 20f, 20f, radius: 2f, speed: 0f, rng: Cycle( 0.5f ) );
		sim.PlaceForTest( 0, new SVec2( 0f, 0f ), new SVec2( 0f, 0f ) );
		sim.PlaceForTest( 1, new SVec2( 1f, 0f ), new SVec2( 1f, 0f ) );

		sim.Step( 0.1f );

		float dist = SVec2.Distance( sim.Cars[0].Pos, sim.Cars[1].Pos );
		Assert.IsTrue( dist >= 4f - 1e-2f, $"overlapping cars are pushed to ≥ 2·radius ({dist})" );
	}

	[TestMethod]
	public void WallBouncesTheCarBackInside()
	{
		// A car at the right edge driving outward must be clamped and turned back inward.
		var sim = new CarSim( 1, 5f, 5f, radius: 0.5f, speed: 20f, rng: Cycle( 0.5f ) );
		sim.PlaceForTest( 0, new SVec2( 4.9f, 0f ), new SVec2( 100f, 0f ) ); // target far outside +X

		for ( int i = 0; i < 30; i++ )
			sim.Step( 0.05f );

		Assert.IsTrue( sim.Cars[0].Pos.X <= 5f + 1e-3f, "clamped inside the right wall" );
		Assert.IsTrue( sim.Cars[0].Vel.X <= 0f, "velocity reversed off the wall (heading back inward)" );
	}

	[TestMethod]
	public void HardCollisionRecordsAnImpact()
	{
		// Two cars closing head-on (each targeting far past the other) collide → one bump at the midpoint (T-047).
		var sim = new CarSim( 2, 20f, 20f, radius: 2f, speed: 14f, rng: Cycle( 0.5f ) );
		sim.PlaceForTest( 0, new SVec2( -1.5f, 0f ), new SVec2( 50f, 0f ) );  // driving +X toward car 1
		sim.PlaceForTest( 1, new SVec2( 1.5f, 0f ), new SVec2( -50f, 0f ) );  // driving -X toward car 0

		sim.Step( 0.1f );

		Assert.AreEqual( 1, sim.Impacts.Count, "a head-on knock records one impact" );
		Assert.IsTrue( sim.Impacts[0].Speed > 2f, $"closing speed above the audible threshold ({sim.Impacts[0].Speed})" );
		Assert.AreEqual( 0f, sim.Impacts[0].Pos.X, 0.7f, "impact near the contact midpoint" );
	}

	[TestMethod]
	public void RestingContactDoesNotBump()
	{
		// Two overlapping but motionless cars (speed 0, sitting on their targets) bounce apart geometrically
		// but record NO impact — only a real closing knock fires a sound.
		var sim = new CarSim( 2, 20f, 20f, radius: 2f, speed: 0f, rng: Cycle( 0.5f ) );
		sim.PlaceForTest( 0, new SVec2( 0f, 0f ), new SVec2( 0f, 0f ) );
		sim.PlaceForTest( 1, new SVec2( 1f, 0f ), new SVec2( 1f, 0f ) );

		sim.Step( 0.1f );

		Assert.AreEqual( 0, sim.Impacts.Count, "resting contact is not an audible bump" );
	}

	[TestMethod]
	public void ImpactsClearBetweenSteps()
	{
		var sim = new CarSim( 2, 20f, 20f, radius: 2f, speed: 14f, rng: Cycle( 0.5f ) );
		sim.PlaceForTest( 0, new SVec2( -1.5f, 0f ), new SVec2( 50f, 0f ) );
		sim.PlaceForTest( 1, new SVec2( 1.5f, 0f ), new SVec2( -50f, 0f ) );
		sim.Step( 0.1f );
		Assert.AreEqual( 1, sim.Impacts.Count );

		// Pull them far apart so the next step has no collision → the impact list is cleared.
		sim.PlaceForTest( 0, new SVec2( -15f, 0f ), new SVec2( -15f, 0f ) );
		sim.PlaceForTest( 1, new SVec2( 15f, 0f ), new SVec2( 15f, 0f ) );
		sim.Step( 0.1f );
		Assert.AreEqual( 0, sim.Impacts.Count, "impacts are recomputed each step, not accumulated" );
	}
}
