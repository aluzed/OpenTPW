using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

[TestClass]
public class ParticleLibraryFileTests
{
	// Mirrors the real layout (Ghidra-confirmed): 8-byte header (count, recordSize), then fixed effect
	// records = [param | 64-byte ramp | 40-byte name], then a shared table (count2, size2, records) and
	// two globals (density, totalParticles).
	private const int RampBytes = 64;
	private const int NameBytes = 40;
	private const int ParamLen = 8;
	private const int RecordSize = ParamLen + RampBytes + NameBytes; // 112

	private static byte[] BuildPlb()
	{
		const int count = 2;
		const int count2 = 2, size2 = 4;

		int recordsEnd = 8 + count * RecordSize;
		int sharedEnd = recordsEnd + 8 + count2 * size2;
		var buf = new byte[sharedEnd + 8]; // + two globals

		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), count );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), RecordSize );

		// Record 0: a recognisable colour ramp + name "NULL". Stop k = D3DCOLOR with R=k,G=2k,B=3k,A=4k.
		int rec0 = 8, ramp0 = rec0 + ParamLen, name0 = ramp0 + RampBytes;
		for ( var k = 0; k < 16; k++ )
		{
			uint argb = (uint)((4 * k) << 24 | (k) << 16 | (2 * k) << 8 | (3 * k));
			BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( ramp0 + k * 4, 4 ), argb );
		}
		Encoding.ASCII.GetBytes( "NULL" ).CopyTo( buf, name0 );

		// Record 1: name "Sparks".
		int rec1 = 8 + RecordSize;
		Encoding.ASCII.GetBytes( "Sparks" ).CopyTo( buf, rec1 + ParamLen + RampBytes );

		// Shared table: count2 records of size2 bytes.
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( recordsEnd, 4 ), count2 );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( recordsEnd + 4, 4 ), size2 );
		buf[recordsEnd + 8] = 0xDE; // a marker byte in shared record 0

		// Globals.
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( sharedEnd, 4 ), 33 );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( sharedEnd + 4, 4 ), 1024 );
		return buf;
	}

	[TestMethod]
	public void ParsesHeaderRecordsAndNames()
	{
		using var stream = new MemoryStream( BuildPlb() );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( RecordSize, plb.RecordSize );
		Assert.AreEqual( 2, plb.Effects.Count );

		Assert.AreEqual( "NULL", plb.Effects[0].Name );
		Assert.AreEqual( 0, plb.Effects[0].Index );
		Assert.AreEqual( ParamLen, plb.Effects[0].Parameters.Length );

		// Colour ramp decoded from the D3DCOLOR words (R=k, G=2k, B=3k, A=4k).
		var ramp = plb.Effects[0].ColorRamp;
		Assert.AreEqual( 16, ramp.Count );
		Assert.AreEqual( new ParticleColor( 0, 0, 0, 0 ), ramp[0] );
		Assert.AreEqual( new ParticleColor( 5, 10, 15, 20 ), ramp[5] );

		Assert.AreEqual( "Sparks", plb.Effects[1].Name );
	}

	[TestMethod]
	public void ParsesSharedTableAndGlobals()
	{
		using var stream = new MemoryStream( BuildPlb() );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( 4, plb.SharedRecordSize );
		Assert.AreEqual( 2, plb.SharedRecords.Count );
		Assert.AreEqual( 0xDE, plb.SharedRecords[0][0] );
		Assert.AreEqual( 33, plb.ParticleDensity );
		Assert.AreEqual( 1024, plb.TotalParticles );
		Assert.AreEqual( 0, plb.TrailingData.Length );
	}

	[TestMethod]
	public void RejectsTruncatedRecords()
	{
		// Header claims 10 records of 320 bytes, but the file is far too small.
		var buf = new byte[8];
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), 10 );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), 320 );

		using var stream = new MemoryStream( buf );
		Assert.ThrowsExactly<InvalidDataException>( () => new ParticleLibraryFile( stream ) );
	}

	// Optional validation against a real .PLB. Set TPW_PLB_SAMPLE (e.g. Data/Particle/Tp2.plb).
	[TestMethod]
	public void ParsesRealPlbSample()
	{
		var path = Environment.GetEnvironmentVariable( "TPW_PLB_SAMPLE" );
		if ( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
			Assert.Inconclusive( "Set TPW_PLB_SAMPLE to a Theme Park World .PLB file to run this test." );

		using var stream = File.OpenRead( path );
		var plb = new ParticleLibraryFile( stream );

		Assert.AreEqual( 320, plb.RecordSize, "observed record size on Tp2.plb" );
		Assert.IsTrue( plb.Effects.Count >= 101, "should expose the full effect catalogue" );

		// Names must line up with par_lib.h's P_EFFECT_* order.
		Assert.AreEqual( "NULL", plb.Effects[0].Name );
		Assert.AreEqual( "Sparks", plb.Effects[1].Name );
		Assert.AreEqual( "Smoke", plb.Effects[2].Name );
		Assert.AreEqual( "Test2D", plb.Effects[100].Name );

		// Every effect carries a 16-stop colour ramp.
		Assert.AreEqual( 16, plb.Effects[1].ColorRamp.Count );

		// Smoke is white with an alpha that fades in from 0 — a sanity check that the
		// channel order (D3DCOLOR ARGB) is decoded correctly.
		var smoke = plb.Effects[2].ColorRamp;
		Assert.AreEqual( new ParticleColor( 255, 255, 255, 0 ), smoke[0] );
		Assert.IsTrue( smoke[8].A > smoke[0].A, "smoke alpha should ramp up from 0" );
		Assert.IsTrue( smoke.All( c => c is { R: 255, G: 255, B: 255 } ), "smoke ramp is white" );

		// The shared table + globals after the records (Ghidra: 20 records of 104 bytes, then density+total).
		Assert.AreEqual( 104, plb.SharedRecordSize, "observed shared record size on Tp2.plb" );
		Assert.AreEqual( 20, plb.SharedRecords.Count, "observed shared record count on Tp2.plb" );
		Assert.IsTrue( plb.ParticleDensity is >= 10 and <= 500, "density is clamped to 10..500 by the engine" );
		Assert.AreEqual( 1024, plb.TotalParticles, "observed total-particle budget on Tp2.plb" );
		Assert.AreEqual( 0, plb.TrailingData.Length, "the whole file is now accounted for" );

		// Labelled per-effect fields (T-019, RE'd from FUN_00521930/FUN_00520560) — the values match each
		// effect's name/behaviour on the real Tp2.plb.
		var sparks = plb.Effects[1];
		Assert.AreEqual( ParticleEmissionMode.Burst, sparks.EmissionMode );
		Assert.AreEqual( 37, sparks.BurstCount, "Sparks throws a burst of many particles" );
		Assert.AreEqual( 100, sparks.LifetimeTicks, "sparks are short-lived" );

		var smokeFx = plb.Effects[2];
		Assert.AreEqual( 800, smokeFx.LifetimeTicks, "smoke lingers" );
		Assert.AreEqual( 64, smokeFx.EmissionVelocity.Y, "smoke rises" );
		Assert.IsTrue( smokeFx.HasChildEffect && smokeFx.ChildEffect == 12, "smoke spawns a child effect" );

		Assert.AreEqual( 31, plb.Effects[51].ChildEffect, "Repair spawns child effect 31" );
		Assert.AreEqual( 62, plb.Effects[4].BurstCount, "ExplodeFirey bursts a big cloud" );
	}

	[TestMethod]
	public void DecodesLabelledFieldsFromAFullRecord()
	{
		// A single full-size (320-byte) record with known values at the RE'd field offsets.
		const int rs = 320;
		var buf = new byte[8 + rs];
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 0, 4 ), 1 );
		BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( 4, 4 ), rs );
		void S16( int off, short v ) => BinaryPrimitives.WriteInt16LittleEndian( buf.AsSpan( 8 + off, 2 ), v );
		void S32( int off, int v ) => BinaryPrimitives.WriteInt32LittleEndian( buf.AsSpan( 8 + off, 4 ), v );
		S16( 0x0a, 1 );    // continuous
		S16( 0x54, 1 );    // cycles the ramp
		S32( 0x5c, 42 );   // burst count
		S32( 0x74, 1500 ); // lifetime
		S32( 0x40, 7 ); S32( 0x44, -8 ); S32( 0x48, 9 ); // velocity
		S32( 0x80, 1 ); S32( 0x84, 2 ); S32( 0x88, 3 );  // acceleration
		S32( 0xa8, 256 ); // cone angle
		S32( 0xac, 2048 ); // velocity scale
		S32( 0xb0, 31 );   // child effect

		using var stream = new MemoryStream( buf );
		var fx = new ParticleLibraryFile( stream ).Effects[0];

		Assert.AreEqual( ParticleEmissionMode.Continuous, fx.EmissionMode );
		Assert.IsTrue( fx.CyclesColorRamp );
		Assert.AreEqual( 42, fx.BurstCount );
		Assert.AreEqual( 1500, fx.LifetimeTicks );
		Assert.AreEqual( new ParticleVec3( 7, -8, 9 ), fx.EmissionVelocity );
		Assert.AreEqual( new ParticleVec3( 1, 2, 3 ), fx.Acceleration );
		Assert.AreEqual( 256, fx.EmissionConeAngle );
		Assert.AreEqual( 2048, fx.VelocityScale );
		Assert.AreEqual( 31, fx.ChildEffect );
		Assert.IsTrue( fx.HasChildEffect );
	}
}
