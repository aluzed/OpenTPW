using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class CoasterTrackTests
{
	// A minimal coaster.sam excerpt: the authored track cross-section (asCrossSectionPoints1[*]), tab-
	// separated like the real files. Mirrors coaster1.wad/coaster.sam's 7-point channel profile.
	private const string Sam =
		"asCrossSectionPoints1[0].fX\t\t-4.0\n" +
		"asCrossSectionPoints1[0].fY\t\t2.5\n" +
		"asCrossSectionPoints1[0].fU\t\t1.0\n" +
		"asCrossSectionPoints1[1].fX\t\t-3.0\n" +
		"asCrossSectionPoints1[1].fY\t\t2.0\n" +
		"asCrossSectionPoints1[1].fU\t\t0.1\n" +
		"asCrossSectionPoints1[2].fX\t\t3.0\n" +
		"asCrossSectionPoints1[2].fY\t\t2.0\n" +
		"asCrossSectionPoints1[2].fU\t\t0.9\n" +
		"asCrossSectionPoints1[3].fX\t\t4.0\n" +
		"asCrossSectionPoints1[3].fY\t\t2.5\n" +
		"asCrossSectionPoints1[3].fU\t\t1.0\n" +
		"asCrossSectionPoints1[4].fX\t\t0.0\n" +
		"asCrossSectionPoints1[4].fY\t\t-1.5\n" +
		"asCrossSectionPoints1[4].fU\t\t0.0\n";

	[TestMethod]
	public void ParsesAllProfilePointsInOrder()
	{
		var settings = new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( Sam ) ) );
		var pts = CoasterTrack.ParseCrossSection( settings );

		Assert.AreEqual( 5, pts.Count );
		Assert.AreEqual( -4.0f, pts[0].X, 1e-4f );
		Assert.AreEqual( 2.5f, pts[0].Y, 1e-4f );
		Assert.AreEqual( 1.0f, pts[0].U, 1e-4f );
		// The channel floor: centred (X=0) and the lowest point (Y=-1.5).
		Assert.AreEqual( 0.0f, pts[4].X, 1e-4f );
		Assert.AreEqual( -1.5f, pts[4].Y, 1e-4f );
	}

	[TestMethod]
	public void EmptySettingsYieldsNoPoints()
	{
		var settings = new SettingsFile( new MemoryStream( Encoding.ASCII.GetBytes( "someOtherKey\t1.0\n" ) ) );
		Assert.AreEqual( 0, CoasterTrack.ParseCrossSection( settings ).Count );
	}
}
