using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RideVehicleTests
{
	// T-048: a car ride shows as many riders/cars as its model declares car/seat nodes (object 0x80 +
	// car 0x100), clamped to a sane range — not a fixed four. Positions stay procedural (the real node
	// positions are runtime simulation output), but the count is authored.

	[TestMethod]
	public void SeatCountFollowsAuthoredNodeCount()
	{
		Assert.AreEqual( 9, RideVehicle.SeatCountFor( 9 ), "Bird's nine seat nodes → nine riders" );
		Assert.AreEqual( 3, RideVehicle.SeatCountFor( 3 ), "go-karts' three car nodes → three" );
	}

	[TestMethod]
	public void SeatCountFallsBackAndClamps()
	{
		Assert.AreEqual( 4, RideVehicle.SeatCountFor( 0 ), "no node graph → the default seat count" );
		Assert.AreEqual( 4, RideVehicle.SeatCountFor( -1 ), "a negative count is treated as none" );
		Assert.AreEqual( 1, RideVehicle.SeatCountFor( 1 ) );
		Assert.AreEqual( 12, RideVehicle.SeatCountFor( 99 ), "absurd counts are capped" );
	}

	[TestMethod]
	public void CountsObjectAndCarNodesFromTheGraph()
	{
		// A Bird-like graph: nine object/head nodes (0xB1, ids 1-9) + one walk node (0x811). Only the
		// object/car nodes feed the seat count — the walk node does not.
		const int tableOff = 0x90;
		const int n = 10;
		var d = new byte[tableOff + n * 0x14];
		void U16( int o, ushort v ) => BinaryPrimitives.WriteUInt16LittleEndian( d.AsSpan( o, 2 ), v );
		void U32( int o, uint v ) => BinaryPrimitives.WriteUInt32LittleEndian( d.AsSpan( o, 4 ), v );

		U16( 0x48, n );
		U32( 0x7c, tableOff );
		for ( int i = 0; i < 9; i++ )
		{
			U32( tableOff + i * 0x14 + 0, 0xB1 );    // object/head (carries 0x80)
			U32( tableOff + i * 0x14 + 4, (uint)(i + 1) );
		}
		U32( tableOff + 9 * 0x14 + 0, 0x811 );       // walk node (no 0x80/0x100)
		U32( tableOff + 9 * 0x14 + 4, 1 );

		var nodes = ModelFile.ParseNodeTable( d );
		int carSeatNodes = nodes.Count( x => x.IsObject || x.IsCar );

		Assert.AreEqual( 9, carSeatNodes, "nine object nodes count, the walk node does not" );
		Assert.AreEqual( 9, RideVehicle.SeatCountFor( carSeatNodes ) );
	}
}
