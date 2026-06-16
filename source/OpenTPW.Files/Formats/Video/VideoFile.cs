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

	/// <summary>The <c>pIQT</c> (TQI) video frame chunks, in order.</summary>
	public IEnumerable<Chunk> VideoChunks => Chunks.Where( c => c.IsVideo );

	/// <summary>The EA audio chunks (<c>SC*</c>), in order.</summary>
	public IEnumerable<Chunk> AudioChunks => Chunks.Where( c => c.IsAudio );

	/// <summary>Number of <c>pIQT</c> video frames.</summary>
	public int VideoFrameCount => Chunks.Count( c => c.IsVideo );

	/// <summary>Video frame dimensions (width × height), read from the first frame header.</summary>
	public readonly record struct VideoInfo( int Width, int Height );

	/// <summary>
	/// Reads the video frame size from the first <c>pIQT</c> frame header (the frame starts
	/// with little-endian uint16 width then height — confirmed: a real BF.TGQ is 320×352,
	/// matching ffprobe).
	///
	/// Decoding the frame pixels is a separate, large task: TQI is an MPEG-1-style intra
	/// DCT codec (DC/AC VLC + IDCT + 4:2:0 YUV). It must be implemented independently from
	/// the MPEG-1 spec — do NOT port FFmpeg's (LGPL) decoder into this MIT project. See T-008.
	/// </summary>
	public VideoInfo GetVideoInfo()
	{
		var frame = Chunks.FirstOrDefault( c => c.IsVideo );
		if ( frame.Type != VideoChunkType )
			throw new InvalidDataException( "Video: no pIQT video frame." );

		var payload = GetPayload( frame );
		if ( payload.Length < 4 )
			throw new InvalidDataException( "Video: pIQT frame header too small." );

		var width = BinaryPrimitives.ReadUInt16LittleEndian( payload );
		var height = BinaryPrimitives.ReadUInt16LittleEndian( payload.Slice( 2, 2 ) );
		return new VideoInfo( width, height );
	}

	/// <summary>
	/// Decodes the <paramref name="index"/>-th video frame (<c>pIQT</c>) to RGB via the
	/// reverse-engineered <see cref="TqiDecoder"/>.
	/// </summary>
	public TqiDecoder.Frame DecodeFrame( int index )
	{
		var i = 0;
		foreach ( var chunk in Chunks )
		{
			if ( !chunk.IsVideo )
				continue;
			if ( i++ == index )
				return TqiDecoder.Decode( GetPayload( chunk ) );
		}
		throw new ArgumentOutOfRangeException( nameof( index ), $"No video frame at index {index}." );
	}

	/// <summary>Whether the file contains any EA audio chunks.</summary>
	public bool HasAudio => Chunks.Any( c => c.IsAudio );

	// NB: video frames (pIQT) use the EA TQI codec (DCT-based) and are not decoded here.
	// The EA-ADPCM audio IS decoded — see DecodeAudio(). Reference:
	// https://wiki.multimedia.cx/index.php/Electronic_Arts_Formats

	/// <summary>Decoded PCM audio: interleaved 16-bit samples + channel/rate info.</summary>
	public readonly record struct DecodedAudio( int Channels, int SampleRate, int SampleCount, short[] Samples );

	// EA ADPCM predictor coefficients (FFmpeg ea_adpcm_table).
	private static readonly int[] EaAdpcmTable =
	{
		0, 240, 460, 392, 0, 0, -208, -220,
		0, 1, 3, 4, 7, 8, 10, 11,
		0, -1, -3, -4,
	};

	/// <summary>
	/// Decodes the EA-ADPCM audio track to 16-bit PCM. Reverse-engineered + verified: the
	/// decoded sample count matches the SCHl header and the waveform is coherent audio.
	/// Only stereo (the format used by TPW movies) is implemented.
	/// </summary>
	public DecodedAudio DecodeAudio()
	{
		var header = Chunks.FirstOrDefault( c => c.Type == "SCHl" );
		if ( header.Type != "SCHl" )
			throw new InvalidDataException( "Video: no SCHl audio header." );

		var (channels, sampleCount) = ParseAudioHeader( GetPayload( header ) );
		if ( channels != 2 )
			throw new NotSupportedException( $"EA-ADPCM: only stereo is implemented (channels={channels})." );

		var left = new List<short>( sampleCount );
		var right = new List<short>( sampleCount );
		foreach ( var chunk in Chunks )
		{
			if ( chunk.Type == "SCDl" )
				DecodeScdlStereo( GetPayload( chunk ), left, right );
		}

		var n = Math.Min( left.Count, right.Count );
		var samples = new short[n * 2];
		for ( var i = 0; i < n; i++ )
		{
			samples[i * 2] = left[i];
			samples[i * 2 + 1] = right[i];
		}

		// Sample rate isn't reliably in the header; TPW movies are 22.05 kHz.
		return new DecodedAudio( channels, 22050, n, samples );
	}

	// EA SCHl "PT" tag header: "PT\0\0" then tag bytes. 0xFF ends; 0xFC/0xFD/0xFE are
	// markers (no value); other tags are followed by a size byte then size big-endian bytes.
	private static (int channels, int sampleCount) ParseAudioHeader( ReadOnlySpan<byte> body )
	{
		int channels = 1, sampleCount = 0;
		var pos = 4; // skip "PT\0\0"
		while ( pos < body.Length )
		{
			var tag = body[pos++];
			if ( tag == 0xFF )
				break;
			if ( tag is 0xFC or 0xFD or 0xFE )
				continue;
			if ( pos >= body.Length )
				break;

			int size = body[pos++];
			long value = 0;
			for ( var i = 0; i < size && pos < body.Length; i++ )
				value = (value << 8) | body[pos++];

			if ( tag == 0x82 )
				channels = (int)value;       // channel count
			else if ( tag == 0x85 )
				sampleCount = (int)value;    // total samples per channel
		}
		return (channels, sampleCount);
	}

	private static void DecodeScdlStereo( ReadOnlySpan<byte> pl, List<short> left, List<short> right )
	{
		if ( pl.Length < 12 )
			return;

		var count = BinaryPrimitives.ReadInt32LittleEndian( pl );
		int prevL = BinaryPrimitives.ReadInt16LittleEndian( pl.Slice( 4, 2 ) );
		int curL = BinaryPrimitives.ReadInt16LittleEndian( pl.Slice( 6, 2 ) );
		int prevR = BinaryPrimitives.ReadInt16LittleEndian( pl.Slice( 8, 2 ) );
		int curR = BinaryPrimitives.ReadInt16LittleEndian( pl.Slice( 10, 2 ) );

		var pos = 12;
		var produced = 0;
		while ( produced < count && pos + 2 <= pl.Length )
		{
			int b0 = pl[pos], b1 = pl[pos + 1];
			pos += 2;
			int c1l = EaAdpcmTable[b0 >> 4], c2l = EaAdpcmTable[(b0 >> 4) + 4];
			int c1r = EaAdpcmTable[b0 & 0xF], c2r = EaAdpcmTable[(b0 & 0xF) + 4];
			int shiftL = 20 - (b1 >> 4), shiftR = 20 - (b1 & 0xF);

			for ( var i = 0; i < 28 && produced < count && pos < pl.Length; i++ )
			{
				int nb = pl[pos++];
				var nl = Clamp16( ((Sign4( nb >> 4 ) << shiftL) + curL * c1l + prevL * c2l + 0x80) >> 8 );
				var nr = Clamp16( ((Sign4( nb & 0xF ) << shiftR) + curR * c1r + prevR * c2r + 0x80) >> 8 );
				prevL = curL; curL = nl;
				prevR = curR; curR = nr;
				left.Add( (short)nl );
				right.Add( (short)nr );
				produced++;
			}
		}
	}

	private static int Sign4( int nibble ) => nibble >= 8 ? nibble - 16 : nibble;

	private static int Clamp16( int v ) => v < -32768 ? -32768 : v > 32767 ? 32767 : v;

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
