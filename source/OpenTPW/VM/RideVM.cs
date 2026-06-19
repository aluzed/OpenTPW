using System;
using System.Reflection;

namespace OpenTPW;

public partial class RideVM
{
	private readonly RideScriptFile rsseqFile;

	public string ScriptName { get; set; } = "Unnamed";

	public bool IsRunning { get; set; }
	public int CurrentPos { get; set; }

	// Wall clock used by the date/time opcodes (YEAR..SEC). The original calls the C runtime
	// time()/localtime(); injectable here so scripts (and tests) are deterministic.
	public Func<DateTime> WallClock { get; set; } = () => DateTime.Now;

	// Ride-alive time in the game's clock units (the original reads a global ms counter); the
	// engine advances this. GETTIME reports it; SETTIMER/GETTIMER work relative to it.
	public int GameTime { get; set; }

	// Single ride timer (the original keeps one per script). SETTIMER sets its expiry to
	// GameTime + value; GETTIMER returns the remaining time (never negative).
	public int Timer { get; set; }

	// VM hierarchy for the child/parent variable opcodes. The original finds the other VM by a
	// handle (struct +0x0c child, +0x10 parent) in a global VM list; modelled here as direct
	// references. SPAWNCHILD (engine, not yet implemented) will set these when it spawns a
	// child ride script. SET/GETVARIN{CHILD,PARENT} read/write the linked VM's Variables.
	public RideVM? Parent { get; set; }
	public RideVM? ActiveChild { get; set; }

	/// <summary>Child ride scripts spawned by this one (via SPAWNCHILD).</summary>
	public List<RideVM> Children { get; } = new();

	/// <summary>
	/// How SPAWNCHILD turns a child-script name into a child VM. The engine sets this to load
	/// the named <c>.RSE</c> from the VFS; left null, SPAWNCHILD is a no-op (and tests inject a
	/// fake). The original builds a path from the name and calls the script loader (FUN_005587f0).
	/// </summary>
	public Func<string, RideVM?>? ChildLoader { get; set; }

	/// <summary>
	/// The ride engine the "engine" opcodes (ADDOBJ, SPAWNSOUND, …) drive. The running game sets this
	/// to a <see cref="RideEngine"/>; left null (unit tests, headless tools) those opcodes are guarded
	/// no-ops, identical to having no handler. See the ride-engine plan / docs/tickets/T-007.
	/// </summary>
	public IRideEngine? Engine { get; set; }

	// Set by WAIT/WAITABS: the game-time the script should resume at. While set, the WAIT
	// instruction re-runs each tick (the original rewinds its PC) until GameTime reaches it.
	// null = not waiting. See Handlers/Time.cs and docs/tickets/T-007.
	public int? WaitUntil { get; set; }
	public string Disassembly => rsseqFile.Disassembly;
	public List<Instruction> Instructions { get; } = new List<Instruction>();

	/// <summary>
	/// Key: String offset
	/// Value: String value
	/// </summary>
	public Dictionary<long, string> Strings { get; } = new Dictionary<long, string>();
	public List<int> Variables { get; set; } = new List<int>();
	public List<string> VariableNames { get; set; } = new List<string>();
	public VMFlags Flags { get; set; } = VMFlags.None;
	public VMConfig Config { get; set; } = new VMConfig();
	public List<Branch> Branches { get; set; } = new List<Branch>();
	public byte[] FileData { get; set; } = null!;

	public List<int> Visitors { get; set; } = new();

	// LIFO call/data stack, shared by JSR/RETURN and PUSH/POP (as on the original VM).
	public Stack<int> Stack { get; set; } = new();

	// Second LIFO stack for HUSH/HOP. The original keeps one backing buffer used as a
	// double-ended stack: PUSH/POP grow down from the top (index +0x40), HUSH/HOP grow up from
	// the bottom (index +0x44). Modelled here as an independent stack. See Handlers/Misc.cs.
	public Stack<int> HushStack { get; set; } = new();

	private Dictionary<Opcode, MethodInfo> OpcodeHandlers { get; } = Assembly.GetExecutingAssembly().GetTypes()
		.SelectMany( t => t.GetMethods() )
		.Where( x => x.GetCustomAttribute<OpcodeHandlerAttribute>() != null )
		.Select( x => (x.GetCustomAttribute<OpcodeHandlerAttribute>()!.Opcode, x) )
		.ToDictionary( x => x.Opcode, x => x.x );

