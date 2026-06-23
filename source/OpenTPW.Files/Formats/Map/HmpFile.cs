using System;
using System.Collections.Generic;
using System.IO;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World height-map / footprint templates (<c>.HMP</c>) — the per-piece placement
/// templates used by rides, coaster track pieces, queue paths, hoardings (fences), sideshows and upgrades.
/// RE'd from samples + the loader <c>FUN_004629d0</c> in the no-CD <c>tp.exe</c>. See docs/tickets/T-052.
///
/// <code>
///   0x00 : u16   version       (3)
///   0x02 : u32   magic         (0x0005AB1E on every file)
///   0x06 : u16   scale         (100 — units per tile)
///   0x08 : u16   cols          (footprint width, in tiles)
///   0x0a : u16   rows          (footprint depth, in tiles)
///   0x0c : u32   tileDataOff   (= 0x30: start of the per-tile detail block)
///   0x10 : u32   codeGridOff   (offset of the per-tile code grid, cols*rows bytes)
///   0x14 : u32   footprintOff  (offset of the per-tile footprint grid, cols*rows bytes)
///   0x18 : 3×f32 anchor/bounds (origin + a height extent; mostly 0)
///   ...
///   0x30 : per tile, a 25-byte (5×5) sub-grid of height/slope codes (same code family as the .map
///          attribute maps), cols*rows tiles in row-major order
///   codeGridOff  : cols*rows bytes — one summary type/height code per tile
///   footprintOff : cols*rows bytes — one footprint byte per tile (1 = solid/occupied, 0 = passable)
/// </code>
/// Verified against <c>coaster1.hmp</c> (2×3, footprint all 1 = the station), <c>StdPylon.hmp</c> (1×1),
/// <c>questra.hmp</c> (queue, 1×1) and <c>ho_fos1.hmp</c> (fence, 2×2, footprint all 0 = passable).
/// </summary>
public sealed class HmpFile : BaseFormat
{
	public const uint Magic = 0x0005AB1E;

	/// <summary>Per-tile sub-grid edge length (each tile carries a 5×5 height/code grid → 25 bytes).</summary>
	public const int TileGrid = 5;
	private const int TileRecord = TileGrid * TileGrid; // 25

	/// <summary>Header version word (observed 3).</summary>
	public int Version { get; private set; }
	/// <summary>Units per tile (observed 100).</summary>
	public int Scale { get; private set; }
	/// <summary>Footprint width in tiles.</summary>
	public int Cols { get; private set; }
	/// <summary>Footprint depth in tiles.</summary>
	public int Rows { get; private set; }
	/// <summary>Anchor / bounds floats from the header (origin + height extent; often 0).</summary>
	public float[] Anchor { get; private set; } = Array.Empty<float>();

	/// <summary>Per-tile 5×5 height/slope code sub-grids, row-major (length <see cref="Cols"/>×<see cref="Rows"/>).</summary>
	public IReadOnlyList<byte[]> Tiles => tiles;
	private readonly List<byte[]> tiles = new();

	/// <summary>Per-tile summary type/height code (one byte per tile, row-major).</summary>
	public byte[] CodeGrid { get; private set; } = Array.Empty<byte>();

	/// <summary>Per-tile footprint (one byte per tile, row-major): non-zero = solid/occupied tile.</summary>
	public byte[] Footprint { get; private set; } = Array.Empty<byte>();

	/// <summary>True if the tile at (col,row) is solid/occupied per the footprint grid.</summary>
	public bool IsSolid( int col, int row ) =>
		col >= 0 && col < Cols && row >= 0 && row < Rows && Footprint[row * Cols + col] != 0;

	public HmpFile( string path ) => ReadFromFile( path );
	public HmpFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var d = ms.ToArray();
		if ( d.Length < 0x30 )
			throw new InvalidDataException( "HMP: file smaller than the 0x30 header." );

		int U16( int o ) => d[o] | d[o + 1] << 8;
		uint U32( int o ) => (uint)(d[o] | d[o + 1] << 8 | d[o + 2] << 16 | d[o + 3] << 24);
		float F32( int o ) => BitConverter.ToSingle( d, o );

		var magic = U32( 0x02 );
		if ( magic != Magic )
			throw new InvalidDataException( $"HMP: bad magic 0x{magic:X8}, expected 0x{Magic:X8}." );

		Version = U16( 0x00 );
		Scale = U16( 0x06 );
		Cols = U16( 0x08 );
		Rows = U16( 0x0a );
		int tileDataOff = (int)U32( 0x0c );
		int codeGridOff = (int)U32( 0x10 );
		int footprintOff = (int)U32( 0x14 );
		Anchor = new[] { F32( 0x18 ), F32( 0x1c ), F32( 0x20 ) };

		int count = Cols * Rows;
		if ( Cols <= 0 || Rows <= 0 || count > 4096 )
			throw new InvalidDataException( $"HMP: implausible footprint {Cols}×{Rows}." );

		// Per-tile 5×5 detail records.
		for ( int t = 0; t < count; t++ )
		{
			int o = tileDataOff + t * TileRecord;
			var rec = new byte[TileRecord];
			if ( o + TileRecord <= d.Length )
				Array.Copy( d, o, rec, 0, TileRecord );
			tiles.Add( rec );
		}

		// The two per-tile summary grids (clamped to the file).
		CodeGrid = ReadGrid( d, codeGridOff, count );
		Footprint = ReadGrid( d, footprintOff, count );
	}

	private static byte[] ReadGrid( byte[] d, int off, int count )
	{
		var g = new byte[count];
		if ( off > 0 && off + count <= d.Length )
			Array.Copy( d, off, g, 0, count );
		return g;
	}
}
