using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// Decodes a Theme Park World sprite image (<c>.TPC</c> / <c>.FPC</c> from <c>esprites.wad</c>).
/// Format reverse-engineered from the original loader (see docs/tickets/T-034):
/// <code>
///   u16 fmt (=3), u16 (=3), u32 frameCount
///   if fmt==3:  byte palette[1024]            // 256 entries, stored B,G,R,A
///   per frame:  u32 dataLen, u16 w, u16 h, u16, u16, s32 hotspotX, s32 hotspotY
///               byte data[dataLen]            // h scanlines of signed-byte RLE
/// </code>
/// Each scanline is prefixed by its byte length, then signed control bytes until <c>width</c> pixels:
/// <c>c &lt; 0</c> → a run of <c>|c|</c> pixels of palette index = the next byte (index 0 = transparent);
/// <c>c &gt; 0</c> → <c>c</c> literal palette indices. Frames are decoded to straight RGBA.
/// </summary>
public sealed class TpcFile
{
	public sealed class Frame
	{
		public int Width { get; init; }
		public int Height { get; init; }
		public int HotspotX { get; init; }
		public int HotspotY { get; init; }
		/// <summary>Straight RGBA, row-major, <c>Width*Height*4</c> bytes (alpha 0 = transparent).</summary>
		public byte[] Rgba { get; init; } = System.Array.Empty<byte>();
	}

	public int FrameCount => Frames.Count;
	public IReadOnlyList<Frame> Frames { get; }

	public TpcFile( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		Frames = Parse( ms.ToArray() );
	}

	public TpcFile( byte[] data ) => Frames = Parse( data );

	private static List<Frame> Parse( byte[] d )
	{
		var frames = new List<Frame>();
		int p = 0;
		ushort fmt = BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( p ) ); p += 2;
		p += 2; // second u16 (also 3)
		uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian( d.AsSpan( p ) ); p += 4;

		// 256-entry BGRA palette (only present for fmt 3).
		byte[]? palette = null;
		if ( fmt == 3 )
		{
			palette = d.AsSpan( p, 1024 ).ToArray();
			p += 1024;
		}

		for ( int i = 0; i < frameCount && p + 20 <= d.Length; i++ )
		{
			uint dataLen = BinaryPrimitives.ReadUInt32LittleEndian( d.AsSpan( p ) );
			ushort w = BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( p + 4 ) );
			ushort h = BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( p + 6 ) );
			short hotspotX = (short)BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( p + 0x14 ) );
			short hotspotY = (short)BinaryPrimitives.ReadUInt16LittleEndian( d.AsSpan( p + 0x16 ) );
			p += 20;
			int pixStart = p;
			p += (int)dataLen;

			frames.Add( new Frame
			{
				Width = w,
				Height = h,
				HotspotX = hotspotX,
				HotspotY = hotspotY,
				Rgba = (w == 0 || h == 0 || palette == null)
					? System.Array.Empty<byte>()
					: DecodeFrame( d, pixStart, (int)dataLen, w, h, palette ),
			} );
		}

		return frames;
	}

	// Decodes one frame's signed-RLE scanlines into straight RGBA via the BGRA palette.
	private static byte[] DecodeFrame( byte[] d, int off, int len, int w, int h, byte[] palette )
	{
		var rgba = new byte[w * h * 4];
		int q = off;
		int end = off + len;

		for ( int row = 0; row < h && q < end; row++ )
		{
			int rowLen = d[q++];          // scanline byte length (also used for row-skipping in the original)
			int rowEnd = q + rowLen;
			int x = 0;
			while ( x < w && q < rowEnd )
			{
				sbyte c = (sbyte)d[q++];
				if ( c < 0 )              // run of |c| pixels of one palette index
				{
					int n = -c;
					byte idx = d[q++];
					for ( int j = 0; j < n && x < w; j++ )
						WritePixel( rgba, (row * w + x++) * 4, idx, palette );
				}
				else if ( c > 0 )         // c literal palette indices
				{
					for ( int j = 0; j < c && x < w && q < rowEnd; j++ )
						WritePixel( rgba, (row * w + x++) * 4, d[q++], palette );
				}
				// c == 0: empty span (no-op)
			}
			q = rowEnd;                   // resync to the next scanline
		}

		return rgba;
	}

	// Palette index → straight RGBA (palette is B,G,R,A); index 0 is fully transparent.
	private static void WritePixel( byte[] rgba, int o, byte index, byte[] palette )
	{
		if ( index == 0 )
			return; // leave transparent (already zeroed)
		int b = index * 4;
		rgba[o] = palette[b + 2];     // R
		rgba[o + 1] = palette[b + 1]; // G
		rgba[o + 2] = palette[b];     // B
		rgba[o + 3] = 255;            // opaque
	}
}
