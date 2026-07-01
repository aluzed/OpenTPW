using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class StaffNamesTests
{
	// STAFF_TYPES.str as decoded from the game (singular/plural pairs).
	private static readonly string[] Types =
		{ "Cleaner", "Cleaners", "Mechanic", "Mechanics", "Entertainer", "Entertainers", "Guard", "Guards", "Scientist", "Scientists" };

	[TestMethod]
	public void MapsRolesToTheirAuthenticSingularNames()
	{
		Assert.AreEqual( "Cleaner", StaffNames.Map( StaffRole.Handyman, Types ), "a Handyman is a Cleaner in TPW" );
		Assert.AreEqual( "Scientist", StaffNames.Map( StaffRole.Researcher, Types ), "a Researcher is a Scientist" );
		Assert.AreEqual( "Mechanic", StaffNames.Map( StaffRole.Mechanic, Types ) );
		Assert.AreEqual( "Entertainer", StaffNames.Map( StaffRole.Entertainer, Types ) );
		Assert.AreEqual( "Guard", StaffNames.Map( StaffRole.Guard, Types ) );
	}

	[TestMethod]
	public void FallsBackToTheEnumNameWhenStringsMissing()
	{
		Assert.AreEqual( "Handyman", StaffNames.Map( StaffRole.Handyman, System.Array.Empty<string>() ) );
		Assert.AreEqual( "Guard", StaffNames.Map( StaffRole.Guard, new[] { "Cleaner", "Cleaners" } ), "index past the list → enum name" );
	}
}
