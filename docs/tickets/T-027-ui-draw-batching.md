# T-027 — Renderer: batch UI draws, remove per-quad allocations

- **Priority**: 🟠 Medium (CPU/GC overhead per frame; lobby has ~22 UI draws)
- **Type**: Rendering / performance
- **Status**: ⚠️ Partial — per-quad allocations removed + UI resource-set churn fixed; draw-call
  merging still to do.
- **Follow-up of**: [T-026](T-026-render-resource-churn.md).

## Problem

`Graphics.Quad` (`source/OpenTPW/Client/Graphics.cs`) allocates a `List<Vertex>`, a `List<uint>`
and two `.ToArray()` per quad, then writes a **single shared** dynamic vertex/index buffer and
issues its own `DrawIndexed`. Because every quad overwrites the same buffer, quads cannot batch —
each is its own `UpdateBuffer` + draw. ~150 heap allocations/frame from the UI alone → GC pressure
and many tiny draws. `Graphics.DrawText` already batches a string's glyphs into one draw, but still
rebinds its texture and reuses the same shared buffer (so interleaving `Quad` and `DrawText` in a
frame would clobber).

## Done

- ✅ **Per-quad allocations removed.** `Graphics.Quad` reused a `new List<Vertex>` + `new List<uint>`
  + two `.ToArray()` every call (~20 quads/frame). Now writes into static reusable 4-vertex / 6-index
  arrays — zero heap allocation per quad. (`source/OpenTPW/Client/Graphics.cs`.)
- ✅ **UI resource-set churn fixed.** After T-026 the shared UI material still rebuilt its `ResourceSet`
  every draw because it rebinds `"Color"` per widget. `Material` now caches resource sets in a
  dictionary keyed by the bound-resource identities (`ComputeBindingKey`, allocation-free), so each
  distinct UI texture combination is built once and reused across frames; model materials (stable
  bindings) hit a single entry. Cleared + disposed on shader recompile. (`Render/Assets/Material.cs`.)

## To do

1. ☐ Actually **merge** draws: accumulate consecutive same-texture quads/text into one growing
   vertex/index buffer and flush in as few `DrawIndexed` as possible (currently each quad/string is
   still its own `UpdateBuffer` + draw on the shared buffer). Add an end-of-frame `Graphics.FlushBatch()`
   in `Renderer.PostRender` **after `OnRender?.Invoke()`** and **before** the MSAA resolve; flush on
   texture/material change or overflow; preserve child draw order (alpha blending).
2. ☐ `Graphics.DrawText` still allocates its vertex/index `List` + `.ToArray()` per call — fold into
   the same batch buffers. Hoist `CreateScreenMatrix` to a cached value (recompute on resize only).

## Acceptance

UI renders identically (logo, buttons + labels, cursor); UI draw count collapses to ~one per
texture run; per-frame UI heap allocations ≈ 0; build 0 warnings; tests green.

## Affected files

`source/OpenTPW/Client/Graphics.cs`, `source/OpenTPW/Client/Graphics.Text.cs`,
`source/OpenTPW/Render/Assets/Material.cs` (UI per-texture set cache),
`source/OpenTPW/Client/Renderer.cs` (`FlushBatch` hook).
