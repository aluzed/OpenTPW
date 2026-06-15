using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World video files (<c>.TQI</c> / <c>.TGQ</c>).
///
/// TPW uses the EA multimedia container: a flat sequence of FourCC blocks, each
/// <c>{ 4-byte type, 4-byte little-endian size (including this 8-byte preamble), payload }</c>.
/// Video frames are <c>pIQT</c> (TQI codec); audio uses the EA <c>SC*</c> chunk family.
///
/// This parses the container layout (chunk index, frame count, audio presence). Decoding
/// the TQI video / EA-ADPCM audio payloads is a separate step. See docs/tickets/T-008.
/// Format reference: https://wiki.multimedia.cx/index.php/Electronic_Arts_Formats
/// </summary>
public sealed class VideoFile : BaseFormat
{
	private const int PreambleSize = 8;
	private const string VideoChunkType = "pIQT";

	private static readonly HashSet<string> AudioChunkTypes = new()
	{
		"SCHl", "SCCl", "SCDl", "SCLl", "SCEl", "SHEN", "SCEN", "SDEN", "SEEN",
	};

	/// <summary>A single FourCC block within the container.</summary>
	public readonly struct Chunk
	{
		/// <summary>The 4-character block type (FourCC), e.g. "pIQT" or "SCHl".</summary>
		public string Type { get; init; }

		/// <summary>Byte offset of the block (its FourCC) within the file.</summary>
		public long Offset { get; init; }

		/// <summary>Total block size in bytes, including the 8-byte preamble.</summary>
		public int Size { get; init; }

		/// <summary>Length of the payload (block size minus the preamble).</summary>
		public int PayloadLength => Size - PreambleSize;

		/// <summary>Byte offset of the payload within the file.</summary>
		public long PayloadOffset => Offset + PreambleSize;

		public bool IsVideo => Type == VideoChunkType;
		public bool IsAudio => AudioChunkTypes.Contains( Type );
	}

	private byte[] data = Array.Empty<byte>();

	/// <summary>All FourCC blocks, in file order.</summary>
	public List<Chunk> Chunks { get; } = new();

	/// <summary>Number of <c>pIQT</c> video frames.</summary>
	public int VideoFrameCount => Chunks.Count( c => c.IsVideo );

	/// <summary>Whether the file contains any EA audio chunks.</summary>
	public bool HasAudio => Chunks.Any( c => c.IsAudio );

	public VideoFile( string path ) => ReadFromFile( path );

	public VideoFile( Stream stream ) => ReadFromStream( stream );

	/// <summary>Returns the raw payload bytes of a chunk.</summary>
	public ReadOnlySpan<byte> GetPayload( Chunk chunk )
		=> data.AsSpan( (int)chunk.PayloadOffset, chunk.PayloadLength );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		data = ms.ToArray();

		var pos = 0;
		while ( pos + PreambleSize <= data.Length )
		{
			var type = Encoding.ASCII.GetString( data, pos, 4 );
			var size = BinaryPrimitives.ReadInt32LittleEndian( data.AsSpan( pos + 4, 4 ) );

			// A size below the preamble would loop forever; a size past EOF means the
			// stream is truncated or not an EA container. Stop and keep what we have.
			if ( size < PreambleSize || pos + size > data.Length )
			{
				Log.Warning( $"Video: malformed chunk '{type}' (size {size}) at offset {pos}; stopping." );
				break;
			}

			Chunks.Add( new Chunk { Type = type, Offset = pos, Size = size } );
			pos += size;
		}
	}
}
