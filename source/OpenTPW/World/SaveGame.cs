using System.IO;
using System.Text.Json;

namespace OpenTPW;

/// <summary>
/// A native OpenTPW save (T-059, Route A): a versioned JSON snapshot of the park — the balance + loans, the
/// in-game clock, and the placed rides/shops (their catalog name, tile, rotation, ticket price). Capturing +
/// applying live in <see cref="Level"/> (which owns the placement pipeline); this is the serializable shape +
/// fault-tolerant file I/O (mirrors <c>GameSettings</c>). Peeps are transient (they respawn), and staff +
/// fine progression (research-in-progress, the active challenge) are a follow-up — the restored clock keeps
/// the calendar/loan/challenge timing aligned.
/// </summary>
public sealed class SaveGame
{
	public int Version { get; set; } = 1;
	public float Money { get; set; }
	public float EntryFee { get; set; }
	public float ClockSeconds { get; set; }
	public List<LoanState> Loans { get; set; } = new();
	public List<Placement> Placements { get; set; } = new();

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
	}

	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

	/// <summary>Default save-slot path under the OpenTPW cache directory.</summary>
	public static string DefaultPath => Path.Combine( ".opentpw", "save1.json" );

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
