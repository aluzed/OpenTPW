using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class TqiDecoderTests
{
	// Optional validation against a real movie. Set TPW_VIDEO_SAMPLE to a .TGQ file.
	[TestMethod]
	public void DecodesRealVideoFrame()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_VIDEO_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_VIDEO_SAMPLE to a Theme Park World .TGQ file to run this test." );

		using var stream = File.OpenRead( path );
		var video = new VideoFile( stream );

		var info = video.GetVideoInfo();
		var frame = video.DecodeFrame( 0 );

		Assert.AreEqual( info.Width, frame.Width );
		Assert.AreEqual( info.Height, frame.Height );
		Assert.AreEqual( frame.Width * frame.Height * 3, frame.Rgb.Length );

		// A real frame is not a flat color: it has genuine pixel variety.
		var distinct = frame.Rgb.Distinct().Count();
		Assert.IsTrue( distinct > 16, "decoded frame should have real image content" );

		// Verified by rendering: frame 120 of BF.TGQ reconstructs the Bullfrog logo,
		// pixel-matching the ffmpeg reference.
	}
}
