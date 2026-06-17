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
	public void MissingShapeFallsBackTo4x4()
	{
		Assert.AreEqual( 4, RideShape.Parse( "Info.Name\t\"x\"\n" ).Width );
		Assert.AreEqual( 4, RideShape.Parse( "" ).Height );
		Assert.AreSame( RideShape.Default, RideShape.Default );
	}
}
