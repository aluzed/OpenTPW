using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World lip-sync files (<c>.LIP</c> / <c>.LIPS</c>, found under
/// <c>SPEECH/LIPS/</c>).
///
/// No published spec exists; reverse-engineered from samples. The file is a flat list of
/// little-endian <see cref="uint"/> mouth keyframe timestamps (monotonically
/// non-decreasing), terminated by <c>0xFFFFFFFF</c>. The first keyframe is 0.
///
/// The timestamp unit is <b>microseconds</b> (verified: for every <c>sp_001.LIP</c> on the
/// disc, the last keyframe read as microseconds lands just under the companion
/// <c>speechHD.SDT</c> clip's duration — jungle 28.58 s vs 28.63 s, fantasy 21.96 vs 22.15,
/// hallow 26.53 vs 26.59, space 23.89 vs 23.93). See docs/tickets/T-008.
/// </summary>
public sealed class LipSyncFile : BaseFormat
{
	private const uint Terminator = 0xFFFFFFFF;

	/// <summary>Number of timestamp units per second (the timestamps are in microseconds).</summary>
	public const long UnitsPerSecond = 1_000_000;

	/// <summary>Mouth keyframe timestamps (microseconds), in file order (terminator excluded).</summary>
	public List<uint> Keyframes { get; } = new();

	/// <summary>The time of the last keyframe (≈ the speech clip's length); zero if empty.</summary>
	public TimeSpan Duration => Keyframes.Count == 0 ? TimeSpan.Zero : TimeOf( Keyframes[^1] );

	/// <summary>Converts a raw keyframe timestamp (microseconds) to a <see cref="TimeSpan"/>.</summary>
	public static TimeSpan TimeOf( uint timestamp ) => TimeSpan.FromMicroseconds( timestamp );

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
