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

		// pIQT payload bytes are 1,2,3,4,... so width=0x0201, height=0x0403.
		var info = video.GetVideoInfo();
		Assert.AreEqual( 0x0201, info.Width );
		Assert.AreEqual( 0x0403, info.Height );

		// Payload of the first video frame is the bytes 1..16.
		var frame = video.Chunks[1];
		Assert.AreEqual( "pIQT", frame.Type );
		var payload = video.GetPayload( frame );
		Assert.AreEqual( 16, payload.Length );
		Assert.AreEqual( 1, payload[0] );
		Assert.AreEqual( 16, payload[15] );
	}

	private static byte[] ChunkWith( string fourcc, byte[] payload )
	{
		var size = 8 + payload.Length;
		var b = new byte[size];
		Encoding.ASCII.GetBytes( fourcc ).CopyTo( b, 0 );
		BinaryPrimitives.WriteInt32LittleEndian( b.AsSpan( 4, 4 ), size );
		payload.CopyTo( b, 8 );
		return b;
	}

	// Mono EA-ADPCM path: a synthesised single-channel SCHl/SCDl decodes to the header's sample count
	// (two samples per data byte). No real mono movie ships in the install, so this validates the
	// channel dispatch + block accounting; the codec math is the proven stereo math applied per-nibble.
	[TestMethod]
	public void DecodesMonoEaAdpcm()
	{
		const int samples = 56; // one full sub-block: 28 data bytes × 2 samples

		var sch = new System.Collections.Generic.List<byte>();
		sch.AddRange( Encoding.ASCII.GetBytes( "PT\0\0" ) );
		sch.AddRange( new byte[] { 0x82, 0x01, 0x01 } );       // channels = 1
		sch.AddRange( new byte[] { 0x85, 0x02, 0x00, 0x38 } ); // sampleCount = 56 (big-endian)
		sch.Add( 0xFF );

		var scd = new System.Collections.Generic.List<byte>();
		scd.AddRange( new byte[] { 56, 0, 0, 0 } ); // count = 56 (LE int32)
		scd.AddRange( new byte[] { 0, 0, 0, 0 } );  // prev, cur history (two int16 = 0)
		scd.Add( 0x0C );                            // coeff index 0, shift = 20 - 12 = 8
		for ( var i = 0; i < 28; i++ ) scd.Add( 0x10 ); // 28 data bytes → 56 samples

		var bytes = ChunkWith( "SCHl", sch.ToArray() )
			.Concat( ChunkWith( "SCDl", scd.ToArray() ) )
			.Concat( Chunk( "SCEl", 0 ) )
			.ToArray();

		var video = new VideoFile( new MemoryStream( bytes ) );
		var audio = video.DecodeAudio();

		Assert.AreEqual( 1, audio.Channels );
		Assert.AreEqual( samples, audio.SampleCount, "decodes to the header's sample count" );
		Assert.AreEqual( samples, audio.Samples.Length, "mono output is not interleaved" );
		// 0x10 → nibbles (1,0): sample 0 = (1<<8 + 0x80)>>8 = 1, sample 1 = (0 + 0x80)>>8 = 0.
		Assert.AreEqual( 1, audio.Samples[0] );
		Assert.AreEqual( 0, audio.Samples[1] );
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

		// Frame dimensions from the pIQT header (a real BF.TGQ is 320x352 per ffprobe).
		var vinfo = video.GetVideoInfo();
		Assert.IsTrue( vinfo.Width > 0 && vinfo.Height > 0, "frame should have real dimensions" );

		// Decode the EA-ADPCM audio and sanity-check it (verified: count matches header).
		var audio = video.DecodeAudio();
		Assert.AreEqual( 2, audio.Channels );
		Assert.IsTrue( audio.SampleCount > 0, "should decode audio samples" );
		Assert.AreEqual( audio.SampleCount * audio.Channels, audio.Samples.Length );
		Assert.IsTrue( audio.Samples.Any( s => s > 1000 ) && audio.Samples.Any( s => s < -1000 ),
			"decoded audio should have real dynamic range, not silence/noise floor" );
	}
}
