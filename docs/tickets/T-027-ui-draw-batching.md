# T-027 — Renderer: batch UI draws, remove per-quad allocations

- **Priority**: 🟠 Medium (CPU/GC overhead per frame; lobby has ~22 UI draws)
- **Type**: Rendering / performance
- **Status**: ☐ To do
- **Follow-up of**: [T-026](T-026-render-resource-churn.md).

## Problem

`Graphics.Quad` (`source/OpenTPW/Client/Graphics.cs`) allocates a `List<Vertex>`, a `List<uint>`
and two `.ToArray()` per quad, then writes a **single shared** dynamic vertex/index buffer and
issues its own `DrawIndexed`. Because every quad overwrites the same buffer, quads cannot batch —
each is its own `UpdateBuffer` + draw. ~150 heap allocations/frame from the UI alone → GC pressure
and many tiny draws. `Graphics.DrawText` already batches a string's glyphs into one draw, but still
rebinds its texture and reuses the same shared buffer (so interleaving `Quad` and `DrawText` in a
frame would clobber).

## To do

1. ☐ Give `Graphics` dedicated batch buffers (separate from model buffers). `Quad`/`DrawText`
   **append** into reusable arrays (no `List`, no `.ToArray()`; grow with `ArrayPool`/`Array.Resize`,
   bounded by `MaxVertexCount`/`MaxIndexCount`).
2. ☐ Flush (`Graphics.FlushBatch()` → one `DrawIndexed`) when: the incoming draw uses a different
   texture/material, the batch would overflow, or end-of-frame. Add the end-of-frame flush hook in
   `Renderer.PostRender` **immediately after `OnRender?.Invoke()`** and **before** the MSAA resolve.
3. ☐ Add a per-texture resource-set cache on `Material.UI` (it rebinds `"Color"` per widget), keyed
   by the bound `TextureView`. Invalidate an entry when its texture is disposed.
4. ☐ Preserve child draw order so alpha blending / overlap stays correct (flush-on-texture-change
   already preserves order). Hoist `CreateScreenMatrix` to a cached value (recompute on resize only).

## Acceptance

UI renders identically (logo, buttons + labels, cursor); UI draw count collapses to ~one per
texture run; per-frame UI heap allocations ≈ 0; build 0 warnings; tests green.

## Affected files

`source/OpenTPW/Client/Graphics.cs`, `source/OpenTPW/Client/Graphics.Text.cs`,
`source/OpenTPW/Render/Assets/Material.cs` (UI per-texture set cache),
`source/OpenTPW/Client/Renderer.cs` (`FlushBatch` hook).
