using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class MapFileTests
{
	[TestMethod]
	public void ReadsCategoryGuidAndData()
	{
		var guid = new Guid( "e9612c00-31d0-11d2-b409-00b0c993f203" );
		var payload = new byte[] { 1, 2, 3, 4 };
		var bytes = guid.ToByteArray().Concat( payload ).ToArray();

		using var stream = new MemoryStream( bytes );
		var map = new MapFile( stream );

		Assert.AreEqual( guid, map.CategoryType );
		CollectionAssert.AreEqual( payload, map.Data );
	}

	[TestMethod]
	public void RejectsTooSmall()
	{
		using var stream = new MemoryStream( new byte[8] ); // < 16-byte GUID
		Assert.ThrowsExactly<InvalidDataException>( () => new MapFile( stream ) );
	}

	// BANK catalogs end with `count` length-prefixed ASCII names, preceded by `count`
	// fixed 11-byte records. The reader locates and decodes that trailing name table.
	[TestMethod]
	public void DecodesBankEntryNames()
	{
		var guid = new Guid( "e9612c01-31d0-11d2-b409-00a0c993f203" );
		var names = new[] { "Sound\\Kids", "Sound\\UI" };

		using var ms = new MemoryStream();
		ms.Write( guid.ToByteArray() );          // 16
		ms.Write( new byte[8] );                 // reserved
		ms.Write( BitConverter.GetBytes( names.Length ) ); // count @0x18
		ms.Write( new byte[11 * names.Length] ); // one 11-byte record per entry
		foreach ( var n in names )
		{
			var raw = System.Text.Encoding.ASCII.GetBytes( n + "\0" );
			ms.Write( BitConverter.GetBytes( raw.Length ) );
			ms.Write( raw );
		}

		ms.Position = 0;
		var map = new MapFile( ms );

		Assert.AreEqual( MapVariant.Bank, map.Variant );
		Assert.AreEqual( 2, map.EntryCount );
		CollectionAssert.AreEqual( names, map.Entries.ToArray() );
	}

	// The SFX variant has a category header (sound count + 3 default params), no name table.
	[TestMethod]
	public void DecodesSfxCategoryHeader()
	{
		var guid = new Guid( "e9612c00-31d0-11d2-b409-00b0c993f203" );

		using var ms = new MemoryStream();
		ms.Write( guid.ToByteArray() );                  // 16
		ms.Write( new byte[8] );                         // reserved @0x10
		ms.Write( BitConverter.GetBytes( 1 ) );          // categoryCount @0x18 = 1
		ms.Write( BitConverter.GetBytes( 2 ) );          // soundEntryCount @0x1c = 2
		ms.Write( BitConverter.GetBytes( 0 ) );          // pad @0x20
		ms.Write( BitConverter.GetBytes( 0x10009 ) );    // flags @0x24
		ms.Write( BitConverter.GetBytes( 1.0f ) );       // params @0x28
		ms.Write( BitConverter.GetBytes( 2.0f ) );
		ms.Write( BitConverter.GetBytes( 0.5f ) );
		// Two 20-byte per-sound records @0x34: (soundId, variations, reserved, param, flags).
		void Record( int id, int vars, int param, int flags )
		{
			ms.Write( BitConverter.GetBytes( id ) );
			ms.Write( BitConverter.GetBytes( vars ) );
			ms.Write( BitConverter.GetBytes( 0 ) );
			ms.Write( BitConverter.GetBytes( param ) );
			ms.Write( BitConverter.GetBytes( flags ) );
		}
		Record( 18, 1, 3200, 0 );
		Record( 14, 3, 3200, 6 );
		ms.Write( new byte[] { 0xAA, 0xBB } );           // start of the (raw) trailing blob

		ms.Position = 0;
		var map = new MapFile( ms );

		Assert.AreEqual( MapVariant.Sfx, map.Variant );
		Assert.AreEqual( 2, map.SoundEntryCount );
		Assert.AreEqual( 0, map.Entries.Count, "SFX has no name table" );
		CollectionAssert.AreEqual( new[] { 1.0f, 2.0f, 0.5f }, map.CategoryParameters.ToArray() );
		Assert.AreEqual( 2, map.SoundEntries.Count );
		Assert.AreEqual( new MapSoundEntry( 18, 1, 3200, 0 ), map.SoundEntries[0] );
		Assert.AreEqual( new MapSoundEntry( 14, 3, 3200, 6 ), map.SoundEntries[1] );
	}

	// Optional validation against a real .MAP (CAT_*) file. Set TPW_MAP_SAMPLE.
	[TestMethod]
	public void ParsesRealMapSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_MAP_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_MAP_SAMPLE to a Theme Park World .MAP (CAT_*) file to run this test." );

		using var stream = File.OpenRead( path );
		var map = new MapFile( stream );

		Assert.AreNotEqual( Guid.Empty, map.CategoryType );
		// TPW audio category catalogs share the DirectMusic-family GUID shape.
		StringAssert.Contains( map.CategoryType.ToString(), "-31d0-11d2-b409-" );
		Assert.IsTrue( map.Data.Length > 0 );

		// A BANK catalog exposes its entry names; an SFX catalog exposes its sound-entry
		// count and three category default parameters.
		if ( map.Variant == MapVariant.Bank )
		{
			Assert.AreEqual( map.EntryCount, map.Entries.Count );
			Assert.IsTrue( map.Entries.All( n => n.Length > 0 ) );
		}
		else if ( map.Variant == MapVariant.Sfx )
		{
			Assert.IsTrue( map.SoundEntryCount > 0, "SFX should have sound entries" );
			Assert.AreEqual( 3, map.CategoryParameters.Count );
			// The 20-byte per-sound record table decodes exactly soundEntryCount entries.
			Assert.AreEqual( map.SoundEntryCount, map.SoundEntries.Count );
			Assert.IsTrue( map.SoundEntries.All( e => e.VariationCount >= 1 ), "each sound has >= 1 sample" );
		}
	}
}
