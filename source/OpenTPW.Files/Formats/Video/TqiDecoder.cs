using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// Decoder for a single Theme Park World TQI video frame (the <c>pIQT</c> chunk payload).
///
/// TQI is an EA intra-only DCT codec, MPEG-1-style. Reverse-engineered and verified
/// against a reference frame (decodes the Bullfrog logo pixel-accurately). Pipeline:
/// <list type="number">
///   <item>8-byte frame header: uint16 width, uint16 height, byte quant (@4); bitstream @8.</item>
///   <item>The bitstream is byte-swapped in 32-bit words before MSB-first bit reading.</item>
///   <item>Row-major macroblocks, 6 blocks each (4:2:0 — Y0..3, Cb, Cr); DC predictors
///         reset to 0 at frame start; each block is MPEG-1 intra (DC + AC run/level VLC).</item>
///   <item>Dequantize, IDCT, assemble Y/Cb/Cr planes, convert to RGB.</item>
/// </list>
///
/// The VLC tables, the flattened-tree Huffman reader and the integer IDCT are adapted from
/// jsmpeg (MIT, © Dominic Szablewski) — compatible with this project's MIT licence; the VLC
/// values are ISO MPEG-1 facts. The dequantization approximates EA's AAN qtable
/// (qscale derived from the header quant); see docs/tickets/T-008.
/// </summary>
public static class TqiDecoder
{
	/// <summary>A decoded frame: <see cref="Rgb"/> is row-major 24-bit RGB, width × height.</summary>
	public readonly record struct Frame( int Width, int Height, byte[] Rgb );

	/// <summary>Decodes a <c>pIQT</c> frame payload to RGB.</summary>
	public static Frame Decode( ReadOnlySpan<byte> payload )
	{
		if ( payload.Length < 8 )
			throw new InvalidDataException( "TQI: frame payload too small for the header." );

		var width = BinaryPrimitives.ReadUInt16LittleEndian( payload );
		var height = BinaryPrimitives.ReadUInt16LittleEndian( payload.Slice( 2, 2 ) );
		int quant = payload[4];
		// EA derives qscale = (215 - 2*quant)*5; scaled here to match the standard MPEG-1
		// dequant path used below (calibrated against the reference frame).
		var qscale = Math.Max( 1, (215 - 2 * quant) * 5 / 10 );

		var mbW = (width + 15) / 16;
		var mbH = (height + 15) / 16;
		var codedW = mbW * 16;
		var codedH = mbH * 16;

		// Byte-swap the bitstream (32-bit words) from offset 8.
		var raw = payload[8..].ToArray();
		var swapped = new byte[(raw.Length + 3) & ~3];
		raw.CopyTo( swapped, 0 );
		for ( var i = 0; i + 4 <= swapped.Length; i += 4 )
			(swapped[i], swapped[i + 1], swapped[i + 2], swapped[i + 3]) =
				(swapped[i + 3], swapped[i + 2], swapped[i + 1], swapped[i]);

		var bits = new BitReader( swapped );
		var block = new int[64];
		var y = new int[codedW * codedH];
		var cb = new int[(codedW / 2) * (codedH / 2)];
		var cr = new int[(codedW / 2) * (codedH / 2)];
		var chromaW = codedW / 2;
		var dc = new int[3]; // Y, Cb, Cr predictors (reset to 0)

		for ( var my = 0; my < mbH; my++ )
		{
			for ( var mx = 0; mx < mbW; mx++ )
			{
				for ( var bi = 0; bi < 6; bi++ )
				{
					var n = DecodeBlock( bits, block, bi, dc, qscale );

					if ( bi < 4 )
					{
						var baseIdx = (my * 16 + (bi / 2) * 8) * codedW + mx * 16 + (bi & 1) * 8;
						Place( block, n, y, baseIdx, codedW );
					}
					else
					{
						var plane = bi == 4 ? cb : cr;
						var baseIdx = (my * 8) * chromaW + mx * 8;
						Place( block, n, plane, baseIdx, chromaW );
					}
				}
			}
		}

		return new Frame( width, height, YuvToRgb( y, cb, cr, width, height, codedW, chromaW ) );
	}

	private static void Place( int[] block, int n, int[] dest, int baseIdx, int stride )
	{
		if ( n == 1 )
		{
			var v = (block[0] + 128) >> 8;
			for ( var yy = 0; yy < 8; yy++ )
				for ( var xx = 0; xx < 8; xx++ )
					dest[baseIdx + yy * stride + xx] = v;
		}
		else
		{
			Idct( block );
			for ( var yy = 0; yy < 8; yy++ )
				for ( var xx = 0; xx < 8; xx++ )
					dest[baseIdx + yy * stride + xx] = block[yy * 8 + xx];
		}
	}

