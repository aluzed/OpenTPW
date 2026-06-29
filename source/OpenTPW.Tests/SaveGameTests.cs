using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class SaveGameTests
{
	[TestMethod]
	public void RoundTripsThroughJson()
	{
		var s = new SaveGame { Version = 1, Money = 12345f, EntryFee = 8f, ClockSeconds = 99.5f };
		s.Loans.Add( new SaveGame.LoanState { Name = "Large", Bought = true, Outstanding = 17700f, Monthly = 1475f } );
		s.Placements.Add( new SaveGame.Placement { Kind = "ride", Name = "totem", TileX = 10, TileY = 20, Rotation = 2, TicketPrice = 7f } );
		s.Placements.Add( new SaveGame.Placement { Kind = "shop", Name = "drink", TileX = 5, TileY = 6, TicketPrice = 12f } );

		var back = SaveGame.FromJson( s.ToJson() );

		Assert.IsNotNull( back );
		Assert.AreEqual( 12345f, back!.Money );
		Assert.AreEqual( 8f, back.EntryFee );
		Assert.AreEqual( 99.5f, back.ClockSeconds );

		Assert.AreEqual( 1, back.Loans.Count );
		Assert.AreEqual( "Large", back.Loans[0].Name );
		Assert.IsTrue( back.Loans[0].Bought );
		Assert.AreEqual( 17700f, back.Loans[0].Outstanding );

		Assert.AreEqual( 2, back.Placements.Count );
		var ride = back.Placements[0];
		Assert.AreEqual( "ride", ride.Kind );
		Assert.AreEqual( "totem", ride.Name );
		Assert.AreEqual( 10, ride.TileX );
		Assert.AreEqual( 20, ride.TileY );
		Assert.AreEqual( 2, ride.Rotation );
		Assert.AreEqual( 7f, ride.TicketPrice );
		Assert.AreEqual( "drink", back.Placements[1].Name );
		Assert.AreEqual( 12f, back.Placements[1].TicketPrice, "the stall's sale price round-trips (T-041)" );
	}

	[TestMethod]
	public void BadJsonDeserializesToNull()
	{
		Assert.IsNull( SaveGame.FromJson( "not valid json {{" ) );
	}
}
