using OpenTPW;

// Minimal CLI to list and extract Theme Park World WAD (DWFB) archives, using the
// engine's own WadArchive reader (handles the directory tree + Refpack decompression).
//
//   wadtool <archive.wad>                  list contents
//   wadtool <archive.wad> -x [<out-dir>]   extract all files (default: <name>_extracted)

if ( args.Length < 1 )
{
	Console.Error.WriteLine( "Usage:" );
	Console.Error.WriteLine( "  wadtool <archive.wad>                  list contents" );
	Console.Error.WriteLine( "  wadtool <archive.wad> -x [<out-dir>]   extract all files" );
	return 1;
}

var wadPath = args[0];
if ( !File.Exists( wadPath ) )
{
	Console.Error.WriteLine( $"File not found: {wadPath}" );
	return 1;
}

var extract = args.Contains( "-x" );
var outDir = args.Length > 2 && !args[2].StartsWith( "-" )
	? args[2]
	: Path.GetFileNameWithoutExtension( wadPath ) + "_extracted";

var archive = new WadArchive( wadPath );

var fileCount = 0;
long totalBytes = 0;

void Walk( ArchiveDirectory dir, string relPath )
{
	foreach ( var child in dir.Children )
	{
		switch ( child )
		{
			case ArchiveDirectory sub:
				Walk( sub, Combine( relPath, sub.Name ) );
				break;

			case ArchiveFile file:
				var rel = Combine( relPath, file.Name );
				var data = file.GetData();
				fileCount++;
				totalBytes += data.Length;

				if ( extract )
				{
					var outPath = Path.Combine( outDir, rel );
					Directory.CreateDirectory( Path.GetDirectoryName( outPath )! );
					File.WriteAllBytes( outPath, data );
				}
				else
				{
					Console.WriteLine( $"{data.Length,12}  {rel}" );
				}
				break;
		}
	}
}

static string Combine( string a, string? b )
	=> string.IsNullOrEmpty( a ) ? b ?? "" : $"{a}/{b}";

Walk( archive.Root, "" );

if ( extract )
	Console.WriteLine( $"Extracted {fileCount} files ({totalBytes:N0} bytes) to {outDir}/" );
else
	Console.WriteLine( $"\n{fileCount} files, {totalBytes:N0} bytes uncompressed" );

return 0;
