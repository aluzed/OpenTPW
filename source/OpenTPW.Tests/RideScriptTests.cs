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

	private static Operand Lit( RideVM vm, int value ) => new( vm, Operand.Type.Literal, value );
}
