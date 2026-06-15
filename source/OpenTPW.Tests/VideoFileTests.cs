using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class VideoFileTests
{
	private static byte[] Chunk( string fourcc, int payloadLength )
	{
		var size = 8 + payloadLength;
		var b = new byte[size];
		Encoding.ASCII.GetBytes( fourcc ).CopyTo( b, 0 );
		BinaryPrimitives.WriteInt32LittleEndian( b.AsSpan( 4, 4 ), size );
		for ( var i = 0; i < payloadLength; i++ )
			b[8 + i] = (byte)(i + 1);
		return b;
	}

	// Parses a synthesized EA-multimedia container (no copyrighted sample needed).
	[TestMethod]
	public void ParsesEaChunkContainer()
	{
		var bytes = Chunk( "SCHl", 8 )      // audio header
			.Concat( Chunk( "pIQT", 16 ) )  // video frame 1
			.Concat( Chunk( "pIQT", 4 ) )   // video frame 2
			.Concat( Chunk( "SCEl", 0 ) )   // audio end
			.ToArray();

		using var stream = new MemoryStream( bytes );
		var video = new VideoFile( stream );

		Assert.AreEqual( 4, video.Chunks.Count );
		Assert.AreEqual( 2, video.VideoFrameCount );
		Assert.AreEqual( 2, video.VideoChunks.Count() );
		Assert.AreEqual( 2, video.AudioChunks.Count() ); // SCHl + SCEl
		Assert.IsTrue( video.HasAudio );

		Assert.AreEqual( "SCHl", video.Chunks[0].Type );
		Assert.AreEqual( 8, video.Chunks[0].PayloadLength );

		// Payload of the first video frame is the bytes 1..16.
		var frame = video.Chunks[1];
		Assert.AreEqual( "pIQT", frame.Type );
		var payload = video.GetPayload( frame );
		Assert.AreEqual( 16, payload.Length );
		Assert.AreEqual( 1, payload[0] );
		Assert.AreEqual( 16, payload[15] );
	}

	// A truncated/garbage tail must not loop forever or overrun — parse stops cleanly.
	[TestMethod]
	public void StopsOnMalformedChunk()
	{
		Log = new(); // the malformed branch logs a warning

		var bytes = Chunk( "pIQT", 8 )
			.Concat( new byte[] { (byte)'B', (byte)'A', (byte)'D', (byte)'!', 0x02, 0x00, 0x00, 0x00 } ) // size 2 < preamble
			.ToArray();

		using var stream = new MemoryStream( bytes );
		var video = new VideoFile( stream );

		Assert.AreEqual( 1, video.Chunks.Count );
		Assert.AreEqual( "pIQT", video.Chunks[0].Type );
	}

	// Optional validation against a real movie. Set TPW_VIDEO_SAMPLE to a .TGQ path to run;
	// Inconclusive otherwise (real movies are copyrighted and not committed).
	[TestMethod]
	public void ParsesRealMovieSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_VIDEO_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_VIDEO_SAMPLE to a Theme Park World .TGQ file to run this test." );

		// Use the stream ctor: the path ctor reads through the game's virtual file system.
		using var stream = File.OpenRead( path );
		var video = new VideoFile( stream );

		Assert.IsTrue( video.VideoFrameCount > 0, "a movie should contain pIQT video frames" );
		Assert.IsTrue( video.HasAudio, "TPW movies contain EA audio chunks" );
		// Every byte must be accounted for by the chunk walk (clean EOF).
		var consumed = video.Chunks.Sum( c => (long)c.Size );
		Assert.AreEqual( new FileInfo( path ).Length, consumed, "chunks should tile the whole file" );

		// Decode the EA-ADPCM audio and sanity-check it (verified: count matches header).
		var audio = video.DecodeAudio();
		Assert.AreEqual( 2, audio.Channels );
		Assert.IsTrue( audio.SampleCount > 0, "should decode audio samples" );
		Assert.AreEqual( audio.SampleCount * audio.Channels, audio.Samples.Length );
		Assert.IsTrue( audio.Samples.Any( s => s > 1000 ) && audio.Samples.Any( s => s < -1000 ),
			"decoded audio should have real dynamic range, not silence/noise floor" );
	}
}
