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
	}
}
