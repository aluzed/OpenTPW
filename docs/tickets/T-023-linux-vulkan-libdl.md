# T-023 — Linux runtime: Vulkan backend fails to load (`libdl`)

- **Priority**: 🔴 High (Linux runtime — the game didn't start at all)
- **Type**: Portability
- **Status**: ✅ **Done.** The game now boots and runs on Linux.

## Problem

On a clean Linux run the process threw before opening usefully:

```
System.TypeInitializationException: The type initializer for 'Vulkan.VulkanNative' threw …
 ---> System.DllNotFoundException: Unable to load shared library 'libdl' …
   at Vulkan.Libdl.dlerror()
   at Vulkan.VulkanNative..cctor()
   at Veldrid.GraphicsDevice.CreateVulkan(...)
   at OpenTPW.Renderer.CreateGraphicsDevice()
```

The Veldrid Vulkan binding (`vk.dll`, namespace `Vulkan`) has a bare `[DllImport("libdl")]`.
Since **glibc 2.34** `libdl` was folded into libc — only `libdl.so.2` exists — so the bare
`libdl` lookup fails on every current distro, and the Vulkan device can't be created. (Vulkan
itself is fine: the ICDs, e.g. `radeonsi`/`lavapipe`, are present.)

## Fix

`Program.Main` registers a `NativeLibrary.SetDllImportResolver` on the binding assemblies
(`vk`, `NativeLibraryLoader`, `Veldrid`) that redirects `libdl` → `libdl.so.2` (then
`libc.so.6`). Linux-only; no system symlink needed. See `source/OpenTPW/Program.cs`.

## Verification

Imported the disc's `Data/` (minus Movies) to a game path and ran on an AMD Radeon Pro WX 3200
(Mesa 25.2, Vulkan): the window opens (titled "Theme Park World"), the Vulkan device is
created, and the render loop runs without crashing (confirmed by a screenshot + a 45 s run).

## Follow-up

The window currently renders **black** (the scene isn't visibly drawn). That's a separate
rendering-correctness issue (clear-only frame / level not drawing / blit) → [T-024](T-024-linux-black-screen.md).
