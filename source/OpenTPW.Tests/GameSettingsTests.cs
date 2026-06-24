using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class GameSettingsTests
{
	// Persistent audio settings (T-051): JSON round-trips the three volume buses, clamps to [0,1], and
	// degrades to defaults on a missing / corrupt file rather than throwing.

	[TestMethod]
	public void SerializeRoundTripsAllVolumes()
	{
		var s = new GameSettings { MusicVolume = 0.3f, SfxVolume = 0.6f, SpeechVolume = 0.9f };
		var back = GameSettings.Deserialize( s.Serialize() );

		Assert.AreEqual( 0.3f, back.MusicVolume, 1e-4f );
		Assert.AreEqual( 0.6f, back.SfxVolume, 1e-4f );
		Assert.AreEqual( 0.9f, back.SpeechVolume, 1e-4f );
	}

	[TestMethod]
	public void OutOfRangeVolumesAreClamped()
	{
		var s = new GameSettings { MusicVolume = 5f, SfxVolume = -2f, SpeechVolume = 1.5f };
		var back = GameSettings.Deserialize( s.Serialize() );

		Assert.AreEqual( 1f, back.MusicVolume, 1e-4f );
		Assert.AreEqual( 0f, back.SfxVolume, 1e-4f );
		Assert.AreEqual( 1f, back.SpeechVolume, 1e-4f );
	}

	[TestMethod]
	public void GarbageJsonYieldsDefaults()
	{
		var d = GameSettings.Deserialize( "this is not json {{{" );

		// The built-in defaults (matching the Audio bus defaults).
		Assert.AreEqual( 0.5f, d.MusicVolume, 1e-4f );
		Assert.AreEqual( 0.8f, d.SfxVolume, 1e-4f );
		Assert.AreEqual( 0.9f, d.SpeechVolume, 1e-4f );
	}

	[TestMethod]
	public void NullJsonYieldsDefaults()
	{
		var d = GameSettings.Deserialize( "null" );
		Assert.AreEqual( 0.5f, d.MusicVolume, 1e-4f );
	}

	[TestMethod]
	public void PartialJsonKeepsDefaultsForMissingKeys()
	{
		// Only music is present; SFX/speech fall back to their defaults.
		var d = GameSettings.Deserialize( "{ \"MusicVolume\": 0.25 }" );
		Assert.AreEqual( 0.25f, d.MusicVolume, 1e-4f );
		Assert.AreEqual( 0.8f, d.SfxVolume, 1e-4f );
		Assert.AreEqual( 0.9f, d.SpeechVolume, 1e-4f );
	}

	[TestMethod]
	public void SaveThenLoadRoundTripsViaFile()
	{
		var original = GameSettings.FilePath;
		var dir = Path.Combine( Path.GetTempPath(), "opentpw-settings-test-" + System.Guid.NewGuid().ToString( "N" ) );
		try
		{
			GameSettings.FilePath = Path.Combine( dir, "settings.json" );

			new GameSettings { MusicVolume = 0.1f, SfxVolume = 0.2f, SpeechVolume = 0.3f }.Save();
			Assert.IsTrue( File.Exists( GameSettings.FilePath ), "Save should create the file (and its directory)" );

			var loaded = GameSettings.Load();
			Assert.AreEqual( 0.1f, loaded.MusicVolume, 1e-4f );
			Assert.AreEqual( 0.2f, loaded.SfxVolume, 1e-4f );
			Assert.AreEqual( 0.3f, loaded.SpeechVolume, 1e-4f );
		}
		finally
		{
			GameSettings.FilePath = original;
			if ( Directory.Exists( dir ) )
				Directory.Delete( dir, true );
		}
	}

	[TestMethod]
	public void LoadOfMissingFileYieldsDefaults()
	{
		var original = GameSettings.FilePath;
		try
		{
			GameSettings.FilePath = Path.Combine( Path.GetTempPath(), "opentpw-no-such-" + System.Guid.NewGuid().ToString( "N" ), "settings.json" );
			var loaded = GameSettings.Load();
			Assert.AreEqual( 0.5f, loaded.MusicVolume, 1e-4f );
			Assert.AreEqual( 0.8f, loaded.SfxVolume, 1e-4f );
		}
		finally
		{
			GameSettings.FilePath = original;
		}
	}
}
