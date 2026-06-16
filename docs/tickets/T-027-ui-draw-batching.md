# T-027 — Renderer: batch UI draws, remove per-quad allocations

- **Priority**: 🟠 Medium (CPU/GC overhead per frame; lobby has ~22 UI draws)
- **Type**: Rendering / performance
- **Status**: ✅ Done — per-quad allocations removed, UI resource-set churn fixed, and consecutive
  same-texture UI draws merged into single `DrawIndexed` calls.
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

- ✅ **Draw merging.** `Graphics.Batch.cs` accumulates UI geometry into reusable vertex/index
  arrays; `Quad`/`DrawText` append (allocation-free) via `AppendGeometry`, which flushes on
  material/binding change or buffer overflow. `Renderer.PostRender` calls `Graphics.FlushBatch()`
  after `OnRender` and before the MSAA resolve. Consecutive same-texture UI draws (e.g. a string's
  glyphs, runs of same-texture quads) merge into one `DrawIndexed`; draw order (alpha) is preserved
  by flushing on every state change. `DrawText` no longer allocates its `List`/`ToArray` per call.
  Verified: lobby renders pixel-identical (logo, buttons + labels, cursor).

## Possible later polish (not blocking)

- Hoist `CreateScreenMatrix` to a value cached per screen size (negligible; recomputed per quad now).

## Acceptance

UI renders identically (logo, buttons + labels, cursor); UI draw count collapses to ~one per
texture run; per-frame UI heap allocations ≈ 0; build 0 warnings; tests green.

## Affected files

`source/OpenTPW/Client/Graphics.cs`, `source/OpenTPW/Client/Graphics.Text.cs`,
`source/OpenTPW/Render/Assets/Material.cs` (UI per-texture set cache),
`source/OpenTPW/Client/Renderer.cs` (`FlushBatch` hook).
