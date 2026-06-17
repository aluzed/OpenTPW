using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using SVector3 = System.Numerics.Vector3;   // OpenTPW.Vector3 (global alias) shadows the bare name

namespace OpenTPW.Tests;

[TestClass]
public class RideKeyframeFileTests
{
	// Builds a minimal keyframe file matching the reverse-engineered layout (docs/08): a header with
	// the magic and a 0x98 anim pointer, a trailer (count + record array), one surface record with a
	// rotation track, and a rotation track of (0xFFFF-tagged time, (w,x,y,z) quaternion) keys.
	private static byte[] BuildFrame( int surfaceIndex, (int time, Quaternion q)[] rotKeys )
	{
		var d = new byte[0x400];
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( 0, 4 ), 0x1CD15D46 ); // magic

		int trailer = 0x100;
		int records = 0x140;
		int rotTrack = 0x200;

		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( 0x98, 4 ), (uint)trailer );    // anim pointer
		BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( trailer + 0x12, 2 ), 1 );      // record count
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( trailer + 0x2c, 4 ), (uint)records ); // record array

		// One surface record (0x40 bytes).
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( records + 0x04, 4 ), RideKeyframeFile.FlagRotation ); // flags
		BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( records + 0x10, 2 ), (ushort)surfaceIndex );          // surface index
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( records + 0x1c, 4 ), (uint)rotTrack );                // rotation track offset

		// Rotation track: stride 20 = [0xFFFF|time][4 floats w,x,y,z].
		int o = rotTrack;
		foreach ( var (time, q) in rotKeys )
		{
			BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), 0xFFFF0000u | (uint)time );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 4, 4 ), q.W );  // stored (w, x, y, z)
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 8, 4 ), q.X );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 12, 4 ), q.Y );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 16, 4 ), q.Z );
			o += 20;
		}
		return d;
	}

	// Builds a frame with one surface carrying a rotation track (0xFFFF marker) and a contiguous scale
	// track (0x0000 marker) — exercises the per-track marker + gap-bounding (a scale key time can
	// exceed the next track's start, which must not be misread as extra keys).
	private static byte[] BuildFrameRotAndScale( (int time, Quaternion q)[] rot, (int time, SVector3 s)[] scale )
	{
		var d = new byte[0x600];
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( 0, 4 ), 0x1CD15D46 );
		int trailer = 0x100, records = 0x140, rotTrack = 0x200, scaleTrack = 0x300;
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( 0x98, 4 ), (uint)trailer );
		BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( trailer + 0x12, 2 ), 1 );
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( trailer + 0x2c, 4 ), (uint)records );
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( records + 0x04, 4 ), RideKeyframeFile.FlagRotation | RideKeyframeFile.FlagScale );
		BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( records + 0x10, 2 ), 5 );
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( records + 0x1c, 4 ), (uint)rotTrack );
		BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( records + 0x20, 4 ), (uint)scaleTrack );

		int o = rotTrack;
		foreach ( var (time, q) in rot )
		{
			BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), 0xFFFF0000u | (uint)time );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 4, 4 ), q.W );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 8, 4 ), q.X );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 12, 4 ), q.Y );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 16, 4 ), q.Z );
			o += 20;
		}
		o = scaleTrack;
		foreach ( var (time, s) in scale )
		{
			BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), (uint)time );   // 0x0000 marker in high u16
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 4, 4 ), s.X );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 8, 4 ), s.Y );
			BinaryPrimitives.WriteSingleLittleEndian( d.AsSpan( o + 12, 4 ), s.Z );
			o += 16;
		}
		return d;
	}

	[TestMethod]
	public void ParsesScaleTrackAndComposesWithRotation()
	{
		// Like space_bouncy: a "grow-in" scale 0.1 -> 1 alongside a rotation track.
		var rot = new[] { (0, RotZ( 0 )), (10, RotZ( 90 )) };
		var scale = new[] { (0, new SVector3( 0.1f )), (10, SVector3.One ) };
		var kf = new RideKeyframeFile( BuildFrameRotAndScale( rot, scale ) );

		var s = kf.Surfaces.Single();
		Assert.AreEqual( 2, s.Rotation.Count );
		Assert.AreEqual( 2, s.Scale.Count, "scale track (0x0000 marker) should parse" );
		Assert.AreEqual( 0.1f, s.Scale[0].Value.X, 1e-4f );

		// Midpoint scale lerps to ~0.55.
		var mid = RideKeyframeFile.SampleVector( s.Scale, 5f, SVector3.One );
		Assert.AreEqual( 0.55f, mid.X, 1e-3f );
	}

	private static Quaternion RotZ( float deg ) =>
		Quaternion.CreateFromAxisAngle( SVector3.UnitZ, deg * MathF.PI / 180f );

	[TestMethod]
	public void ParsesRotationTrackKeysAndTimes()
	{
		// A full 360° turn about Z, like monkeym1's m_arm (surface 5): t=0..40.
		var keys = new[] { (0, RotZ( 0 )), (10, RotZ( 90 )), (20, RotZ( 180 )), (30, RotZ( 270 )), (40, RotZ( 360 )) };
		var kf = new RideKeyframeFile( BuildFrame( 5, keys ) );

		Assert.AreEqual( 1, kf.Surfaces.Count );
		var s = kf.Surfaces[0];
		Assert.AreEqual( 5, s.SurfaceIndex );
		Assert.AreEqual( 5, s.Rotation.Count, "should read exactly 5 keys (sentinel + monotonic time)" );
		Assert.AreEqual( 40f, kf.Duration );
		Assert.AreEqual( 0f, s.Rotation[0].Time );
		Assert.AreEqual( 20f, s.Rotation[2].Time );
	}

	[TestMethod]
	public void StopsAtTimeWrap()
	{
		// Two concatenated tracks (the second wraps back to t=0) — only the first should be read.
		var keys = new[] { (0, RotZ( 0 )), (10, RotZ( 90 )), (0, RotZ( 0 )), (10, RotZ( 90 )) };
		var kf = new RideKeyframeFile( BuildFrame( 5, keys ) );
		Assert.AreEqual( 2, kf.Surfaces[0].Rotation.Count, "must stop when the keyframe time decreases" );
	}

	[TestMethod]
	public void SampleRotationInterpolatesAndClamps()
	{
		var keys = new (float, Quaternion)[] { (0f, RotZ( 0 )), (10f, RotZ( 90 )) };

		// Clamp below/above the range.
		AssertQuat( RotZ( 0 ), RideKeyframeFile.SampleRotation( keys, -5f ) );
		AssertQuat( RotZ( 90 ), RideKeyframeFile.SampleRotation( keys, 99f ) );

		// Midpoint slerps to ~45°.
		AssertQuat( RotZ( 45 ), RideKeyframeFile.SampleRotation( keys, 5f ) );
	}

	[TestMethod]
	public void EmptyOrBadDataYieldsNoAnimation()
	{
		Assert.AreEqual( 0, new RideKeyframeFile( new byte[0x20] ).Surfaces.Count );      // too short / no magic
		var noAnim = new byte[0x100];
		BinaryPrimitives.WriteUInt32LittleEndian( noAnim.AsSpan( 0, 4 ), 0x1CD15D46 );     // magic but anim ptr 0 (base model)
		Assert.AreEqual( 0, new RideKeyframeFile( noAnim ).Surfaces.Count );
	}

	// Optional validation against a real keyframe file (e.g. monkeym1.MD2 extracted from monkey.wad).
	// Set TPW_KEYFRAME_SAMPLE to that file to run.
	[TestMethod]
	public void ParsesRealKeyframeSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_KEYFRAME_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_KEYFRAME_SAMPLE to a ride keyframe .MD2 (e.g. monkeym1.MD2) to run this test." );

		var kf = new RideKeyframeFile( File.ReadAllBytes( path ) );
		Assert.IsTrue( kf.Surfaces.Count > 0, "real keyframe file should have animated surfaces" );

		foreach ( var s in kf.Surfaces )
		{
			// Each track's times must be strictly increasing, and rotation values must be unit quaternions.
			for ( int i = 1; i < s.Rotation.Count; i++ )
				Assert.IsTrue( s.Rotation[i].Time > s.Rotation[i - 1].Time, "keyframe times strictly increase" );
			foreach ( var (_, q) in s.Rotation )
				Assert.IsTrue( MathF.Abs( q.Length() - 1f ) < 1e-3f, $"rotation key should be a unit quaternion, got |q|={q.Length()}" );
		}
		Assert.IsTrue( kf.Duration > 0, "duration should be positive" );
	}

	private static void AssertQuat( Quaternion expected, Quaternion actual )
	{
		// Quaternions q and -q are the same rotation; compare via dot magnitude.
		float dot = MathF.Abs( Quaternion.Dot( Quaternion.Normalize( expected ), Quaternion.Normalize( actual ) ) );
		Assert.IsTrue( dot > 0.999f, $"expected {expected}, got {actual} (|dot|={dot})" );
	}
}
