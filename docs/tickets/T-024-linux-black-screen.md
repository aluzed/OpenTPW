# T-024 — Linux: the window renders black (scene not visibly drawn)

- **Priority**: 🟠 Medium (the game runs but shows nothing)
- **Type**: Rendering
- **Status**: ☐ To do — needs interactive (on-screen) investigation.
- **Follow-up of**: [T-023](T-023-linux-vulkan-libdl.md).

## Symptom

After the [T-023](T-023-linux-vulkan-libdl.md) fix the game **runs** on Linux (window titled
"Theme Park World", Vulkan device created, render loop running, no crash), but the window stays
**black** — the jungle level isn't visibly rendered.

## Likely causes (to check at the machine)

- The MSAA framebuffer → swapchain **blit/present** not landing (clear-only frame).
- The level/camera: nothing in view, or the level didn't finish loading what's needed to draw.
- Shader cross-compile/pipeline issue on this Vulkan/Mesa stack (check for SPIRV/pipeline logs).
- Missing assets: `Data/` was extracted **without `Movies/`** for the test; verify the jungle
  level doesn't need anything that was skipped.

## How to investigate

Run with a real `OPENTPW_GAMEPATH`, watch the console log (shader compile, asset load, render
warnings), and bisect: clear to a non-black colour to confirm present works, then check whether
draw calls are issued and the camera/level are set up. This is display-dependent, so it's best
done interactively rather than headless.

## Affected files

`source/OpenTPW/Client/Renderer.cs` (swapchain/blit), `source/OpenTPW/World/Level.cs`,
`source/OpenTPW/Render/*`.
