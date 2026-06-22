using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class SpriteFacingTests
{
	// A default-oriented camera: screen-right = world +X, screen-into-scene = world +Y.
	private static readonly Vector3 Right = new( 1, 0, 0 );
	private static readonly Vector3 Fwd = new( 0, 1, 0 );

	[TestMethod]
	public void DefaultCameraMapsWorldDirectionToSector()
	{
		Assert.AreEqual( 0, SpriteFacing.Sector( new Vector3( 1, 0, 0 ), Right, Fwd ), "east → 0" );
		Assert.AreEqual( 2, SpriteFacing.Sector( new Vector3( 0, 1, 0 ), Right, Fwd ), "into-scene → 2" );
		Assert.AreEqual( 4, SpriteFacing.Sector( new Vector3( -1, 0, 0 ), Right, Fwd ), "west → 4" );
		Assert.AreEqual( 6, SpriteFacing.Sector( new Vector3( 0, -1, 0 ), Right, Fwd ), "toward camera → 6" );
	}

	[TestMethod]
	public void FacingIsRelativeToTheCamera()
	{
		// The same world movement, seen through a camera orbited 90° (its right axis now points world +Y),
		// must map to a different on-screen sector — that's the whole point of camera-relative facing.
		var right90 = new Vector3( 0, 1, 0 );
		var fwd90 = new Vector3( -1, 0, 0 );
		int def = SpriteFacing.Sector( new Vector3( 1, 0, 0 ), Right, Fwd );
		int orbited = SpriteFacing.Sector( new Vector3( 1, 0, 0 ), right90, fwd90 );
		Assert.AreNotEqual( def, orbited );
		Assert.AreEqual( 6, orbited, "world +X seen from the 90°-orbited camera reads as screen-down" );
	}

	[TestMethod]
	public void AlwaysReturnsAValidSector()
	{
		for ( int deg = 0; deg < 360; deg += 7 )
		{
			float r = deg * MathF.PI / 180f;
			int s = SpriteFacing.Sector( new Vector3( MathF.Cos( r ), MathF.Sin( r ), 0 ), Right, Fwd );
			Assert.IsTrue( s is >= 0 and < 8, $"sector {s} out of range at {deg}°" );
		}
	}
}
