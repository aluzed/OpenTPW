using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// Program entry point
/// </summary>
public class Program
{
	public static void Main( string[] args )
	{
		if ( OperatingSystem.IsLinux() )
			FixupLinuxNativeLibraries();

		Game.Run( args );
	}

	/// <summary>
	/// Modern glibc (≥ 2.34) folded <c>libdl</c> into libc, so only <c>libdl.so.2</c> exists.
	/// The bundled Vulkan binding still imports a bare <c>libdl</c> and fails to start the Vulkan
	/// backend on current Linux distros. Redirect those imports to the real library so the game
	/// runs on Linux without needing a system symlink.
	/// </summary>
	private static void FixupLinuxNativeLibraries()
	{
		// The Veldrid Vulkan binding lives in vk.dll (namespace Vulkan); it (and NativeLibraryLoader)
		// declare the bare [DllImport("libdl")] that fails on modern glibc.
		foreach ( var assemblyName in new[] { "vk", "NativeLibraryLoader", "Veldrid" } )
		{
			try
			{
				NativeLibrary.SetDllImportResolver( Assembly.Load( assemblyName ), ResolveLinuxLibrary );
			}
			catch
			{
				// Assembly not loadable / a resolver is already set — nothing to do.
			}
		}
	}

	private static IntPtr ResolveLinuxLibrary( string libraryName, Assembly assembly, DllImportSearchPath? searchPath )
	{
		if ( libraryName is "libdl" or "libdl.so" )
		{
			foreach ( var candidate in new[] { "libdl.so.2", "libc.so.6" } )
			{
				if ( NativeLibrary.TryLoad( candidate, out var handle ) )
					return handle;
			}
		}

		// Fall back to the default resolution for everything else.
		return IntPtr.Zero;
	}
}
