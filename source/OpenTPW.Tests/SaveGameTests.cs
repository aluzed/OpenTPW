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

	[TestMethod]
	public void RideProgressionRoundTrips()
	{
		var s = new SaveGame();
		s.Placements.Add( new SaveGame.Placement
		{
			Kind = "ride", Name = "totem", TileX = 3, TileY = 4,
			UpgradeLevel = 2, ResearchedLevel = 3, Researching = true, ResearchFraction = 0.5f,
			ResearchQueuePos = 1, Reliability = 0.25f, Broken = true,
		} );

		var back = SaveGame.FromJson( s.ToJson() )!.Placements[0];

		Assert.AreEqual( 2, back.UpgradeLevel );
		Assert.AreEqual( 3, back.ResearchedLevel );
		Assert.IsTrue( back.Researching );
		Assert.AreEqual( 0.5f, back.ResearchFraction );
		Assert.AreEqual( 1, back.ResearchQueuePos );
		Assert.AreEqual( 0.25f, back.Reliability );
		Assert.IsTrue( back.Broken );
	}

	[TestMethod]
	public void StaffRoundTrips()
	{
		var s = new SaveGame();
		s.Staff.Add( new SaveGame.StaffState { Role = "Guard", X = 12f, Y = -7f } );
		s.Staff.Add( new SaveGame.StaffState { Role = "Mechanic", X = 1f, Y = 2f, HasZone = true, ZoneX = 1f, ZoneY = 2f, ZoneRadius = 30f } );

		var back = SaveGame.FromJson( s.ToJson() )!;

		Assert.AreEqual( 2, back.Staff.Count );
		Assert.AreEqual( "Guard", back.Staff[0].Role );
		Assert.AreEqual( 12f, back.Staff[0].X );
		Assert.IsFalse( back.Staff[0].HasZone );
		Assert.IsTrue( back.Staff[1].HasZone );
		Assert.AreEqual( 30f, back.Staff[1].ZoneRadius );
	}

	[TestMethod]
	public void V1SaveLoadsWithSaneProgressionDefaults()
	{
		// A v1 save predates the progression/staff fields; the missing keys must default to a freshly-built,
		// fully-reliable, un-queued ride (not "broken at level 0 with reliability 0").
		const string v1 = """
		{ "Version": 1, "Money": 5000, "EntryFee": 5,
		  "Placements": [ { "Kind": "ride", "Name": "totem", "TileX": 1, "TileY": 1, "TicketPrice": 6 } ] }
		""";

		var back = SaveGame.FromJson( v1 )!;
		var ride = back.Placements[0];

		Assert.AreEqual( 0, ride.UpgradeLevel );
		Assert.IsFalse( ride.Researching );
		Assert.AreEqual( -1, ride.ResearchQueuePos, "an absent queue position defaults to -1 (not queued)" );
		Assert.AreEqual( 1f, ride.Reliability, "an absent reliability defaults to fully reliable" );
		Assert.IsFalse( ride.Broken );
		Assert.AreEqual( 0, back.Staff.Count );
	}

	[TestMethod]
	public void SlotPathsAreDistinctAndClamped()
	{
		Assert.AreNotEqual( SaveGame.SlotPath( 1 ), SaveGame.SlotPath( 2 ) );
		Assert.AreEqual( SaveGame.SlotPath( 1 ), SaveGame.DefaultPath, "slot 1 is the default path (v1 compat)" );
		Assert.AreEqual( SaveGame.SlotPath( SaveGame.SlotCount ), SaveGame.SlotPath( 99 ), "out-of-range slots clamp to the last" );
		Assert.AreEqual( SaveGame.SlotPath( 1 ), SaveGame.SlotPath( 0 ), "out-of-range slots clamp to the first" );
	}
}
