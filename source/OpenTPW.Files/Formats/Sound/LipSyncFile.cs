using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// A Theme Park World advisor mouth shape (viseme). Codes RE'd from the mouth-mesh selector
/// <c>FUN_0044b2e0</c> in the no-CD <c>tp.exe</c>, which maps a shape code to a named mesh part
/// ("mouth - normal/aah/eee/ooh/sss"); 0 = mouth closed, 1..5 = the five visemes.
/// </summary>
public enum MouthShape
{
	Closed = 0,
	Normal = 1,
	Aah = 2,
	Eee = 3,
	Ooh = 4,
	Sss = 5,
}

/// <summary>Helpers for <see cref="MouthShape"/>.</summary>
public static class MouthShapeExtensions
{
	/// <summary>
	/// The advisor model's named sub-mesh that the engine shows for this viseme — RE'd verbatim from the
	/// mouth-mesh selector <c>FUN_0044b2e0</c> (it string-matches the model's parts against these names).
	/// <see cref="MouthShape.Closed"/> has no part (the mouth is hidden); the others map to the
	/// "mouth - *" parts. (The whole-head part is "bug head".)
	/// </summary>
	public static string? MeshPartName( this MouthShape shape ) => shape switch
	{
		MouthShape.Normal => "mouth - normal",
		MouthShape.Aah => "mouth - aah",
		MouthShape.Eee => "mouth - eee",
		MouthShape.Ooh => "mouth - ooh",
		MouthShape.Sss => "mouth - sss",
		_ => null, // Closed → no mouth part shown
	};
}

/// <summary>
/// Reader for Theme Park World lip-sync files (<c>.LIP</c> / <c>.LIPS</c>, found under
/// <c>SPEECH/LIPS/</c>).
///
/// No published spec exists; reverse-engineered from samples. The file is a flat list of
/// little-endian <see cref="uint"/> mouth keyframe timestamps (monotonically non-decreasing),
/// terminated by <c>0xFFFFFFFF</c>.
///
/// The timestamp unit is <b>microseconds</b> (verified: for every <c>sp_001.LIP</c> on the
/// disc, the last keyframe read as microseconds lands just under the companion
/// <c>speechHD.SDT</c> clip's duration — jungle 28.58 s vs 28.63 s, fantasy 21.96 vs 22.15,
/// hallow 26.53 vs 26.59, space 23.89 vs 23.93).
///
/// <para><b>Mouth shapes are NOT in the file</b> (T-020). Empirically each keyframe is a bare
/// timestamp — the values use all 24+ low bits for the time, with no spare high bits, and the file is
/// exactly <c>N×u32 + terminator</c> (no room for a parallel shape stream). The five visemes
/// (<see cref="MouthShape"/>) exist in the engine (<c>tp.exe FUN_0044b2e0</c>) and are chosen at
/// runtime; the <c>.LIP</c> only marks <i>when</i> the mouth changes (phoneme boundaries), not
/// <i>which</i> shape. <see cref="ShapeAt"/> therefore cycles the visemes deterministically as an
/// engine-side stand-in for driving a character in sync. See docs/tickets/T-020.</para>
/// </summary>
public sealed class LipSyncFile : BaseFormat
{
	private const uint Terminator = 0xFFFFFFFF;

	/// <summary>Number of timestamp units per second (the timestamps are in microseconds).</summary>
	public const long UnitsPerSecond = 1_000_000;

	/// <summary>How many open-mouth visemes the engine has (normal/aah/eee/ooh/sss); 0 = closed.</summary>
	public const int VisemeCount = 5;

	/// <summary>Mouth keyframe timestamps (microseconds), in file order (terminator excluded).</summary>
	public List<uint> Keyframes { get; } = new();

	/// <summary>The time of the last keyframe (≈ the speech clip's length); zero if empty.</summary>
	public TimeSpan Duration => Keyframes.Count == 0 ? TimeSpan.Zero : TimeOf( Keyframes[^1] );

	/// <summary>Converts a raw keyframe timestamp (microseconds) to a <see cref="TimeSpan"/>.</summary>
	public static TimeSpan TimeOf( uint timestamp ) => TimeSpan.FromMicroseconds( timestamp );

	/// <summary>
	/// The active keyframe interval at <paramref name="time"/> (0-based: interval <c>k</c> spans
	/// <c>[Keyframes[k], Keyframes[k+1])</c>), or -1 before the first keyframe or at/after the last.
	/// </summary>
	public int IntervalAt( TimeSpan time )
	{
		var us = time.TotalMicroseconds;
		if ( Keyframes.Count < 2 || us < Keyframes[0] || us >= Keyframes[^1] )
			return -1;

		// Keyframes are monotonic and short lists — a linear scan is plenty.
		for ( var k = Keyframes.Count - 2; k >= 0; k-- )
			if ( us >= Keyframes[k] )
				return k;
		return -1;
	}

	/// <summary>
	/// The mouth shape to show at <paramref name="time"/>. The file marks only phoneme boundaries, so the
	/// viseme is an engine choice — we cycle the five visemes by interval (deterministic), and return
	/// <see cref="MouthShape.Closed"/> before the first / at-or-after the last keyframe.
	/// </summary>
	public MouthShape ShapeAt( TimeSpan time )
	{
		var interval = IntervalAt( time );
		return interval < 0 ? MouthShape.Closed : (MouthShape)(1 + interval % VisemeCount);
	}

	public LipSyncFile( string path ) => ReadFromFile( path );

	public LipSyncFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var data = ms.ToArray();

		for ( var pos = 0; pos + 4 <= data.Length; pos += 4 )
		{
			var value = BinaryPrimitives.ReadUInt32LittleEndian( data.AsSpan( pos, 4 ) );
			if ( value == Terminator )
				break;

			Keyframes.Add( value );
		}
	}
}
