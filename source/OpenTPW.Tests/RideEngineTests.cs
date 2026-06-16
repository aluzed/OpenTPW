using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW.Tests;

[TestClass]
public class RideEngineTests
{
	// Records what the VM's engine opcodes ask the engine to do, so we can verify routing without a
	// renderer or audio device.
	private sealed class FakeEngine : IRideEngine
	{
		public List<(int type, int param, int id, int slot)> Spawns = new();
		public List<int> Sounds = new();
		public List<int> Kills = new();

		public void SpawnObject( int type, int parameter, int id, int slot ) => Spawns.Add( (type, parameter, id, slot) );
		public void PlaySound( int sound ) => Sounds.Add( sound );
		public void KillObject( int id ) => Kills.Add( id );
	}

	private static RideVM NewVm()
	{
		Log = new();
		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "Test.RSE" );
		return new RideVM( File.OpenRead( path ) );
	}

	private static Operand Lit( RideVM vm, int value ) => new( vm, Operand.Type.Literal, value );

	[TestMethod]
	public void EngineOpcodesRouteToEngine()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.ADDOBJ, Lit( vm, 3 ), Lit( vm, 42 ), Lit( vm, 7 ), Lit( vm, 1 ) );
		vm.CallOpcodeHandler( Opcode.SPAWNSOUND, Lit( vm, 9 ) );
		vm.CallOpcodeHandler( Opcode.KILLOBJ, Lit( vm, 7 ) );

		Assert.AreEqual( 1, fake.Spawns.Count );
		Assert.AreEqual( (3, 42, 7, 1), fake.Spawns[0] );
		CollectionAssert.AreEqual( new[] { 9 }, fake.Sounds );
		CollectionAssert.AreEqual( new[] { 7 }, fake.Kills );
	}

	[TestMethod]
	public void EngineOpcodesAreNoOpWithoutEngine()
	{
		var vm = NewVm(); // Engine is null

		// Must not throw — the null-conditional engine call is a guarded no-op (the pre-engine behaviour).
		vm.CallOpcodeHandler( Opcode.ADDOBJ, Lit( vm, 3 ), Lit( vm, 42 ), Lit( vm, 7 ), Lit( vm, 1 ) );
		vm.CallOpcodeHandler( Opcode.SPAWNSOUND, Lit( vm, 9 ) );
		vm.CallOpcodeHandler( Opcode.KILLOBJ, Lit( vm, 7 ) );
	}
}