	private static int DecodeBlock( BitReader bits, int[] block, int bi, int[] dc, int qscale )
	{
		Array.Clear( block );

		int predictor, size;
		if ( bi < 4 )
		{
			predictor = dc[0];
			size = ReadHuffman( bits, DcSizeLuminance );
		}
		else
		{
			predictor = bi == 4 ? dc[1] : dc[2];
			size = ReadHuffman( bits, DcSizeChrominance );
		}

		if ( size > 0 )
		{
			var diff = bits.Read( size );
			block[0] = (diff & (1 << (size - 1))) != 0
				? predictor + diff
				: predictor + ((-1 << size) | (diff + 1));
		}
		else
		{
			block[0] = predictor;
		}

		if ( bi < 4 ) dc[0] = block[0];
		else if ( bi == 4 ) dc[1] = block[0];
		else dc[2] = block[0];

		block[0] <<= 8;
		var n = 1;

		while ( true )
		{
			var coeff = ReadHuffman( bits, DctCoeff );
			if ( coeff == 0x0001 && n > 0 && bits.Read1() == 0 )
				break; // end of block

			int run, level;
			if ( coeff == 0xffff )
			{
				run = bits.Read( 6 );
				level = bits.Read( 8 );
				if ( level == 0 ) level = bits.Read( 8 );
				else if ( level == 128 ) level = bits.Read( 8 ) - 256;
				else if ( level > 128 ) level -= 256;
			}
			else
			{
				run = coeff >> 8;
				level = coeff & 0xff;
				if ( bits.Read1() != 0 ) level = -level;
			}

			n += run;
			if ( n >= 64 ) break;
			var zz = ZigZag[n];
			n++;

			var lv = (level * 2 * qscale * IntraQuant[zz]) >> 4;
			if ( (lv & 1) == 0 ) lv -= lv > 0 ? 1 : -1;
			lv = Math.Clamp( lv, -2048, 2047 );
			block[zz] = lv * Premultiplier[zz];
		}

		return n;
	}

	private static int ReadHuffman( BitReader bits, int[] table )
	{
		var state = 0;
		do
		{
			state = table[state + bits.Read1()];
		}
		while ( state >= 0 && table[state] != 0 );
		return table[state + 2];
	}

	// jsmpeg's integer IDCT (TU Chemnitz), in place.
	private static void Idct( int[] b )
	{
		for ( var i = 0; i < 8; i++ )
		{
			int b1 = b[32 + i], b3 = b[16 + i] + b[48 + i], b4 = b[40 + i] - b[24 + i];
			int t1 = b[8 + i] + b[56 + i], t2 = b[24 + i] + b[40 + i], b6 = b[8 + i] - b[56 + i], b7 = t1 + t2, m0 = b[i];
			var x4 = ((b6 * 473 - b4 * 196 + 128) >> 8) - b7;
			var x0 = x4 - (((t1 - t2) * 362 + 128) >> 8);
			var x1 = m0 - b1;
			var x2 = (((b[16 + i] - b[48 + i]) * 362 + 128) >> 8) - b3;
			var x3 = m0 + b1;
			int y3 = x1 + x2, y4 = x3 + b3, y5 = x1 - x2, y6 = x3 - b3, y7 = -x0 - ((b4 * 473 + b6 * 196 + 128) >> 8);
			b[i] = b7 + y4; b[8 + i] = x4 + y3; b[16 + i] = y5 - x0; b[24 + i] = y6 - y7;
			b[32 + i] = y6 + y7; b[40 + i] = x0 + y5; b[48 + i] = y3 - x4; b[56 + i] = y4 - b7;
		}
		for ( var i = 0; i < 64; i += 8 )
		{
			int b1 = b[4 + i], b3 = b[2 + i] + b[6 + i], b4 = b[5 + i] - b[3 + i];
			int t1 = b[1 + i] + b[7 + i], t2 = b[3 + i] + b[5 + i], b6 = b[1 + i] - b[7 + i], b7 = t1 + t2, m0 = b[i];
			var x4 = ((b6 * 473 - b4 * 196 + 128) >> 8) - b7;
			var x0 = x4 - (((t1 - t2) * 362 + 128) >> 8);
			var x1 = m0 - b1;
			var x2 = (((b[2 + i] - b[6 + i]) * 362 + 128) >> 8) - b3;
			var x3 = m0 + b1;
			int y3 = x1 + x2, y4 = x3 + b3, y5 = x1 - x2, y6 = x3 - b3, y7 = -x0 - ((b4 * 473 + b6 * 196 + 128) >> 8);
			b[i] = (b7 + y4 + 128) >> 8; b[1 + i] = (x4 + y3 + 128) >> 8; b[2 + i] = (y5 - x0 + 128) >> 8; b[3 + i] = (y6 - y7 + 128) >> 8;
			b[4 + i] = (y6 + y7 + 128) >> 8; b[5 + i] = (x0 + y5 + 128) >> 8; b[6 + i] = (y3 - x4 + 128) >> 8; b[7 + i] = (y4 - b7 + 128) >> 8;
		}
	}

