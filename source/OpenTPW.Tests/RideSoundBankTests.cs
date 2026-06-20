using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace OpenTPW.Tests;

// Verifies the ride sound registry's global-index mapping against the real game banks (gated on data).
[TestClass]
public class RideSoundBankTests
{
	private static string DataRoot()
	{
		var gp = Environment.GetEnvironmentVariable( "OPENTPW_GAMEPATH" ) ?? "/var/tmp/tpw_game";
		return Path.Combine( gp, "data" );
	}

	[TestMethod]
	public void ResolvesGlobalSoundIdsThroughConcatenatedBanks()
	{
		Log = new();
		var data = DataRoot();
		var catalog = Path.Combine( data, "global", "sound", "cat_ridesBANK.map" );
		if ( !File.Exists( catalog ) )
			Assert.Inconclusive( "Set OPENTPW_GAMEPATH to a Theme Park World install to run this test." );

		// GameDir.GamePath drives the bank paths; point it at the install for the duration of the test.
		var prev = Environment.GetEnvironmentVariable( "OPENTPW_GAMEPATH" );
		Environment.SetEnvironmentVariable( "OPENTPW_GAMEPATH", Path.GetDirectoryName( data ) );
		try
		{
			var bank = RideSoundBank.FromBankCatalog( catalog );
			Assert.IsNotNull( bank, "cat_ridesBANK should build a registry" );

			// RE'd: a ride sound id is a global index across the BANK list concatenated in catalog order
			// (RideHD 0-69, sfUiHD 70-89, StaffHD 90-185, KidsHD 186-…). Verified mappings:
			Assert.AreEqual( "nl_creak_2.mp2", bank!.Resolve( 14 )?.Name );   // RideHD
			Assert.AreEqual( "urinal.mp2", bank.Resolve( 18 )?.Name );        // RideHD
			Assert.AreEqual( "mortar_3.mp2", bank.Resolve( 69 )?.Name );      // last RideHD
			Assert.AreEqual( "hamer002.mp2", bank.Resolve( 104 )?.Name );     // StaffHD (90 + 14)
			Assert.IsNull( bank.Resolve( -1 ), "negative id resolves to nothing" );
		}
		finally
		{
			Environment.SetEnvironmentVariable( "OPENTPW_GAMEPATH", prev );
		}
	}
}
