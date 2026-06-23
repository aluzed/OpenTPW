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
		public List<string> Sounds = new();
		public List<int> Kills = new();
		public List<(int id, int param, int value)> Params = new();
		public List<(int id, int anim, bool loop)> Anims = new();
		public List<string> Screams = new(); // scream opcode trace
		public bool Animating; // controls IsAnimating / AnyAnimating

		public void SpawnObject( int type, int parameter, int id, int slot ) => Spawns.Add( (type, parameter, id, slot) );
		public void PlaySound( string name ) => Sounds.Add( name );
		public void KillObject( int id ) => Kills.Add( id );
		public void SetObjectParam( int id, int param, int value ) => Params.Add( (id, param, value) );
		public void TriggerAnim( int id, int anim, bool loop ) => Anims.Add( (id, anim, loop) );
		public void SetAnimSpeed( int id, float speed ) { }
		public void FlushAnims( int id ) { }
		public int GetAnim( int id ) => 0;
		public bool IsAnimating( int id, int anim ) => Animating;
		public bool AnyAnimating() => Animating;
		public void StartScream( int code, int level ) => Screams.Add( $"start({code},{level})" );
		public void StopScream() => Screams.Add( "stop" );
		public void SingleScream( int code, int level ) => Screams.Add( $"single({code},{level})" );
		public void SetScreamLevel( int level ) => Screams.Add( $"level({level})" );
		public List<string> Effects = new(); // COAST / EVENT / SETREVERB / DIPMUSIC trace
		public void Coast( int sub, int arg ) => Effects.Add( $"coast({sub},{arg})" );
		public void Tour( int sub, int arg ) => Effects.Add( $"tour({sub},{arg})" );
		public void Bump( int sub, int arg ) => Effects.Add( $"bump({sub},{arg})" );
		public void Event( int type, int p1, int p2 ) => Effects.Add( $"event({type},{p1},{p2})" );
		public void SetReverb( int level ) => Effects.Add( $"reverb({level})" );
		public void DipMusic( int amount ) => Effects.Add( $"dip({amount})" );
		public List<int> ParticleEffects = new(); // SpawnParticleEffect codes
		public void SpawnParticleEffect( int effectCode ) => ParticleEffects.Add( effectCode );
		public List<string> LightCalls = new(); // light opcode trace (id + scaled values)
		private static string F( float v ) => v.ToString( "0.00", System.Globalization.CultureInfo.InvariantCulture );
		public void EnableLight( int id ) => LightCalls.Add( $"enable({id})" );
		public void DisableLight( int id ) => LightCalls.Add( $"disable({id})" );
		public void SetLight( int id, float brightness ) => LightCalls.Add( $"set({id},{F( brightness )})" );
		public void ColourLight( int id, float r, float g, float b ) => LightCalls.Add( $"colour({id},{F( r )},{F( g )},{F( b )})" );
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
		// SPAWNSOUND takes a string operand (sound name); the handler resolves it via vm.Strings.
		vm.Strings[5] = "EventMap.rse";
		vm.CallOpcodeHandler( Opcode.SPAWNSOUND, new Operand( vm, Operand.Type.String, 5 ) );
		vm.CallOpcodeHandler( Opcode.KILLOBJ, Lit( vm, 7 ) );

		Assert.AreEqual( 1, fake.Spawns.Count );
		Assert.AreEqual( (3, 42, 7, 1), fake.Spawns[0] );
		CollectionAssert.AreEqual( new[] { "EventMap.rse" }, fake.Sounds );
		CollectionAssert.AreEqual( new[] { 7 }, fake.Kills );
	}

	[TestMethod]
	public void AnimationOpcodesRouteToEngine()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.TRIGANIM, Lit( vm, 5 ), Lit( vm, 2 ), Lit( vm, 0 ) );
		vm.CallOpcodeHandler( Opcode.LOOPANIM, Lit( vm, 5 ), Lit( vm, 3 ) );
		vm.CallOpcodeHandler( Opcode.SETOBJPARAM, Lit( vm, 5 ), Lit( vm, 1 ), Lit( vm, 99 ) );

		Assert.AreEqual( (5, 2, false), fake.Anims[0] );
		Assert.AreEqual( (5, 3, true), fake.Anims[1] );
		Assert.AreEqual( (5, 1, 99), fake.Params[0] );
	}

	// Light opcodes route to the engine; the brightness/colour operands are 0..100 percentages that the
	// handler scales by 0.01 (the original's _DAT_00700fe0) before the engine sees them. See T-007.
	[TestMethod]
	public void LightOpcodesRouteToEngineWithPercentScale()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.ENABLELIGHT, Lit( vm, 3 ) );
		vm.CallOpcodeHandler( Opcode.SETLIGHT, Lit( vm, 3 ), Lit( vm, 50 ) );           // 50% → 0.50
		vm.CallOpcodeHandler( Opcode.COLOURLIGHT, Lit( vm, 3 ), Lit( vm, 100 ), Lit( vm, 0 ), Lit( vm, 25 ) ); // → 1.00, 0.00, 0.25
		vm.CallOpcodeHandler( Opcode.DISABLELIGHT, Lit( vm, 3 ) );

		CollectionAssert.AreEqual(
			new[] { "enable(3)", "set(3,0.50)", "colour(3,1.00,0.00,0.25)", "disable(3)" },
			fake.LightCalls );
	}

	// Particle opcodes route to the engine's particle system with the par_lib effect codes:
	// REPAIREFFECT → P_EFFECT_Repair (51), SPARK → P_EFFECT_Sparks (1). See T-007 / T-019.
	[TestMethod]
	public void ParticleOpcodesRouteToEngine()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.REPAIREFFECT, Lit( vm, 0 ) );
		vm.CallOpcodeHandler( Opcode.SPARK, Lit( vm, 0 ), Lit( vm, 0 ), Lit( vm, 0 ), Lit( vm, 0 ) );

		CollectionAssert.AreEqual( new[] { 51, 1 }, fake.ParticleEffects );
	}

	// ADDOBJ_EXT (the 5-operand extended ADDOBJ) registers the object like ADDOBJ, and for particle types
	// (3-10) also fires the .PLB effect with the `code` operand. Sound types (1-2) only register. T-007.
	[TestMethod]
	public void AddObjExtRegistersAndSpawnsParticleForParticleTypes()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		// Particle type 4: registers (type,node,code,volume) + spawns particle effect `code`.
		vm.CallOpcodeHandler( Opcode.ADDOBJ_EXT, Lit( vm, 4 ), Lit( vm, 2 ), Lit( vm, 11 ), Lit( vm, 100 ), Lit( vm, 7 ) );
		Assert.AreEqual( (4, 2, 11, 100), fake.Spawns[^1] );
		CollectionAssert.AreEqual( new[] { 11 }, fake.ParticleEffects );

		// Sound type 1: registers only (no particle; sound deferred like ADDOBJ).
		vm.CallOpcodeHandler( Opcode.ADDOBJ_EXT, Lit( vm, 1 ), Lit( vm, 0 ), Lit( vm, 5 ), Lit( vm, 50 ), Lit( vm, 0 ) );
		Assert.AreEqual( (1, 0, 5, 50), fake.Spawns[^1] );
		CollectionAssert.AreEqual( new[] { 11 }, fake.ParticleEffects ); // unchanged — no particle for a sound type
	}

	[TestMethod]
	public void WaitAnimSuspendsWhileAnimating()
	{
		var vm = NewVm();
		var fake = new FakeEngine { Animating = true };
		vm.Engine = fake;

		vm.CurrentPos = 10;
		vm.CallOpcodeHandler( Opcode.WAITANIM, Lit( vm, 5 ), Lit( vm, 2 ) );
		Assert.AreEqual( 9, vm.CurrentPos, "WAITANIM should rewind the PC while still animating" );

		fake.Animating = false;
		vm.CallOpcodeHandler( Opcode.WAITANIM, Lit( vm, 5 ), Lit( vm, 2 ) );
		Assert.AreEqual( 9, vm.CurrentPos, "WAITANIM should fall through once the animation is done" );
	}

	[TestMethod]
	public void ChannelLetterIsFirstLetterOfAnimationName()
	{
		// The original (FUN_00461f10) names ride keyframe files <base><letter>[<n>].md2, where the
		// channel letter is the first letter of the animation name. Verified against monkey/totem/
		// wateride WAD contents — see docs/08-ghidra-animation.md.
		Assert.AreEqual( 'c', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Create ) );
		Assert.AreEqual( 'i', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Idle ) );
		Assert.AreEqual( 'l', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Load ) );
		Assert.AreEqual( 's', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Start ) );
		Assert.AreEqual( 'm', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Main ) );
		Assert.AreEqual( 'e', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_End ) );
		Assert.AreEqual( 'u', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Unload ) );
		Assert.AreEqual( 'b', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Break ) );
		Assert.AreEqual( 'r', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Repair ) );
		Assert.AreEqual( 'o', RideEngine.ChannelLetter( ScriptDefs.Animations.ANIM_Other ) );
	}

	[TestMethod]
	public void ScreamOpcodesRouteToEngine()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		// Mirrors monkey.rse: STARTSCREAM(code,level), SCREAMLEVEL(level), SINGLESCREAM(code,level), STOPSCREAM().
		vm.CallOpcodeHandler( Opcode.STARTSCREAM, Lit( vm, 0 ), Lit( vm, 20 ) );
		vm.CallOpcodeHandler( Opcode.SCREAMLEVEL, Lit( vm, 50 ) );
		vm.CallOpcodeHandler( Opcode.SINGLESCREAM, Lit( vm, 0 ), Lit( vm, 90 ) );
		vm.CallOpcodeHandler( Opcode.STOPSCREAM );

		CollectionAssert.AreEqual( new[] { "start(0,20)", "level(50)", "single(0,90)", "stop" }, fake.Screams );
	}

	[TestMethod]
	public void EffectOpcodesRouteToEngine()
	{
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.COAST, Lit( vm, 6 ), Lit( vm, 8 ) );   // set capacity
		vm.CallOpcodeHandler( Opcode.EVENT, Lit( vm, 3 ), Lit( vm, -1 ), Lit( vm, 62 ) );
		vm.CallOpcodeHandler( Opcode.SETREVERB, Lit( vm, 2 ) );
		vm.CallOpcodeHandler( Opcode.DIPMUSIC, Lit( vm, 50 ) );
		CollectionAssert.AreEqual( new[] { "coast(6,8)", "event(3,-1,62)", "reverb(2)", "dip(50)" }, fake.Effects );

		// COAST query subcommands set the Zero flag to steer the coaster load/unload loops: sub 2
		// "can-load?" clears Zero (fall through to the VAR_LETMEON gate), sub 3 "wants-off?" sets it.
		vm.Flags = RideVM.VMFlags.Zero;
		vm.CallOpcodeHandler( Opcode.COAST, Lit( vm, 2 ), Lit( vm, 0 ) );
		Assert.IsFalse( vm.Flags.HasFlag( RideVM.VMFlags.Zero ), "COAST can-load? should clear Zero" );
		vm.CallOpcodeHandler( Opcode.COAST, Lit( vm, 3 ), Lit( vm, 0 ) );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ), "COAST wants-off? should set Zero" );
	}

	[TestMethod]
	public void CarMultiplexersRouteToEngine()
	{
		// TOUR (op_53) and BUMP (op_54) are fixed 2-operand multiplexers like COAST: every (sub, arg)
		// routes to the engine, which records it. Verifies arity + routing for the last two opcodes.
		var vm = NewVm();
		var fake = new FakeEngine();
		vm.Engine = fake;

		vm.CallOpcodeHandler( Opcode.TOUR, Lit( vm, 1 ), Lit( vm, 7 ) );   // TOUR_INITIALISE
		vm.CallOpcodeHandler( Opcode.BUMP, Lit( vm, 3 ), Lit( vm, 0 ) );   // BUMP_STARTRIDE
		vm.CallOpcodeHandler( Opcode.BUMP, Lit( vm, 11 ), Lit( vm, 0 ) );  // BUMP_CARSONRIDE (a query)
		CollectionAssert.AreEqual( new[] { "tour(1,7)", "bump(3,0)", "bump(11,0)" }, fake.Effects );
	}

	[TestMethod]
	public void CarMultiplexerQueriesReturnZeroWithoutEngine()
	{
		// Engine-absent (no car subsystem) — exactly the original's `0x4a454647`-magic-missing path:
		// a query subcommand returns 0 (writes the dest variable + sets the Zero flag the next BRANCH
		// reads); a command is a guarded no-op. Must not throw.
		var vm = NewVm(); // Engine is null
		var dest = new Operand( vm, Operand.Type.Variable, 0 );
		dest.Value = 123;

		vm.Flags = RideVM.VMFlags.None;
		vm.CallOpcodeHandler( Opcode.BUMP, Lit( vm, 11 ), dest ); // CARSONRIDE query → 0
		Assert.AreEqual( 0, dest.Value, "query should write 0 into the destination variable" );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ), "query result 0 should set Zero" );

		// A command subcommand leaves the Zero flag untouched (pure no-op without an engine).
		vm.Flags = RideVM.VMFlags.None;
		vm.CallOpcodeHandler( Opcode.BUMP, Lit( vm, 3 ), Lit( vm, 5 ) ); // STARTRIDE command
		vm.CallOpcodeHandler( Opcode.TOUR, Lit( vm, 1 ), Lit( vm, 5 ) ); // INITIALISE command
		Assert.IsFalse( vm.Flags.HasFlag( RideVM.VMFlags.Zero ), "a command must not force the Zero flag" );
	}

	[TestMethod]
	public void EngineOpcodesAreNoOpWithoutEngine()
	{
		var vm = NewVm(); // Engine is null

		// Must not throw — the null-conditional engine call is a guarded no-op (the pre-engine behaviour).
		vm.CallOpcodeHandler( Opcode.ADDOBJ, Lit( vm, 3 ), Lit( vm, 42 ), Lit( vm, 7 ), Lit( vm, 1 ) );
		vm.CallOpcodeHandler( Opcode.SPAWNSOUND, Lit( vm, 9 ) );
		vm.CallOpcodeHandler( Opcode.KILLOBJ, Lit( vm, 7 ) );
		vm.CallOpcodeHandler( Opcode.STARTSCREAM, Lit( vm, 0 ), Lit( vm, 20 ) );
		vm.CallOpcodeHandler( Opcode.STOPSCREAM );
	}
}
