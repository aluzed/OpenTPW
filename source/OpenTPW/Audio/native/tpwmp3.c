// Thin C ABI over minimp3 (lieff/minimp3, public domain / CC0) for OpenTPW game audio (T-031).
// NLayer mis-decoded the game's MPEG-2 Layer II music (dropped ~12% of frames); minimp3 decodes
// MPEG-1/2 Layer I/II/III correctly. Build: see build.sh. Loaded via P/Invoke ("tpwmp3").
#define MINIMP3_IMPLEMENTATION
#include "minimp3_ex.h"
#include <stdlib.h>
#include <string.h>

// Decodes an entire MPEG audio buffer. On success returns 0 and sets *out to a malloc'd interleaved
// int16 PCM buffer (free it with tpw_mp3_free), plus total sample count (across channels), channel
// count and sample rate. Returns nonzero on failure.
int tpw_mp3_decode( const unsigned char *data, int size,
                    short **out, int *samples, int *channels, int *hz )
{
    mp3dec_t dec;
    mp3dec_init( &dec );
    mp3dec_file_info_t info;
    memset( &info, 0, sizeof( info ) );
    if ( mp3dec_load_buf( &dec, data, (size_t)size, &info, 0, 0 ) != 0 )
        return -1;
    *out = info.buffer;
    *samples = (int)info.samples;
    *channels = info.channels;
    *hz = info.hz;
    return 0;
}

void tpw_mp3_free( short *p ) { free( p ); }
