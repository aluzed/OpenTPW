# T-026 — Renderer: per-frame GPU resource churn freezes the lobby

- **Priority**: 🔴 High (the lobby is unusable — frames take seconds, WM shows "not responding")
- **Type**: Rendering / performance
- **Status**: ☐ To do
- **Follow-up of**: [T-024](T-024-linux-black-screen.md) (the lobby *renders*; this is about it not running).

## Symptom

Once the lobby is **already displayed** (post-load), the scene is heavily stuttering: focusing the
window immediately triggers the window manager's "not responding" modal that takes ~10s to clear.
A live 3D scene behind a vsync render loop should run smoothly (~60 fps). It does not, because each
frame takes **seconds**.

## Root cause

The render loop itself is fine (`Renderer.Run` → `Update`, vsync on via `SyncToVerticalBlank`,
events pumped every frame, **no `sleep`**). The cost is per-draw waste introduced by the upstream
re-implementation. Audited on the lobby (~62 model draws + ~22 UI draws per frame):

1. **Synchronous GPU submit per uniform bind.** `Material.Set<T>` (uniform buffers) calls
   `Render.ImmediateSubmit`, which creates a brand-new `CommandList`, begins/ends it and calls
   `Device.SubmitCommands` — a **full GPU queue submit** — once per model per frame (~62/frame).
   This is the dominant cost. (`source/OpenTPW/Render/Assets/Material.cs` `Set<T>`,
   `source/OpenTPW/Client/Renderer.cs` `ImmediateSubmit`.)
2. **Resource-set churn.** Every draw calls `Material.CreateEphemeralResourceSet`, which calls
   `Device.ResourceFactory.CreateResourceSet(...)` and schedules it for disposal — ~84 GPU
   descriptor-set create + destroy per frame. (`Material.cs`, `Model.cs` `Draw`.)

Refinements that make the fix safe:
- `ui.shader` has **no uniform buffer**, so #1 never touches UI — only the 62 model draws.
- Each `LobbyIsland` mesh constructs its **own** `Material` instance, drawn **once per frame**, and
  binds its textures **once at construction**. So a persistent per-material uniform buffer and a
  cached resource set are correct **without** ring buffers or dynamic offsets.
- Veldrid `CommandList.UpdateBuffer` is ordered in the command stream at record time; a
  `ResourceSet` references its buffer (not a snapshot). One `UpdateBuffer` + one `Draw` per material
  per frame, recorded on the frame command list, is **bit-identical** output to the current code.

## To do

1. ✅/☐ `Material.Set<T>`: drop `ImmediateSubmit`; record `Render.CommandList.UpdateBuffer` on the
   frame command list into a **persistent** per-material uniform buffer (rename `ScratchBuffer` →
   `UniformBuffer`, size it to the real struct rounded up to 16, not the fixed 2048). Bind it into
   `_boundResources` once. Document the invariant: **one draw per material per frame**.
2. ☐ Replace `CreateEphemeralResourceSet` with `GetResourceSets()` returning a cached
   `ResourceSet[]`, invalidated by a `_resourceVersion` bumped on (a) a texture `Set` that changes a
   binding and (b) `SetupResources` (shader recompile). Uniform `Set<T>` must **not** invalidate.
   `Model.Draw` calls `GetResourceSets()`; old sets disposed via the deletion queue.
3. ☐ Remove the now-dead `ClearBoundResources`/`DestroyResourceSets` per-frame scheduling.

## Risks

- A cached `ResourceSet` references a `TextureView`; if a texture is recreated/disposed the cached
  set dangles → GPU crash. Invalidate via `_resourceVersion` on texture `Set` and defer set disposal
  through the deletion queue so an in-flight frame isn't using a freed set.
- The persistent-buffer approach is only correct while each model material is drawn once per frame.
  If a material is ever reused for multiple entities in a frame, switch it to a ring/dynamic-offset
  buffer. Guard with a code comment.
- `Set<T>` now writes `Render.CommandList`, valid only between `PreRender`/`PostRender`; all current
  callers run inside `OnRender`.

## Acceptance

Lobby runs at vsync (~16 ms/frame) with no "not responding"; rendering identical to before; build
0 warnings; `OpenTPW.Tests` green.

## Affected files

`source/OpenTPW/Render/Assets/Material.cs` (`Set<T>`, resource-set cache, `SetupResources`),
`source/OpenTPW/Render/Assets/Model.cs` (`Draw`), `source/OpenTPW/Client/Renderer.cs`
(`ImmediateSubmit` out of the hot path), `source/OpenTPW/Client/Renderer.Deletion.cs`.
