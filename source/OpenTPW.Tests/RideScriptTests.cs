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

	private static Operand Lit( RideVM vm, int value ) => new( vm, Operand.Type.Literal, value );
}