	private static byte[] YuvToRgb( int[] y, int[] cb, int[] cr, int width, int height, int codedW, int chromaW )
	{
		var rgb = new byte[width * height * 3];
		for ( var py = 0; py < height; py++ )
		{
			for ( var px = 0; px < width; px++ )
			{
				var yy = Math.Clamp( y[py * codedW + px], 0, 255 );
				var u = Math.Clamp( cb[(py / 2) * chromaW + (px / 2)], 0, 255 ) - 128;
				var v = Math.Clamp( cr[(py / 2) * chromaW + (px / 2)], 0, 255 ) - 128;
				var i = (py * width + px) * 3;
				rgb[i] = ClampByte( yy + 1.402 * v );
				rgb[i + 1] = ClampByte( yy - 0.344 * u - 0.714 * v );
				rgb[i + 2] = ClampByte( yy + 1.772 * u );
			}
		}
		return rgb;
	}

	private static byte ClampByte( double v ) => (byte)Math.Clamp( (int)(v + 0.5), 0, 255 );

	// MSB-first bit reader.
	private sealed class BitReader
	{
		private readonly byte[] data;
		private int pos;

		public BitReader( byte[] data ) => this.data = data;

		public int Read1()
		{
			var by = (pos >> 3) < data.Length ? data[pos >> 3] : 0;
			var bit = (by >> (7 - (pos & 7))) & 1;
			pos++;
			return bit;
		}

		public int Read( int n )
		{
			var v = 0;
			for ( var i = 0; i < n; i++ )
				v = (v << 1) | Read1();
			return v;
		}
	}

