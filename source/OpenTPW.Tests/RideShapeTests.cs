using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class RideShapeTests
{
	[TestMethod]
	public void ParsesMonkeyFootprint4x4()
	{
		// Real monkey.sam shape (4×4).
		var sam = "Info.Name\t\"Crazy Ape\"\nInfo.Shape\n---\n**S*\n****\n****\n*2**\n---\nInfo.Hoarding\n---\nF^.7\n---\n";
		var s = RideShape.Parse( sam );
		Assert.AreEqual( 4, s.Width );
		Assert.AreEqual( 4, s.Height );
		Assert.IsTrue( s.Cells[0, 0] );          // '*'
		Assert.IsTrue( s.Cells[2, 0] );          // 'S' is still a footprint tile
		Assert.IsTrue( s.Cells[1, 3] );          // '2'

		Assert.AreEqual( (2, 0), s.Entrance );   // 'S' at column 2, row 0
		Assert.AreEqual( (1, 3), s.Exit );       // '2' at column 1, row 3
	}

	[TestMethod]
	public void NEntranceMarkerForTrackRides()
	{
		// gokarts uses 'N' for the entrance (no 'S') and '2' for the exit.
		var s = RideShape.Parse( "Info.Shape\n---\n****\n****\n*2N*\n---\n" );
		Assert.AreEqual( (2, 2), s.Entrance );   // 'N'
		Assert.AreEqual( (1, 2), s.Exit );       // '2'
	}

	[TestMethod]
	public void ParsesNonSquareFootprints()
	{
		// totem is 3×4.
		var totem = "Info.Shape\n---\n*S*\n***\n***\n*2*\n---\n";
		var t = RideShape.Parse( totem );
		Assert.AreEqual( 3, t.Width );
		Assert.AreEqual( 4, t.Height );

		// coaster1 is 2×3.
		var coaster = "Info.Shape\n---\n**\n<>\n2N\n---\n";
		var c = RideShape.Parse( coaster );
		Assert.AreEqual( 2, c.Width );
		Assert.AreEqual( 3, c.Height );
	}

	[TestMethod]
	public void SpacesAreEmptyCells()
	{
		// An L-shaped footprint: the bounding box is 3×2 but a corner is empty.
		var sam = "Info.Shape\n---\n*  \n***\n---\n";
		var s = RideShape.Parse( sam );
		Assert.AreEqual( 3, s.Width );
		Assert.AreEqual( 2, s.Height );
		Assert.IsTrue( s.Cells[0, 0] );
		Assert.IsFalse( s.Cells[1, 0], "space = not part of the footprint" );
		Assert.IsTrue( s.Cells[2, 1] );
	}

	[TestMethod]
	public void RotatedSwapsDimsAndMovesMarkers()
	{
		// totem 3×4 with entrance 'S' at (1,0) and exit '2' at (1,3).
		var s = RideShape.Parse( "Info.Shape\n---\n*S*\n***\n***\n*2*\n---\n" );
		Assert.AreEqual( (1, 0), s.Entrance );
		Assert.AreEqual( (1, 3), s.Exit );

		// 90° CW: dims swap to 4×3; a point (x,y) maps to (Height-1-y, x).
		var r1 = s.Rotated( 1 );
		Assert.AreEqual( 4, r1.Width );
		Assert.AreEqual( 3, r1.Height );
		Assert.AreEqual( (4 - 1 - 0, 1), r1.Entrance ); // (3,1)
		Assert.AreEqual( (4 - 1 - 3, 1), r1.Exit );     // (0,1)

		// 180°: dims unchanged, points flip both axes.
		var r2 = s.Rotated( 2 );
		Assert.AreEqual( 3, r2.Width );
		Assert.AreEqual( 4, r2.Height );
		Assert.AreEqual( (3 - 1 - 1, 4 - 1 - 0), r2.Entrance ); // (1,3)
		Assert.AreEqual( (3 - 1 - 1, 4 - 1 - 3), r2.Exit );     // (1,0)

		// Four turns is identity (dims + markers back to the original).
		var r4 = s.Rotated( 4 );
		Assert.AreEqual( s.Width, r4.Width );
		Assert.AreEqual( s.Height, r4.Height );
		Assert.AreEqual( s.Entrance, r4.Entrance );

		// 0 turns returns the same instance.
		Assert.AreSame( s, s.Rotated( 0 ) );
	}

	[TestMethod]
	public void RotatedPreservesCellCountAndTrackConnectors()
	{
		// coaster1 2×3 with track connectors < (in) and > (out).
		var c = RideShape.Parse( "Info.Shape\n---\n**\n<>\n2N\n---\n" );
		int cells( RideShape s ) { int n = 0; for ( int y = 0; y < s.Height; y++ ) for ( int x = 0; x < s.Width; x++ ) if ( s.Cells[x, y] ) n++; return n; }

		var r = c.Rotated( 1 );
		Assert.AreEqual( 3, r.Width );
		Assert.AreEqual( 2, r.Height );
		Assert.AreEqual( cells( c ), cells( r ), "rotation preserves the occupied-cell count" );
		Assert.IsNotNull( r.TrackIn );
		Assert.IsNotNull( r.TrackOut );
		Assert.IsTrue( r.HasTrack );
	}

	[TestMethod]
	public void MissingShapeFallsBackTo4x4()
	{
		Assert.AreEqual( 4, RideShape.Parse( "Info.Name\t\"x\"\n" ).Width );
		Assert.AreEqual( 4, RideShape.Parse( "" ).Height );
		Assert.AreSame( RideShape.Default, RideShape.Default );
	}
}
