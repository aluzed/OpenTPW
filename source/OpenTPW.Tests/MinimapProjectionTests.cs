using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Numerics;

namespace OpenTPW.Tests;

[TestClass]
public class MinimapProjectionTests
{
	// A park spanning world (100..300) on X and (50..250) on Y (200×200 units), drawn into a 160×160 map at
	// base-space (1000, 20). FlipY off so the maths are easy to reason about.
	private static MinimapProjection Proj( bool flipY = false )
		=> new( OriginX: 100f, OriginY: 50f, WorldW: 200f, WorldH: 200f, Map: new Rectangle( 1000f, 20f, 160f, 160f ), FlipY: flipY );

	[TestMethod]
	public void ProjectsCornersAndCentre()
	{
		var p = Proj();
		var min = p.Project( 100f, 50f );
		Assert.AreEqual( 1000f, min.X, 1e-3f );
		Assert.AreEqual( 20f, min.Y, 1e-3f );

		var max = p.Project( 300f, 250f );
		Assert.AreEqual( 1160f, max.X, 1e-3f );
		Assert.AreEqual( 180f, max.Y, 1e-3f );

		var mid = p.Project( 200f, 150f );
		Assert.AreEqual( 1080f, mid.X, 1e-3f );
		Assert.AreEqual( 100f, mid.Y, 1e-3f );
	}

	[TestMethod]
	public void ClampsOffParkPositionsToTheEdge()
	{
		var p = Proj();
		var off = p.Project( 9999f, -9999f );
		Assert.AreEqual( 1160f, off.X, 1e-3f, "east of the park pins to the right edge" );
		Assert.AreEqual( 20f, off.Y, 1e-3f, "south of the park pins to the bottom edge" );
	}

	[TestMethod]
	public void FlipYInvertsTheVerticalAxis()
	{
		var p = Proj( flipY: true );
		// World min-Y now maps to the TOP of the map rect (Y + Height), max-Y to the bottom (Y).
		Assert.AreEqual( 180f, p.Project( 100f, 50f ).Y, 1e-3f );
		Assert.AreEqual( 20f, p.Project( 100f, 250f ).Y, 1e-3f );
	}

	[TestMethod]
	public void UnprojectRoundTrips()
	{
		foreach ( var flip in new[] { false, true } )
		{
			var p = Proj( flip );
			var world = new Vector2( 175f, 210f );
			var screen = p.Project( world.X, world.Y );
			var (wx, wy) = p.Unproject( screen );
			Assert.AreEqual( world.X, wx, 1e-2f, $"flip={flip}" );
			Assert.AreEqual( world.Y, wy, 1e-2f, $"flip={flip}" );
		}
	}

	[TestMethod]
	public void ContainsMatchesTheMapRect()
	{
		var p = Proj();
		Assert.IsTrue( p.Contains( new Vector2( 1080f, 100f ) ) );
		Assert.IsFalse( p.Contains( new Vector2( 999f, 100f ) ) );
		Assert.IsFalse( p.Contains( new Vector2( 1080f, 200f ) ) );
	}

	[TestMethod]
	public void DegenerateBoundsProjectToCentreWithoutDividingByZero()
	{
		var p = new MinimapProjection( 0f, 0f, 0f, 0f, new Rectangle( 0f, 0f, 100f, 100f ), FlipY: false );
		var c = p.Project( 1234f, 5678f );
		Assert.AreEqual( 50f, c.X, 1e-3f );
		Assert.AreEqual( 50f, c.Y, 1e-3f );
	}
}
