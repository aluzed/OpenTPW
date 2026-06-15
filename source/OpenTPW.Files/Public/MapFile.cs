namespace OpenTPW;

/// <summary>
/// Reader for Theme Park World <c>.MAP</c> files.
///
/// Despite the extension, these are <b>not</b> terrain/tile maps — every <c>.MAP</c> on
/// the disc is a <c>CAT_*</c> file under SOUND / MUSIC / SPEECH directories: an audio
/// <b>category catalog</b>. Each file begins with a 16-byte COM class GUID identifying the
/// catalog type (DirectMusic family <c>{e9612c0?-31d0-11d2-b409-00?0c993f203}</c>),
/// followed by category fields and length-prefixed entry strings (sound names / paths,
/// e.g. "Music\Music").
///
/// Only the leading GUID is decoded here (confirmed); the per-category entry layout
/// varies by type and is left as raw <see cref="Data"/>. See docs/tickets/T-012.
/// (The previous "TileType" terrain interpretation was incorrect.)
/// </summary>
public sealed class MapFile : BaseFormat
{
	/// <summary>The catalog's COM class GUID (identifies the category type).</summary>
	public Guid CategoryType { get; private set; }

	/// <summary>The raw bytes following the GUID header (entry data, not yet decoded).</summary>
	public byte[] Data { get; private set; } = Array.Empty<byte>();

	public MapFile( string path ) => ReadFromFile( path );

	public MapFile( Stream stream ) => ReadFromStream( stream );

	protected override void ReadFromStream( Stream stream )
	{
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		var bytes = ms.ToArray();

		if ( bytes.Length < 16 )
			throw new InvalidDataException( $"MAP: file too small ({bytes.Length} bytes) for a category GUID." );

		CategoryType = new Guid( bytes.AsSpan( 0, 16 ) );
		Data = bytes[16..];
	}
}
