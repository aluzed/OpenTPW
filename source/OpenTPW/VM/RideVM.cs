using System.Reflection;

namespace OpenTPW;

public partial class RideVM
{
	private readonly RideScriptFile rsseqFile;

	public string ScriptName { get; set; } = "Unnamed";

	public bool IsRunning { get; set; }
	public int CurrentPos { get; set; }
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
		var totalCount = 210; // Total number, see https://opentpw.gu3.me/formats/rsse-vm-instructions.html
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
		if ( IsRunning && TimeSinceLastTick > 1f / 5f )
		{
			Step();
			TimeSinceLastTick = 0;
		}
	}

	public void Run()
	{
		IsRunning = !IsRunning;
	}
}