	public RideVM( Stream stream )
	{
		// Parse the .RSE script: populates Variables, Strings, Instructions, Branches.
		rsseqFile = new RideScriptFile( this, stream );

		// DEBUG: Log implemented opcode counts
		var implementedOpcodes = OpcodeHandlers.Keys.ToList();
		var implementedCount = implementedOpcodes.Count;
		// The VM has exactly 106 opcodes (0..105), confirmed from the original's opcode table
		// in tp.exe (a name/operand-count array at VA 0x765280). The old "210" double-counted
		// that array's (name, operandCount) pointer pairs. See docs/tickets/T-007.
		var totalCount = 106;
		var totalPercent = (float)implementedCount / totalCount * 100;

		Log.Info( $"Implemented {implementedCount} / {totalCount} ({totalPercent.CeilToInt()}%) opcodes" );

		// Set up basic ride variables
		EnsureCommonVariables();
		Variables[(int)RideVariables.VAR_RIDECLOSED] = 1;
		Variables[(int)RideVariables.VAR_CAPACITY] = 16;
		Variables[(int)RideVariables.VAR_DURATION] = 30;
	}

	/// <summary>
	/// All rides are expected to expose the common <see cref="RideVariables"/> set. Pad
	/// the variable table if a script declares fewer (e.g. a minimal test script) so the
	/// default initialization below cannot index out of range.
	/// </summary>
	private void EnsureCommonVariables()
	{
		var required = Enum.GetValues<RideVariables>().Length;
		while ( Variables.Count < required )
			Variables.Add( 0 );
	}

	public void Step()
	{
		var instruction = Instructions[CurrentPos++];
		Log.Trace( $"Invoking {instruction.opcode} at position {CurrentPos}" );

		instruction.Invoke();
	}

	/// <summary>Set by the ENDSLICE opcode to yield the rest of this tick's slice (see <see cref="RunSlice"/>).</summary>
	public bool SliceEnded { get; set; }

	/// <summary>
	/// Runs the script for one game tick: a batch of instructions, stopping early at an <c>ENDSLICE</c>
	/// (the script's own per-tick yield) or when a blocking opcode (<c>WAIT</c>/<c>WAITANIM</c>) rewinds
	/// the PC to re-run itself next tick. The instruction cap bounds work and stops a tight loop with no
	/// yield from spinning forever within a single tick. This replaces stepping one instruction per tick,
	/// which was far too slow for the ride scripts' real-time load/run loops (see T-032).
	/// </summary>
	public void RunSlice( int maxInstructions = 256 )
	{
		for ( int n = 0; n < maxInstructions && IsRunning && CurrentPos >= 0 && CurrentPos < Instructions.Count; n++ )
		{
			int before = CurrentPos;
			SliceEnded = false;
			Step();
			if ( SliceEnded )
				break;                       // ENDSLICE: done for this tick
			if ( CurrentPos == before )
				break;                       // a WAIT/WAITANIM rewound to re-run: yield until next tick
		}
	}

	public MethodInfo? FindOpcodeHandler( Opcode opcodeId )
	{
		if ( OpcodeHandlers.TryGetValue( opcodeId, out var handlerAttribute ) )
			return handlerAttribute;

		return null;
	}

	public void CallOpcodeHandler( Opcode opcodeId, params Operand[] operands )
	{
		var handlerAttribute = FindOpcodeHandler( opcodeId );

		if ( handlerAttribute == null )
		{
			Log.Warning( $"No handler for {opcodeId}, treating as no-op" );
			return;
		}

		var parameters = new object[] { this };
		parameters = Enumerable.Concat( parameters, operands ).ToArray();

		handlerAttribute?.Invoke( null, parameters );
	}

	// Maps an instruction's byte offset to its index in Instructions, so BranchTo is an
	// O(1) lookup instead of an O(n) scan. Built lazily once the script is loaded.
	private Dictionary<long, int>? offsetToIndex;

	public void BranchTo( int value )
	{
		//
		// Branch targets are offsets in instruction-stream units (4 bytes each: one
		// opcode or operand). Convert to the byte offset of the target instruction, then
		// look up its index. (Previously an O(n) FindIndex scan per branch.)
		//
		offsetToIndex ??= Instructions
			.Select( ( instruction, index ) => (instruction.offset, index) )
			.ToDictionary( x => x.offset, x => x.index );

		var fileOffset = value * 4 + (int)Instructions.First().offset;

		if ( !offsetToIndex.TryGetValue( fileOffset, out var index ) )
		{
			// Unknown target: leave execution where it is rather than silently jumping to
			// the start (the old FindIndex returned -1, then +1 -> position 0).
			Log.Warning( $"Branch target {value} (offset {fileOffset}) not found; ignoring" );
			return;
		}

		CurrentPos = index + 1; // Ignore the leading NO-OP, as before.

		Log.Trace( $"Branching to .label_{value} / {fileOffset} (location: {CurrentPos})" );
	}

	private TimeSince TimeSinceLastTick;
	public void Update()
	{
		if ( !IsRunning || TimeSinceLastTick <= 1f / 5f )
			return;

		// Advance the ride's millisecond clock by the real elapsed time so WAIT/WAITABS actually wake
		// (GameTime was never advanced, so every WAIT hung the script at its first wait — T-032), then
		// run this tick's slice.
		GameTime += (int)( (float)TimeSinceLastTick * 1000f );
		TimeSinceLastTick = 0;
		RunSlice();
	}

	public void Run()
	{
		IsRunning = !IsRunning;
	}
}
