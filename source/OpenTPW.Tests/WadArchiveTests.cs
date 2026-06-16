using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class WadArchiveTests
{
	// Builds a minimal valid DWFB archive in memory (one uncompressed file) so the
	// WadArchive reader — which the WadTool relies on — has direct test coverage.
	private static byte[] BuildSingleFileWad( string name, string content )
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true );

		var nameBytes = Encoding.ASCII.GetBytes( name + "\0" );
		var dataBytes = Encoding.ASCII.GetBytes( content );

		const int headerSize = 88;     // magic+version+64 pad+count+listOff+listLen+null
		const int entrySize = 40;
		var nameOffset = headerSize + entrySize;
		var dataOffset = nameOffset + nameBytes.Length;

		// Header
		w.Write( Encoding.ASCII.GetBytes( "DWFB" ) );
		w.Write( 1 );                  // version
		w.Write( new byte[64] );       // padding
		w.Write( 1 );                  // file count
		w.Write( headerSize );         // file list offset
		w.Write( entrySize );          // file list length
		w.Write( 0 );                  // null

		// File entry (40 bytes)
		w.Write( 0 );                  // unused
		w.Write( nameOffset );         // filename offset
		w.Write( nameBytes.Length );   // filename length (incl. null terminator)
		w.Write( dataOffset );         // data offset
		w.Write( dataBytes.Length );   // data length
		w.Write( 0 );                  // compression type (0 = uncompressed; 4 = refpack)
		w.Write( dataBytes.Length );   // decompressed size
		w.Write( new byte[12] );       // null

		w.Write( nameBytes );          // name string
		w.Write( dataBytes );          // file data

		w.Flush();
		return ms.ToArray();
	}

	[TestMethod]
	public void ReadsSingleFileArchive()
	{
		var bytes = BuildSingleFileWad( "test.txt", "hello" );

		using var archive = new WadArchive( new MemoryStream( bytes ) );

		var files = archive.GetFiles( "" );
		CollectionAssert.Contains( files, "test.txt" );

		using var stream = archive.OpenFile( "test.txt" );
		using var reader = new StreamReader( stream! );
		Assert.AreEqual( "hello", reader.ReadToEnd() );
	}
}
