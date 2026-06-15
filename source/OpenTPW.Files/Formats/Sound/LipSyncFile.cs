using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World lip-sync files (<c>.LIP</c> / <c>.LIPS</c>, found under
/// <c>SPEECH/LIPS/</c>).
///
/// No published spec exists; reverse-engineered from samples. The file is a flat list of
/// little-endian <see cref="uint"/> mouth keyframe timestamps (monotonically
/// non-decreasing), terminated by <c>0xFFFFFFFF</c>. The first keyframe is 0; the unit is
/// not pinned down (audio ticks/sample-relative). See docs/tickets/T-008.
/// </summary>
public sealed class LipSyncFile : BaseFormat
{
	private const uint Terminator = 0xFFFFFFFF;

	/// <summary>Mouth keyframe timestamps, in file order (terminator excluded).</summary>
	public List<uint> Keyframes { get; } = new();

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
