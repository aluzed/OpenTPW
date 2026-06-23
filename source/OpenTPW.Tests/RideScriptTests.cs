using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

[TestClass]
public class RideScriptTests
{
	// Test.RSE is a known, hand-written script (see content/testscripts/Test.rss):
	// one variable VAR_TEST, a NAME "Test Script 1", then COPY/NOP instructions.
	[TestMethod]
	public void LoadsTestScript()
	{
		Log = new();

		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "Test.RSE" );
		using var stream = File.OpenRead( path );

		var vm = new RideVM( stream );

		Assert.IsTrue( vm.Instructions.Count > 0, "should disassemble instructions" );
		Assert.IsTrue( vm.VariableNames.Contains( "VAR_TEST" ), "should read the variable table" );
		Assert.IsTrue( vm.Strings.Values.Any( s => s.Contains( "Test Script 1" ) ),
			"should read the string table" );
		Assert.IsTrue( vm.Disassembly.Length > 0, "should produce a disassembly" );

		// Config comes from the header (#setlimbo 10 / #setwalk 10 in the source).
		Assert.AreEqual( 10, vm.Config.LimboSize );
		Assert.AreEqual( 10, vm.Config.WalkSize );
	}

	// Exercises the newly implemented arithmetic opcodes (MULT / DIV / MOD).
	[TestMethod]
	public void ArithmeticOpcodes()
	{
		Log = new();

		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "Test.RSE" );
		using var stream = File.OpenRead( path );
		var vm = new RideVM( stream );

		// Variable index 0 exists after the common-variable padding.
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		OpcodeHandlers.Math.Mult( ref vm, Lit( vm, 6 ), Lit( vm, 7 ), dest );
		Assert.AreEqual( 42, dest.Value );

		OpcodeHandlers.Math.Div( ref vm, Lit( vm, 20 ), Lit( vm, 4 ), dest );
		Assert.AreEqual( 5, dest.Value );
		Assert.IsFalse( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );

		// Divide-by-zero is guarded to 0 (and sets the Zero flag).
		OpcodeHandlers.Math.Div( ref vm, Lit( vm, 7 ), Lit( vm, 0 ), dest );
		Assert.AreEqual( 0, dest.Value );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );

		OpcodeHandlers.Math.Mod( ref vm, Lit( vm, 17 ), Lit( vm, 5 ), dest );
		Assert.AreEqual( 2, dest.Value );
	}

	// ADD / SUB must set the Zero/Sign flags from their result (T-010).
	[TestMethod]
	public void AddSubSetFlags()
	{
		Log = new();

		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "Test.RSE" );
		using var stream = File.OpenRead( path );
		var vm = new RideVM( stream );

		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		// 5 + 5 = 10 -> no flags
		dest.Value = 5;
		OpcodeHandlers.Math.Add( ref vm, dest, Lit( vm, 5 ) );
		Assert.AreEqual( 10, dest.Value );
		Assert.AreEqual( RideVM.VMFlags.None, vm.Flags );

		// 5 - 5 = 0 -> Zero
		dest.Value = 5;
		OpcodeHandlers.Math.Sub( ref vm, Lit( vm, 5 ), Lit( vm, 5 ), dest );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );

		// 5 - 9 = -4 -> Sign
		OpcodeHandlers.Math.Sub( ref vm, Lit( vm, 5 ), Lit( vm, 9 ), dest );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Sign ) );
	}

	// BranchTest.RSE is a compiled loop (see content/testscripts/BranchTest.rss):
	//   VAR_I = 0; VAR_LIMIT = 3; do { VAR_I++ } while (VAR_I != VAR_LIMIT)
	// It must terminate with VAR_I == 3, which validates branch resolution (T-011).
	[TestMethod]
	public void BranchingLoopTerminates()
	{
		Log = new();

		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "BranchTest.RSE" );
		using var stream = File.OpenRead( path );
		var vm = new RideVM( stream );

		var varI = vm.VariableNames.IndexOf( "VAR_I" );
		Assert.IsTrue( varI >= 0, "VAR_I should be in the variable table" );

		// Drive the VM; the loop must exit on its own well within the step bound.
		for ( var steps = 0; steps < 1000 && vm.CurrentPos < vm.Instructions.Count; steps++ )
			vm.Step();

		Assert.AreEqual( 3, vm.Variables[varI], "loop should run exactly until VAR_I == VAR_LIMIT" );
	}

	// END stops the script (the game loop only steps while IsRunning).
	[TestMethod]
	public void EndHaltsExecution()
	{
		Log = new();

		var vm = LoadTestVm();
		vm.IsRunning = true;

		OpcodeHandlers.Logic.End( ref vm );

		Assert.IsFalse( vm.IsRunning, "END should clear IsRunning" );
	}

	// PUSH/POP and JSR/RETURN share one LIFO stack: pushes pop in reverse, and nested
	// subroutine returns unwind last-in-first-out. Underflow is guarded (no throw).
	[TestMethod]
	public void StackIsLifoAndGuarded()
	{
		Log = new();

		var vm = LoadTestVm();

		// PUSH / POP round-trip. POP writes the popped value into its destination operand.
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );
		OpcodeHandlers.Misc.Push( ref vm, Lit( vm, 11 ) );
		OpcodeHandlers.Misc.Push( ref vm, Lit( vm, 22 ) );
		Assert.AreEqual( 2, vm.Stack.Count );

		OpcodeHandlers.Misc.Pop( ref vm, dest ); // pops 22 (top) into dest
		Assert.AreEqual( 22, dest.Value );
		Assert.AreEqual( 1, vm.Stack.Count );
		Assert.AreEqual( 11, vm.Stack.Peek() );

		// POP underflow is ignored rather than throwing.
		OpcodeHandlers.Misc.Pop( ref vm, dest );
		OpcodeHandlers.Misc.Pop( ref vm, dest );
		Assert.AreEqual( 0, vm.Stack.Count );

		// Nested JSR/RETURN unwind LIFO (the old Queue did not).
		vm.CurrentPos = 100;
		OpcodeHandlers.Logic.JumpSubRoutine( ref vm, Loc( vm, 0 ) ); // pushes 100
		vm.CurrentPos = 200;
		OpcodeHandlers.Logic.JumpSubRoutine( ref vm, Loc( vm, 0 ) ); // pushes 200

		OpcodeHandlers.Logic.Return( ref vm );
		Assert.AreEqual( 200, vm.CurrentPos, "innermost call returns first" );
		OpcodeHandlers.Logic.Return( ref vm );
		Assert.AreEqual( 100, vm.CurrentPos, "outer call returns second" );

		// RETURN underflow is guarded.
		OpcodeHandlers.Logic.Return( ref vm );
	}

	// Date/time opcodes read the (injectable) wall clock and return raw C tm fields:
	// year since 1900, month 0-11. Semantics recovered from the executor (T-007).
	[TestMethod]
	public void DateTimeOpcodes()
	{
		Log = new();
		var vm = LoadTestVm();
		vm.WallClock = () => new DateTime( 2001, 6, 15, 13, 30, 45 );
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		OpcodeHandlers.Time.Year( ref vm, dest );   Assert.AreEqual( 101, dest.Value ); // 2001 - 1900
		OpcodeHandlers.Time.Month( ref vm, dest );  Assert.AreEqual( 5, dest.Value );   // June -> 5
		OpcodeHandlers.Time.Day( ref vm, dest );    Assert.AreEqual( 15, dest.Value );
		OpcodeHandlers.Time.Hour( ref vm, dest );   Assert.AreEqual( 13, dest.Value );
		OpcodeHandlers.Time.Minute( ref vm, dest ); Assert.AreEqual( 30, dest.Value );
		OpcodeHandlers.Time.Second( ref vm, dest ); Assert.AreEqual( 45, dest.Value );
	}

	// GETTIME reports the ride clock; SETTIMER sets expiry = now + value; GETTIMER returns the
	// remaining time, clamped to zero once elapsed (T-007).
	[TestMethod]
	public void TimerOpcodes()
	{
		Log = new();
		var vm = LoadTestVm();
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		vm.GameTime = 1000;
		OpcodeHandlers.Time.GetTime( ref vm, dest );
		Assert.AreEqual( 1000, dest.Value );

		OpcodeHandlers.Time.SetTimer( ref vm, Lit( vm, 500 ) ); // expires at 1500
		OpcodeHandlers.Time.GetTimer( ref vm, dest );
		Assert.AreEqual( 500, dest.Value );

		vm.GameTime = 1600; // past expiry
		OpcodeHandlers.Time.GetTimer( ref vm, dest );
		Assert.AreEqual( 0, dest.Value, "elapsed timer clamps to zero" );
	}

	// Cross-VM variable opcodes operate on the linked child/parent VM's Variables (T-007).
	[TestMethod]
	public void ChildParentVariableOpcodes()
	{
		Log = new();
		var parent = LoadTestVm();
		var child = LoadTestVm();
		parent.ActiveChild = child;
		child.Parent = parent;

		// Parent sets child var 2, then reads it back into its own var 0.
		OpcodeHandlers.Hierarchy.SetVarInChild( ref parent, Lit( parent, 2 ), Lit( parent, 77 ) );
		Assert.AreEqual( 77, child.Variables[2] );

		var pdest = new Operand( parent, Operand.Type.Variable, 0, 0 );
		OpcodeHandlers.Hierarchy.GetVarInChild( ref parent, pdest, Lit( parent, 2 ) );
		Assert.AreEqual( 77, parent.Variables[0] );

		// Child sets parent var 3, then reads it back into its own var 1.
		OpcodeHandlers.Hierarchy.SetVarInParent( ref child, Lit( child, 3 ), Lit( child, 88 ) );
		Assert.AreEqual( 88, parent.Variables[3] );

		var cdest = new Operand( child, Operand.Type.Variable, 1, 1 );
		OpcodeHandlers.Hierarchy.GetVarInParent( ref child, cdest, Lit( child, 3 ) );
		Assert.AreEqual( 88, child.Variables[1] );

		// A missing link or out-of-range index is a guarded no-op (no throw).
		var orphan = LoadTestVm();
		OpcodeHandlers.Hierarchy.SetVarInChild( ref orphan, Lit( orphan, 0 ), Lit( orphan, 1 ) );
		OpcodeHandlers.Hierarchy.SetVarInChild( ref parent, Lit( parent, 99999 ), Lit( parent, 1 ) );
	}

	// WAIT/WAITABS suspend the script: they arm a wake time and rewind so the instruction
	// re-runs each tick until the game clock reaches it, then fall through (T-007).
	[TestMethod]
	public void WaitSuspendsUntilGameTime()
	{
		Log = new();
		var vm = LoadTestVm();
		vm.GameTime = 100;

		// Step() advances CurrentPos past the WAIT before the handler runs; emulate that.
		// First hit: arm wake = 100 + 50 = 150 and rewind to re-run.
		vm.CurrentPos = 5;
		OpcodeHandlers.Time.WaitAbs( ref vm, Lit( vm, 50 ) );
		Assert.AreEqual( 150, vm.WaitUntil );
		Assert.AreEqual( 4, vm.CurrentPos );

		// Still before the wake time: keep waiting (rewind again, wake unchanged).
		vm.GameTime = 120;
		vm.CurrentPos = 5;
		OpcodeHandlers.Time.WaitAbs( ref vm, Lit( vm, 50 ) );
		Assert.AreEqual( 150, vm.WaitUntil );
		Assert.AreEqual( 4, vm.CurrentPos );

		// Reached the wake time: clear the wait and proceed (no rewind).
		vm.GameTime = 150;
		vm.CurrentPos = 5;
		OpcodeHandlers.Time.WaitAbs( ref vm, Lit( vm, 50 ) );
		Assert.IsNull( vm.WaitUntil );
		Assert.AreEqual( 5, vm.CurrentPos );
	}

	// HUSH/HOP are a second LIFO stack, independent of PUSH/POP (T-007).
	[TestMethod]
	public void HushHopSecondaryStack()
	{
		Log = new();
		var vm = LoadTestVm();
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		OpcodeHandlers.Misc.Hush( ref vm, Lit( vm, 7 ) );
		OpcodeHandlers.Misc.Hush( ref vm, Lit( vm, 9 ) );

		// Independent of the PUSH/POP stack.
		OpcodeHandlers.Misc.Push( ref vm, Lit( vm, 99 ) );
		Assert.AreEqual( 2, vm.HushStack.Count );
		Assert.AreEqual( 1, vm.Stack.Count );

		OpcodeHandlers.Misc.Hop( ref vm, dest ); // LIFO -> 9
		Assert.AreEqual( 9, dest.Value );
		OpcodeHandlers.Misc.Hop( ref vm, dest ); // -> 7
		Assert.AreEqual( 7, dest.Value );

		// Underflow is a guarded no-op (dest keeps its value).
		OpcodeHandlers.Misc.Hop( ref vm, dest );
		Assert.AreEqual( 7, dest.Value );
	}

	// SPAWNCHILD resolves a string-named child script via the engine's ChildLoader, links it as
	// the active child, and (once spawned) the child-variable opcodes operate on it (T-007).
	[TestMethod]
	public void SpawnChildLinksAndDrivesChildVars()
	{
		Log = new();
		var parent = LoadTestVm();
		var spawned = LoadTestVm();
		parent.Strings[1000] = "carscript";
		parent.ChildLoader = name => name == "carscript" ? spawned : null;

		// No child yet: a child-var op is a guarded no-op.
		OpcodeHandlers.Hierarchy.SetVarInChild( ref parent, Lit( parent, 0 ), Lit( parent, 5 ) );
		Assert.AreNotEqual( 5, spawned.Variables[0] );

		// Spawn it, then drive its variables.
		var nameOperand = new Operand( parent, Operand.Type.String, 1000 );
		OpcodeHandlers.Hierarchy.SpawnChild( ref parent, nameOperand );
		Assert.AreSame( spawned, parent.ActiveChild );
		Assert.AreSame( parent, spawned.Parent );
		CollectionAssert.Contains( parent.Children, spawned );

		OpcodeHandlers.Hierarchy.SetVarInChild( ref parent, Lit( parent, 2 ), Lit( parent, 42 ) );
		Assert.AreEqual( 42, spawned.Variables[2] );

		// An unknown script name spawns nothing (no throw).
		parent.Strings[1001] = "missing";
		OpcodeHandlers.Hierarchy.SpawnChild( ref parent, new Operand( parent, Operand.Type.String, 1001 ) );
		Assert.AreEqual( 1, parent.Children.Count );
	}

	// The limbo opcodes: a per-VM timed queue of values. LIMBO parks (value, now+secs*1000); UNLIMBO
	// releases the first EXPIRED entry, FORCEUNLIMBO the first regardless; INLIMBO=count, LIMBOSPACE=free.
	// Result feeds the Zero flag (so a following JZ reacts). Semantics recovered from the executor (T-007).
	[TestMethod]
	public void LimboOpcodes()
	{
		Log = new();
		var vm = LoadTestVm();
		vm.GameTime = 1000;
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		// Park two values: A expires at 1000+1*1000=2000, B at 1000+5*1000=6000.
		OpcodeHandlers.Limbo.Park( ref vm, Lit( vm, 42 ), Lit( vm, 1 ) );
		Assert.IsFalse( vm.Flags.HasFlag( RideVM.VMFlags.Zero ), "added → result 1" );
		OpcodeHandlers.Limbo.Park( ref vm, Lit( vm, 99 ), Lit( vm, 5 ) );

		OpcodeHandlers.Limbo.Count( ref vm, dest );
		Assert.AreEqual( 2, dest.Value );
		OpcodeHandlers.Limbo.Space( ref vm, dest );
		Assert.AreEqual( RideVM.LimboCapacity - 2, dest.Value );

		// Nothing expired yet at t=1500 → UNLIMBO returns 0 and sets Zero.
		vm.GameTime = 1500;
		OpcodeHandlers.Limbo.Release( ref vm, dest );
		Assert.AreEqual( 0, dest.Value );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );

		// At t=3000, A (expiry 2000) is expired → released; B remains.
		vm.GameTime = 3000;
		OpcodeHandlers.Limbo.Release( ref vm, dest );
		Assert.AreEqual( 42, dest.Value );
		OpcodeHandlers.Limbo.Count( ref vm, dest );
		Assert.AreEqual( 1, dest.Value );

		// FORCEUNLIMBO releases B even though it hasn't expired.
		OpcodeHandlers.Limbo.ForceRelease( ref vm, dest );
		Assert.AreEqual( 99, dest.Value );
		OpcodeHandlers.Limbo.Count( ref vm, dest );
		Assert.AreEqual( 0, dest.Value );

		// Empty: both release ops return 0 (guarded, no throw).
		OpcodeHandlers.Limbo.ForceRelease( ref vm, dest );
		Assert.AreEqual( 0, dest.Value );
	}

	// Cross-VM opcodes resolve another script by its global handle (GET/SETREMOTEVAR) or find one by name
	// (FINDSCRIPTRAND). Semantics recovered from the executor (op_90/91/92) + the resolver FUN_0055a070.
	[TestMethod]
	public void CrossVmRemoteVarsAndFindScript()
	{
		Log = new();
		var a = LoadTestVm();
		var b = LoadTestVm();
		a.ScriptName = "Caller";
		b.ScriptName = $"TPW_TEST_TARGET_{b.Handle}"; // unique → matches exactly this VM
		while ( b.Variables.Count < 4 ) b.Variables.Add( 0 );

		var dest = new Operand( a, Operand.Type.Variable, 0, 0 );

		// SETREMOTEVAR(handle, index, value) then GETREMOTEVAR(dest, handle, index) round-trips through b.
		OpcodeHandlers.Hierarchy.SetRemoteVar( ref a, Lit( a, b.Handle ), Lit( a, 2 ), Lit( a, 55 ) );
		Assert.AreEqual( 55, b.Variables[2] );
		OpcodeHandlers.Hierarchy.GetRemoteVar( ref a, dest, Lit( a, b.Handle ), Lit( a, 2 ) );
		Assert.AreEqual( 55, dest.Value );

		// Unknown handle → GET returns 0 (+ Zero flag); SET / out-of-range index are guarded no-ops.
		OpcodeHandlers.Hierarchy.GetRemoteVar( ref a, dest, Lit( a, 0 ), Lit( a, 2 ) );
		Assert.AreEqual( 0, dest.Value );
		Assert.IsTrue( a.Flags.HasFlag( RideVM.VMFlags.Zero ) );
		OpcodeHandlers.Hierarchy.SetRemoteVar( ref a, Lit( a, b.Handle ), Lit( a, 9999 ), Lit( a, 7 ) );
		OpcodeHandlers.Hierarchy.SetRemoteVar( ref a, Lit( a, 0 ), Lit( a, 2 ), Lit( a, 7 ) );

		// FINDSCRIPTRAND(name, dest): find b by its unique name → its handle.
		var prevRng = RideVM.RandomIndex;
		try
		{
			RideVM.RandomIndex = _ => 0; // deterministic: take the first match
			a.Strings[2001] = b.ScriptName;
			OpcodeHandlers.Hierarchy.FindScriptRand( ref a, new Operand( a, Operand.Type.String, 2001 ), dest );
			Assert.AreEqual( b.Handle, dest.Value );

			// An unmatched name returns 0 (+ Zero flag).
			a.Strings[2002] = "no_such_script_zzz";
			OpcodeHandlers.Hierarchy.FindScriptRand( ref a, new Operand( a, Operand.Type.String, 2002 ), dest );
			Assert.AreEqual( 0, dest.Value );
			Assert.IsTrue( a.Flags.HasFlag( RideVM.VMFlags.Zero ) );
		}
		finally { RideVM.RandomIndex = prevRng; }
	}

	// REMOVECHILD destroys the active child and clears the child link (op_65), dropping it from the
	// global registry so cross-VM lookups can no longer resolve it.
	[TestMethod]
	public void RemoveChildDetachesAndUnregisters()
	{
		Log = new();
		var parent = LoadTestVm();
		var child = LoadTestVm();
		parent.ActiveChild = child;
		parent.Children.Add( child );
		child.Parent = parent;
		var childHandle = child.Handle;
		Assert.IsNotNull( RideVM.Resolve( childHandle ) );

		OpcodeHandlers.Hierarchy.RemoveChild( ref parent );
		Assert.IsNull( parent.ActiveChild );
		Assert.AreEqual( 0, parent.Children.Count );
		Assert.IsNull( child.Parent );
		Assert.IsNull( RideVM.Resolve( childHandle ), "child dropped from the global registry" );

		// No active child: REMOVECHILD is a guarded no-op.
		OpcodeHandlers.Hierarchy.RemoveChild( ref parent );
	}

	// The walk subsystem: WALKON parks a peep walking on, the per-tick advance moves it to Arrived at its
	// end time, WALKOFF sends it back (Releasing → Done), and WALKGET hands the finished peep back. Plus
	// the walk-float timer (WALKST_FLOAT / WALKFLOATSTAT / WALKFLOATSTOP). RE'd from op_76..op_81 (T-007).
	[TestMethod]
	public void WalkOpcodes()
	{
		Log = new();
		var vm = LoadTestVm();
		vm.GameTime = 0;
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );

		// WALKON(peep=7, start=1, end=2, e5, e6, type, e8): one slot becomes active.
		OpcodeHandlers.Walk.WalkOn( ref vm, Lit( vm, 7 ), Lit( vm, 1 ), Lit( vm, 2 ),
			Lit( vm, 0 ), Lit( vm, 0 ), Lit( vm, 0 ), Lit( vm, 0 ) );
		Assert.AreEqual( 1, vm.ActiveWalkCount );

		// Nothing finished yet: WALKGET returns 0 (+ Zero flag).
		OpcodeHandlers.Walk.WalkGet( ref vm, dest );
		Assert.AreEqual( 0, dest.Value );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );

		// Advance past the walk duration → the peep has Arrived (still occupying its slot, not retrievable).
		vm.GameTime = RideVM.DefaultWalkDuration + 1;
		vm.WalkAdvance();
		OpcodeHandlers.Walk.WalkGet( ref vm, dest );
		Assert.AreEqual( 0, dest.Value, "an arrived (still-aboard) peep is not retrievable" );

		// WALKOFF then advance → the peep finishes (Done) and WALKGET hands it back, freeing the slot.
		OpcodeHandlers.Walk.WalkOff( ref vm, Lit( vm, 7 ) );
		vm.GameTime += RideVM.DefaultWalkDuration + 1;
		vm.WalkAdvance();
		OpcodeHandlers.Walk.WalkGet( ref vm, dest );
		Assert.AreEqual( 7, dest.Value, "the peep that finished walking off is returned" );
		Assert.AreEqual( 0, vm.ActiveWalkCount, "its slot is freed" );

		// Walk-float timer: start, read, stop.
		OpcodeHandlers.Walk.WalkStartFloat( ref vm, Lit( vm, 250 ), Lit( vm, 1 ), Lit( vm, 2 ) );
		OpcodeHandlers.Walk.WalkFloatStat( ref vm, dest );
		Assert.AreEqual( 250, dest.Value );
		OpcodeHandlers.Walk.WalkFloatStop( ref vm );
		OpcodeHandlers.Walk.WalkFloatStat( ref vm, dest );
		Assert.AreEqual( 1000, dest.Value, "STOP finalises the float value" );
	}

	// The head subsystem: ADDHEAD drops a value into a random free slot, DELHEAD clears all slots holding
	// a value. A per-VM head-slot table (pure VM bookkeeping). RE'd from op_56/op_57 (T-007).
	[TestMethod]
	public void HeadOpcodes()
	{
		Log = new();
		var vm = LoadTestVm();
		var prev = RideVM.RandomIndex;
		try
		{
			RideVM.RandomIndex = _ => 0; // deterministic: always take the first free slot

			OpcodeHandlers.Heads.AddHead( ref vm, Lit( vm, 11 ) ); // → slot 0
			OpcodeHandlers.Heads.AddHead( ref vm, Lit( vm, 22 ) ); // first free is now slot 1
			Assert.AreEqual( 2, vm.HeadCount );
			Assert.AreEqual( 11, vm.HeadSlots[0] );
			Assert.AreEqual( 22, vm.HeadSlots[1] );

			OpcodeHandlers.Heads.DelHead( ref vm, Lit( vm, 11 ) ); // frees slot 0
			Assert.AreEqual( 1, vm.HeadCount );
			Assert.AreEqual( 0, vm.HeadSlots[0] );

			// Filling past capacity is a guarded no-op (no overflow).
			for ( var i = 0; i < RideVM.DefaultHeadCapacity + 3; i++ )
				OpcodeHandlers.Heads.AddHead( ref vm, Lit( vm, 99 ) );
			Assert.AreEqual( RideVM.DefaultHeadCapacity, vm.HeadCount, "table fills to capacity, then stops" );
		}
		finally { RideVM.RandomIndex = prev; }
	}

	// GETCUSTPTCLCODE: RE'd at instruction level (op_94) — it discards its operand and always writes 0
	// (the result is EDI, zeroed in the executor prologue). A stub returning 0 in the shipped build. T-007.
	[TestMethod]
	public void GetCustomParticleCodeAlwaysReturnsZero()
	{
		Log = new();
		var vm = LoadTestVm();
		var dest = new Operand( vm, Operand.Type.Variable, 0, 0 );
		dest.Value = 999; // pre-set to prove it's overwritten regardless of the arg

		OpcodeHandlers.Particles.GetCustomParticleCode( ref vm, dest, Lit( vm, 42 ) );
		Assert.AreEqual( 0, dest.Value );
		Assert.IsTrue( vm.Flags.HasFlag( RideVM.VMFlags.Zero ) );
	}

	// TURBO stores the operand's low byte in the VM (struct +0xb8) — pure VM state. RE'd from op_51 (T-007).
	[TestMethod]
	public void TurboStoresFlag()
	{
		Log = new();
		var vm = LoadTestVm();
		Assert.AreEqual( 0, vm.Turbo );

		OpcodeHandlers.Effects.Turbo( ref vm, Lit( vm, 3 ) );
		Assert.AreEqual( 3, vm.Turbo );

		OpcodeHandlers.Effects.Turbo( ref vm, Lit( vm, 0x142 ) ); // only the low byte is stored
		Assert.AreEqual( 0x42, vm.Turbo );
	}

	private static RideVM LoadTestVm()
	{
		var path = Path.Combine( AppContext.BaseDirectory, "content", "testscripts", "Test.RSE" );
		using var stream = File.OpenRead( path );
		return new RideVM( stream );
	}

	private static Operand Lit( RideVM vm, int value ) => new( vm, Operand.Type.Literal, value );
	private static Operand Loc( RideVM vm, int value ) => new( vm, Operand.Type.Location, value );
}
