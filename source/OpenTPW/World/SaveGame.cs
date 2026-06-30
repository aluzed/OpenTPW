using System.IO;
using System.Text.Json;

namespace OpenTPW;

/// <summary>
/// A native OpenTPW save (T-059, Route A): a versioned JSON snapshot of the park — the balance + loans, the
/// in-game clock, the placed rides/shops (their catalog name, tile, rotation, ticket price, research/upgrade
/// level + mechanical condition), and the hired staff (role + position + patrol zone). Capturing + applying
/// live in <see cref="Level"/> (which owns the placement pipeline); this is the serializable shape +
/// fault-tolerant file I/O (mirrors <c>GameSettings</c>), with <see cref="SlotPath"/> save slots. Peeps are
/// transient (they respawn); the restored clock keeps the calendar/loan/challenge timing aligned.
/// </summary>
public sealed class SaveGame
{
	public int Version { get; set; } = 2;
	public float Money { get; set; }
	public float EntryFee { get; set; }
	public float ClockSeconds { get; set; }
	public List<LoanState> Loans { get; set; } = new();
	public List<Placement> Placements { get; set; } = new();
	public List<StaffState> Staff { get; set; } = new();

	public sealed class LoanState
	{
		public string Name { get; set; } = "";
		public bool Bought { get; set; }
		public float Outstanding { get; set; }
		public float Monthly { get; set; }
	}

	public sealed class Placement
	{
		public string Kind { get; set; } = "";   // "ride" | "shop"
		public string Name { get; set; } = "";   // catalog item name (totem / drink / …)
		public int TileX { get; set; }
		public int TileY { get; set; }
		public int Rotation { get; set; }
		public float TicketPrice { get; set; }

		// Per-ride progression (T-059 v2): research/upgrade level + mechanical condition, so a reloaded park
		// keeps its researched capacity bumps and a ride's reliability/breakdown state. Defaults match a
		// freshly-built ride so a v1 save (which omits these keys) loads as "level 0, fully reliable".
		public int UpgradeLevel { get; set; }
		public int ResearchedLevel { get; set; }
		public bool Researching { get; set; }
		public float ResearchFraction { get; set; }    // 0..1 progress toward the next level
		public int ResearchQueuePos { get; set; } = -1; // park-wide research-queue order (-1 = not queued)
		public float Reliability { get; set; } = 1f;
		public bool Broken { get; set; }
	}

	/// <summary>A hired staff member (T-059 v2): role + wander centre + optional patrol zone. Staff aren't
	/// grid-reserved (they roam), so they're snapshot/respawned separately from the ride/shop placements.</summary>
	public sealed class StaffState
	{
		public string Role { get; set; } = "";    // StaffRole name (Entertainer/Handyman/Guard/Mechanic/Researcher)
		public float X { get; set; }
		public float Y { get; set; }
		public bool HasZone { get; set; }
		public float ZoneX { get; set; }
		public float ZoneY { get; set; }
		public float ZoneRadius { get; set; }
	}

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	/// <summary>How many save slots the UI offers (T-059).</summary>
	public const int SlotCount = 3;

	/// <summary>The file path for save slot <paramref name="slot"/> (1..<see cref="SlotCount"/>, clamped).</summary>
	public static string SlotPath( int slot ) => Path.Combine( ".opentpw", $"save{Math.Clamp( slot, 1, SlotCount )}.json" );

	/// <summary>True if slot <paramref name="slot"/> holds a save on disk (for the slot UI).</summary>
	public static bool SlotExists( int slot ) => File.Exists( SlotPath( slot ) );

	/// <summary>Default save-slot path under the OpenTPW cache directory (slot 1).</summary>
	public static string DefaultPath => SlotPath( 1 );

	public string ToJson() => JsonSerializer.Serialize( this, JsonOptions );

	public static SaveGame? FromJson( string json )
	{
		try { return JsonSerializer.Deserialize<SaveGame>( json ); }
		catch { return null; }
	}

	/// <summary>Write this save to <paramref name="path"/> (best-effort; logs + swallows IO errors).</summary>
	public void WriteToFile( string path )
	{
		try
		{
			var dir = Path.GetDirectoryName( path );
			if ( !string.IsNullOrEmpty( dir ) )
				Directory.CreateDirectory( dir );
			File.WriteAllText( path, ToJson() );
			Log.Info( $"[save] wrote {Placements.Count} placement(s) to {path}" );
		}
		catch ( Exception e ) { Log.Warning( $"[save] write failed: {e.Message}" ); }
	}

	/// <summary>Read a save from <paramref name="path"/>, or null if it's missing/unreadable.</summary>
	public static SaveGame? ReadFromFile( string path )
	{
		try { return File.Exists( path ) ? FromJson( File.ReadAllText( path ) ) : null; }
		catch ( Exception e ) { Log.Warning( $"[save] read failed: {e.Message}" ); return null; }
	}
}
