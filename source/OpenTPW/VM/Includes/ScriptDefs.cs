namespace OpenTPW;

/// <summary>
/// Animations, objects, etc.
/// </summary>
public class ScriptDefs
{
	public enum Animations
	{
		// Animations
		ANIM_Create = 0,
		ANIM_Idle = 2,
		ANIM_Load = 3,
		ANIM_Start = 4,
		ANIM_Main = 5,
		ANIM_End = 6,
		ANIM_Unload = 7,
		ANIM_Break = 9,
		ANIM_Repair = 10,
		ANIM_Other = 11,
	}

	public enum Effects
	{
		// Particle / sound effects
		OBJ_PTCL = 1,
		OBJ_SOUND_LOC_AMB = 1,
		OBJ_PTCL_D = 2,
		OBJ_SOUND_LOC_RID = 3,
		OBJ_SOUND_GLO_RID = 5,
		OBJ_SOUND_GLO_KID = 6,
		OBJ_SOUND_GLO_STA = 7,
		OBJ_SOUND_GLO_AMB = 8,
		OBJ_SOUND_GLO_UI = 9,
		OBJ_SOUND_GLO_BMP = 11,
	}

	public enum Coaster
	{
		// Rollercoaster specific
		COAST_ADDPEEP = 1,
		COAST_GETQUEUE = 2,
		COAST_GETPEEP = 3,
		COAST_SETBROKE = 4,
		COAST_SETCLOSED = 5,
		COAST_SETCAPACITY = 6,
		COAST_SETWORN = 7,
		COAST_INITIALISE = 8,

		SPACE_ENGINE_REVS = 16,
		BUS_CTRL = 20,
	}

	public enum Walk
	{
		// WALKON actions
		WALK_ACTION_VANISH = 1,
		WALK_ACTION_BEAM = 2,
		WALK_ACTION_HEAD = 4,
		WALK_ACTION_THROW = 5,
		WALK_ACTION_CHEER = 6,
	}

	// BUMP (op_54) subcommands — the bumper/kart multiplexer (DinoKart, Space Water Ride). Indices RE'd
	// from the executor's jump table at 0x5546f5; names from the called bumper-car methods + log strings.
	// Queries (write the VM result register): ADDPEEP, GETLOADCAR, ADDCAR, GETMODE, CARSONRIDE, GETSTATE,
	// REMOVECAR.
	public enum Bumper
	{
		BUMP_ADDPEEP = 1,      // "Peep %d added to next car to be loaded" — returns whether a car was free
		BUMP_GETLOADCAR = 2,   // query the next car being loaded
		BUMP_STARTRIDE = 3,    // "Start Bump Ride" — release the loaded cars
		BUMP_ADDCAR = 4,       // spawn a car if cars-on-ride < max; returns the new car
		BUMP_GETMODE = 5,      // read ride field +0x2c
		BUMP_RESET = 6,
		BUMP_OPENRIDE = 7,     // "Open ride %d"
		BUMP_HALT_A = 8,       // arg!=0 → halt path A, else stop
		BUMP_HALT_B = 9,       // arg!=0 → halt path B, else stop
		BUMP_CLOSERIDE = 10,
		BUMP_CARSONRIDE = 11,  // returns the live car count (ride field +0x17)
		BUMP_GETSTATE = 12,
		BUMP_SETLAPS = 13,     // set lap target (arg × 30)
		BUMP_SETTIME = 14,     // set a negative timer (−arg)
		BUMP_REMOVECAR = 16,   // destroy a car (arg!=0 → only empties); returns whether one was removed
		BUMP_SETOPEN = 17      // toggle the ride's open/closed visual state
	}

	// TOUR (op_53) subcommands — the tour-ride multiplexer. Indices RE'd from the jump table at 0x5542fe,
	// dispatching onto a tour-ride object class (FUN_0055a620 create / FUN_0055d3d0 destroy + per-car
	// query/setter helpers). Queries (write the VM result register): 3, 4, 10, 11, 15, 16.
	public enum Tour
	{
		TOUR_INITIALISE = 1,   // allocate + register the tour-ride object
		TOUR_SHUTDOWN = 2      // tear it down and free it
		// 3..18: per-car position/animation queries (3,4,10,11,15,16) and setters (5,8,9,12,14,17,18)
	}
}
