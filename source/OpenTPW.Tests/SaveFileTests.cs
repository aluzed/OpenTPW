using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class SaveFileTests
{
	private static byte[] SamplePayload()
	{
		// Stand-in for the SAD_* module stream — opaque to the container reader/writer.
		var p = new byte[5000];
		for ( int i = 0; i < p.Length; i++ )
			p[i] = (byte)(i * 7 + 3);
		return p;
	}

	private static byte[] BuildSave( byte[] payload )
	{
		var legal = Encoding.ASCII.GetBytes( "(c) Bullfrog" );
		Array.Resize( ref legal, 0x500 );
		var hdr = new byte[0x100];
		for ( int i = 0; i < hdr.Length; i++ ) hdr[i] = (byte)i;
		return SaveReader.Build( SaveReader.CurrentVersion, SaveReader.OfflineFileType, 0, 0x85, legal, hdr, payload );
	}

	[TestMethod]
	public void ReadsContainerHeaderAndDecompressesPayload()
	{
		var payload = SamplePayload();
		using var stream = new MemoryStream( BuildSave( payload ) );
		var save = new SaveReader( stream );
		var got = save.ReadFile();

		Assert.AreEqual( SaveReader.CurrentVersion, save.Version );
		Assert.AreEqual( SaveReader.OfflineFileType, save.FileType );
		Assert.AreEqual( (byte)0x85, save.HeaderByte );
		Assert.IsTrue( payload.SequenceEqual( got ), "decompressed payload matches" );
	}

	[TestMethod]
	public void RoundTripsReadWriteRead()
	{
		var payload = SamplePayload();
		var first = new SaveReader( new MemoryStream( BuildSave( payload ) ) );
		first.ReadFile();

		var rewritten = first.Serialize();              // write the loaded save back out
		var second = new SaveReader( new MemoryStream( rewritten ) );
		var got = second.ReadFile();

		Assert.AreEqual( first.Version, second.Version );
		Assert.AreEqual( first.FileType, second.FileType );
		Assert.AreEqual( first.HeaderFlag, second.HeaderFlag );
		Assert.IsTrue( first.LegalText.SequenceEqual( second.LegalText ) );
		Assert.IsTrue( first.HeaderStruct.SequenceEqual( second.HeaderStruct ) );
		Assert.IsTrue( payload.SequenceEqual( got ), "payload survives a read→write→read round-trip" );
	}

	[TestMethod]
	public void RejectsFutureVersion()
	{
		var save = BuildSave( SamplePayload() );
		// Bump the leading LE version u32 past the current max.
		save[0] = 0xF5; save[1] = 0x01; // 0x1F5 = 501 > 500
		var reader = new SaveReader( new MemoryStream( save ) );
		Assert.ThrowsExactly<InvalidDataException>( () => reader.ReadFile() );
	}

	// Optional validation against a real save. Set TPW_SAVE_SAMPLE to a .TPWS file.
	[TestMethod]
	public void ReadsRealSaveSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_SAVE_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_SAVE_SAMPLE to a Theme Park World .TPWS file to run this test." );

		var save = new SaveReader( path );
		var payload = save.ReadFile();

		Assert.IsTrue( save.Version <= SaveReader.CurrentVersion, "version within range" );
		Assert.AreEqual( SaveReader.OfflineFileType, save.FileType, "offline fileType tag" );
		Assert.IsTrue( payload.Length > 0, "payload decompressed" );

		// The container must round-trip: re-serialising and re-reading yields the same payload.
		var got = new SaveReader( new MemoryStream( save.Serialize() ) ).ReadFile();
		Assert.IsTrue( payload.SequenceEqual( got ), "real save round-trips" );
	}
}
