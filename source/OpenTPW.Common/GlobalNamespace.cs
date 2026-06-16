global using static OpenTPW.Common.GlobalNamespace;

namespace OpenTPW.Common;
public static class GlobalNamespace
{
	public static Logger Log { get; set; } = null!;
	public static BaseFileSystem FileSystem { get; set; } = null!;
	public static BaseFileSystem SaveFileSystem { get; set; } = null!;
	public static BaseFileSystem CacheFileSystem { get; set; } = null!;
}
