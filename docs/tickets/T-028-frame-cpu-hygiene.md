# T-028 — Renderer: per-frame CPU hygiene (asset scan, timing)

- **Priority**: ⚪ Technical debt (cheap wins; lands with [T-026](T-026-render-resource-churn.md))
- **Type**: Code quality / performance
- **Status**: ✅ Done — dirty-shader queue drains in `PreRender` (no per-frame `Asset.All` scan), and
  frame timing uses a `Stopwatch`.

## Problem

Two small per-frame inefficiencies in `source/OpenTPW/Client/Renderer.cs`:

1. `PreRender` runs `Asset.All.OfType<Shader>().Where( x => x.IsDirty )` **every frame** — a LINQ
   allocation + a full scan of *all* loaded assets (textures, models, materials, shaders) just to
   find shaders flagged dirty by the hot-reload `FileSystemWatcher`.
2. Frame timing uses `DateTime.Now` (wall-clock, can jump, slower) instead of a monotonic
   `Stopwatch`.

## To do

1. ☐ Add a thread-safe dirty-shader registry on `Shader` (the `FileSystemWatcher.Changed` callback
   runs on a worker thread): `OnWatcherChanged` flags dirty **and** enqueues `this`; `Recompile`
   clears it. `Renderer.PreRender` drains the registry instead of scanning `Asset.All`.
2. ☐ Replace the `DateTime.Now` delta in `Renderer.Update` with a `Stopwatch` started in the ctor.

## Acceptance

No per-frame `Asset.All` scan or `DateTime.Now`; hot-reload still recompiles edited shaders; build
0 warnings; tests green.

## Affected files

`source/OpenTPW/Render/Assets/Shader.cs`, `source/OpenTPW/Client/Renderer.cs`.