	// VLC tables (flattened Huffman trees [left, right, value]) from jsmpeg (MIT).
	private static readonly int[] DcSizeLuminance =
	{
		6, 3, 0, 18, 15, 0, 9, 12, 0, 0, 0, 1, 0, 0, 2, 27,
		24, 0, 21, 30, 0, 0, 0, 0, 36, 33, 0, 0, 0, 4, 0, 0,
		3, 39, 42, 0, 0, 0, 5, 0, 0, 6, 48, 45, 0, 51, -1, 0,
		0, 0, 7, 0, 0, 8,
	};
	private static readonly int[] DcSizeChrominance =
	{
		6, 3, 0, 12, 9, 0, 18, 15, 0, 24, 21, 0, 0, 0, 2, 0,
		0, 1, 0, 0, 0, 30, 27, 0, 0, 0, 3, 36, 33, 0, 0, 0,
		4, 42, 39, 0, 0, 0, 5, 48, 45, 0, 0, 0, 6, 51, -1, 0,
		0, 0, 7, 0, 0, 8,
	};
	private static readonly int[] DctCoeff =
	{
		3, 6, 0, 12, 9, 0, 0, 0, 1, 21, 24, 0, 18, 15, 0, 39, 27, 0,
		33, 30, 0, 42, 36, 0, 0, 0, 257, 60, 66, 0, 54, 63, 0, 48, 57, 0,
		0, 0, 513, 51, 45, 0, 0, 0, 2, 0, 0, 3, 81, 75, 0, 87, 93, 0,
		72, 78, 0, 96, 90, 0, 0, 0, 1025, 69, 84, 0, 0, 0, 769, 0, 0, 258,
		0, 0, 1793, 0, 0, 65535, 0, 0, 1537, 111, 108, 0, 0, 0, 1281, 105, 102, 0,
		117, 114, 0, 99, 126, 0, 120, 123, 0, 156, 150, 0, 162, 159, 0, 144, 147, 0,
		129, 135, 0, 138, 132, 0, 0, 0, 2049, 0, 0, 4, 0, 0, 514, 0, 0, 2305,
		153, 141, 0, 165, 171, 0, 180, 168, 0, 177, 174, 0, 183, 186, 0, 0, 0, 2561,
		0, 0, 3329, 0, 0, 6, 0, 0, 259, 0, 0, 5, 0, 0, 770, 0, 0, 2817,
		0, 0, 3073, 228, 225, 0, 201, 210, 0, 219, 213, 0, 234, 222, 0, 216, 231, 0,
		207, 192, 0, 204, 189, 0, 198, 195, 0, 243, 261, 0, 273, 240, 0, 246, 237, 0,
		249, 258, 0, 279, 276, 0, 252, 255, 0, 270, 282, 0, 264, 267, 0, 0, 0, 515,
		0, 0, 260, 0, 0, 7, 0, 0, 1026, 0, 0, 1282, 0, 0, 4097, 0, 0, 3841,
		0, 0, 3585, 315, 321, 0, 333, 342, 0, 312, 291, 0, 375, 357, 0, 288, 294, 0,
		-1, 369, 0, 285, 303, 0, 318, 363, 0, 297, 306, 0, 339, 309, 0, 336, 348, 0,
		330, 300, 0, 372, 345, 0, 351, 366, 0, 327, 354, 0, 360, 324, 0, 381, 408, 0,
		417, 420, 0, 390, 378, 0, 435, 438, 0, 384, 387, 0, 0, 0, 2050, 396, 402, 0,
		465, 462, 0, 0, 0, 8, 411, 399, 0, 429, 432, 0, 453, 414, 0, 426, 423, 0,
		0, 0, 10, 0, 0, 9, 0, 0, 11, 0, 0, 5377, 0, 0, 1538, 0, 0, 771,
		0, 0, 5121, 0, 0, 1794, 0, 0, 4353, 0, 0, 4609, 0, 0, 4865, 444, 456, 0,
		0, 0, 1027, 459, 450, 0, 0, 0, 261, 393, 405, 0, 0, 0, 516, 447, 441, 0,
		516, 519, 0, 486, 474, 0, 510, 483, 0, 504, 498, 0, 471, 537, 0, 507, 501, 0,
		522, 513, 0, 534, 531, 0, 468, 477, 0, 492, 495, 0, 549, 546, 0, 525, 528, 0,
		0, 0, 263, 0, 0, 2562, 0, 0, 2306, 0, 0, 5633, 0, 0, 5889, 0, 0, 6401,
		0, 0, 6145, 0, 0, 1283, 0, 0, 772, 0, 0, 13, 0, 0, 12, 0, 0, 14,
		0, 0, 15, 0, 0, 517, 0, 0, 6657, 0, 0, 262, 540, 543, 0, 480, 489, 0,
		588, 597, 0, 0, 0, 27, 609, 555, 0, 606, 603, 0, 0, 0, 19, 0, 0, 22,
		591, 621, 0, 0, 0, 18, 573, 576, 0, 564, 570, 0, 0, 0, 20, 552, 582, 0,
		0, 0, 21, 558, 579, 0, 0, 0, 23, 612, 594, 0, 0, 0, 25, 0, 0, 24,
		600, 615, 0, 0, 0, 31, 0, 0, 30, 0, 0, 28, 0, 0, 29, 0, 0, 26,
		0, 0, 17, 0, 0, 16, 567, 618, 0, 561, 585, 0, 654, 633, 0, 0, 0, 37,
		645, 648, 0, 0, 0, 36, 630, 636, 0, 0, 0, 34, 639, 627, 0, 663, 666, 0,
		657, 624, 0, 651, 642, 0, 669, 660, 0, 0, 0, 35, 0, 0, 267, 0, 0, 40,
		0, 0, 268, 0, 0, 266, 0, 0, 32, 0, 0, 264, 0, 0, 265, 0, 0, 38,
		0, 0, 269, 0, 0, 270, 0, 0, 33, 0, 0, 39, 0, 0, 7937, 0, 0, 6913,
		0, 0, 7681, 0, 0, 4098, 0, 0, 7425, 0, 0, 7169, 0, 0, 271, 0, 0, 274,
		0, 0, 273, 0, 0, 272, 0, 0, 1539, 0, 0, 2818, 0, 0, 3586, 0, 0, 3330,
		0, 0, 3074, 0, 0, 3842,
	};
	private static readonly int[] ZigZag =
	{
		0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
		12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
		35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
		58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
	};
	private static readonly int[] IntraQuant =
	{
		8, 16, 19, 22, 26, 27, 29, 34, 16, 16, 22, 24, 27, 29, 34, 37,
		19, 22, 26, 27, 29, 34, 34, 38, 22, 22, 26, 27, 29, 34, 37, 40,
		22, 26, 27, 29, 32, 35, 40, 48, 26, 27, 29, 32, 35, 40, 48, 58,
		26, 27, 29, 34, 38, 46, 56, 69, 27, 29, 35, 38, 46, 56, 69, 83,
	};
	private static readonly int[] Premultiplier =
	{
		32, 44, 42, 38, 32, 25, 17, 9, 44, 62, 58, 52, 44, 35, 24, 12,
		42, 58, 55, 49, 42, 33, 23, 12, 38, 52, 49, 44, 38, 30, 20, 10,
		32, 44, 42, 38, 32, 25, 17, 9, 25, 35, 33, 30, 25, 20, 14, 7,
		17, 24, 23, 20, 17, 14, 9, 5, 9, 12, 12, 10, 9, 7, 5, 2,
	};
}

