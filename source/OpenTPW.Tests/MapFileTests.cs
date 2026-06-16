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

		Assert.AreEqual( 2, map.EntryCount );
		CollectionAssert.AreEqual( names, map.Entries.ToArray() );
	}

	// The SFX variant has no name table: Entries stays empty (no false positives).
	[TestMethod]
	public void SfxVariantHasNoEntryNames()
	{
		var guid = new Guid( "e9612c00-31d0-11d2-b409-00b0c993f203" );

		using var ms = new MemoryStream();
		ms.Write( guid.ToByteArray() );
		ms.Write( new byte[8] );
		ms.Write( BitConverter.GetBytes( 1 ) );   // count = 1
		ms.Write( new byte[] { 0x19, 0, 0, 0, 1, 0, 0, 0, 0, 0 } ); // non-string binary body

		ms.Position = 0;
		var map = new MapFile( ms );

		Assert.AreEqual( 0, map.Entries.Count );
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

		// A BANK catalog exposes its entry names; every decoded name must be non-empty.
		if ( map.Entries.Count > 0 )
		{
			Assert.AreEqual( map.EntryCount, map.Entries.Count );
			Assert.IsTrue( map.Entries.All( n => n.Length > 0 ) );
		}
	}
}
