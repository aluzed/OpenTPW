using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class LipSyncFileTests
{
	private static byte[] Lip( params uint[] values )
	{
		var data = new byte[(values.Length + 1) * 4];
		for ( var i = 0; i < values.Length; i++ )
			BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( i * 4, 4 ), values[i] );
		BinaryPrimitives.WriteUInt32LittleEndian( data.AsSpan( values.Length * 4, 4 ), 0xFFFFFFFF );
		return data;
	}

	[TestMethod]
	public void ReadsKeyframesUntilTerminator()
	{
		var bytes = Lip( 0, 1378095, 4497959, 5268752 );

		using var stream = new MemoryStream( bytes );
		var lip = new LipSyncFile( stream );

		CollectionAssert.AreEqual( new uint[] { 0, 1378095, 4497959, 5268752 }, lip.Keyframes );

		// Timestamps are microseconds: the last keyframe (5268752 µs) is ~5.27 s.
		Assert.AreEqual( TimeSpan.FromMicroseconds( 5268752 ), lip.Duration );
		Assert.AreEqual( 5.268752, lip.Duration.TotalSeconds, 1e-6 );
	}

	// Optional validation against a real .LIP. Set TPW_LIP_SAMPLE.
	[TestMethod]
	public void ParsesRealLipSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_LIP_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_LIP_SAMPLE to a Theme Park World .LIP file to run this test." );

		using var stream = File.OpenRead( path );
		var lip = new LipSyncFile( stream );

		Assert.IsTrue( lip.Keyframes.Count > 0, "lip-sync should have keyframes" );
		Assert.AreEqual( 0u, lip.Keyframes[0], "first keyframe is 0" );
		// Keyframe timestamps must be monotonically non-decreasing.
		for ( var i = 1; i < lip.Keyframes.Count; i++ )
			Assert.IsTrue( lip.Keyframes[i] >= lip.Keyframes[i - 1], $"keyframe {i} not monotonic" );

		// Microsecond unit: a speech clip's duration is a few-to-tens of seconds.
		Assert.IsTrue( lip.Duration > TimeSpan.Zero && lip.Duration < TimeSpan.FromMinutes( 5 ),
			$"duration {lip.Duration} should be a plausible speech length" );
	}
}
