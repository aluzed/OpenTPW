# Native render loop & frame pacing (Ghidra) — T-029

How the original *Theme Park World* (`tp.exe`, the unencrypted no-CD build — see
[05-ghidra-reverse.md](05-ghidra-reverse.md) for why the disc binary can't be used) drives its
window, renders a frame, and paces itself. The goal is to **validate the OpenTPW renderer's
architecture** against the original (the upstream C# re-implementation had per-draw GPU resource
churn — fixed in [T-026](tickets/T-026-render-resource-churn.md)/[T-027](tickets/T-027-ui-draw-batching.md)).

> **Method.** PE import table + string analysis (`objdump -p/-x`, `strings`) on `tp.exe`
> (PE32, x86, 11 sections, 3.6 MB), cross-checked with a Ghidra 12.1 auto-analysis pass. The import
> names and the DirectX error-string tables are conclusive about the API and the loop shape; this
> doc cites that concrete evidence rather than guessing.

## Graphics API: DirectDraw + Direct3D Immediate Mode (DX6/7), with a software fallback

Imported DLLs (`objdump -p`): `DDRAW.dll`, `DINPUT.dll`, `DSOUND.dll`, `WINMM.dll`, `GDI32`,
`USER32`, `KERNEL32`, `ADVAPI32`, `WSOCK32`, `ole32`, `IMM32`, `USP10`, `QMIXER.dll`, and a family
of EA online libs (`weavoter/weachatr/weaauthr/weanewsr/weacityr/weauploadr/wearasr/weamailr.dll` —
the long-dead "Wireplay"/EA online service).

There is **no `D3DIM.dll`/`D3D8.dll`** import — because in **DirectX 6/7 Direct3D Immediate Mode
lived inside `ddraw.dll`** (you get an `IDirect3D` by `QueryInterface` on the `IDirectDraw`). The
binary's embedded error tables make the API unambiguous:

- The **`D3DERR_EXECUTE_*` family** (`D3DERR_EXECUTE_CREATE_FAILED`, `_LOCK_FAILED`, `_CLIPPED_FAILED`,
  `_UNLOCK_FAILED`, …) plus `D3DERR_MATRIX_*`, `D3DERR_MATERIAL_*`, `D3DERR_TEXTURE_*`,
  `D3DERR_SETVIEWPORTDATA_FAILED` — this is the **DX6 Direct3D Immediate Mode "execute buffer" API**
  (`IDirect3DDevice::Execute`), where a frame is built as a buffer of vertices + state/draw opcodes
  and submitted in one call.
- `"Direct3D initialized succesfully"`, `"A hardware-only DirectDraw object creation was attempted
  but the driver did not support any hardware."`, `" -- Detected Voodoo1, failing HAL init."`,
  `" -- Detected PowerVR1, failing HAL init."` → it initialises a **Direct3D HAL** device
  (hardware), and explicitly blacklists early 3dfx Voodoo1 / PowerVR1 from the HAL path.

**Software fallback.** The binary also embeds a full software rasteriser:
`"Initializing Software renderer"`, `"Software renderer initialized succesfully"`, `"Failed to init
software renderer."`, and the author tag **`"MMX Software Renderer, by Martin Griffiths, 1998/9
(fast sub-pixel triangles huh?!?!)"`**. So TPW ships **two renderers** — a Direct3D HAL path and an
MMX software rasteriser — both targeting DirectDraw surfaces.

## Present: DirectDraw page-flip

Presentation is classic DirectDraw primary/back-buffer **page flipping**, with a Blt fallback. The
embedded `DDERR_*` strings show the paths it handles: `DDERR_NOFLIPHW`, `DDERR_NOTFLIPPABLE`,
`"flip a surface that is not flippable"`, `"This process already has created a primary surface."`,
`DDERR_NOVSYNCHW`, `DDERR_VERTICALBLANKINPROGRESS` (so it **syncs to the vertical blank** when the
hardware supports it, and copes when it doesn't). There is an on-screen **`"FPS: %4g"`** counter.

## Message pump: a `PeekMessage` game loop — `FUN_0045a960`

The main loop is **`FUN_0045a960`** (image base `0x00400000`; the only function that references
all of `PeekMessageA` + `GetMessageA` + `TranslateMessage` + `DispatchMessageA`). After init it runs
the classic **pump-then-render** game loop (Ghidra decompilation, trimmed):

```c
joined_r0x0045aafb:
  if ((DAT_007a1a14 & 2) == 0) {                       // while not "quit" flag
    iVar3 = PeekMessageA(&local_140, NULL, 0, 0, 0);   // peek (don't block)
    while (iVar3 != 0) {                               // drain all pending messages
      BVar4 = GetMessageA(&local_140, NULL, 0, 0);
      if (BVar4 == 0) goto LAB_0045ac66;               // WM_QUIT -> exit
      TranslateMessage(&local_140);
      DispatchMessageA(&local_140);
      iVar3 = PeekMessageA(&local_140, NULL, 0, 0, 0);
    }
    if ((DAT_007a1a14 & 1) != 0) { ... }               // no messages pending -> run a game frame
    goto joined_r0x0045aafb;
  }
```

So: drain the Windows message queue without blocking, and when it's empty run one game/render frame
— a real-time loop, not a tool's blocking `GetMessageA` wait. Two secondary pumps exist
(`FUN_005f86e0`, `FUN_004798e0`) for modal/wait sub-loops.

## Frame pacing

Timing imports: **`QueryPerformanceCounter` + `QueryPerformanceFrequency`** (high-resolution timer)
and **`timeGetTime`** (WINMM, 1 ms timer). **`FUN_005f5f10`** is the timer core — the one function
that calls *both* `QueryPerformanceCounter` and `timeGetTime` (a QPC clock with a `timeGetTime`
fallback). Combined with the `"FPS: %4g"` counter and the vblank sync above, the original measures
real elapsed time per frame and advances simulation by it — consistent with the ride VM's `WAIT`
opcodes scaling by a runtime "framerate factor" ([T-007](tickets/T-007-vm-opcodes-rse.md)): logic is
time-based, not a fixed lockstep tick. DirectDraw is initialised from `FUN_00563460` / `FUN_005fa2b0`
(callers of `DirectDrawCreate`).

## Per-frame state management — the key comparison

The execute-buffer model answers the architectural question directly. In DX6 Immediate Mode the
expensive objects are created **once and reused**:

- **Textures** are uploaded once to `IDirect3DTexture` handles (`D3DERR_TEXTURE_CREATE_FAILED` /
  `_LOAD_FAILED` are init-time errors, not per-frame).
- **Materials** are created once to handles (`D3DERR_MATERIAL_CREATE_FAILED`).
- **Matrices/viewport** are set as device state.
- Each **frame** then fills an **execute buffer** with vertices + draw/state opcodes and submits it
  with one `Execute` — i.e. geometry is **batched per frame**, while the GPU-side resources
  (textures, materials, render state) **persist across frames**.

So the original did **not** allocate/destroy GPU resources per draw. It cached them and batched the
per-frame geometry.

## Comparison with OpenTPW

| Aspect | Original `tp.exe` (DX6/7) | OpenTPW (Veldrid/Vulkan) |
|--------|---------------------------|--------------------------|
| Graphics API | DirectDraw + D3D Immediate Mode (execute buffers), HAL + MMX software fallback | Veldrid (Vulkan), one modern pipeline |
| Main loop | `PeekMessage` pump → render | `Renderer.Run` → `Update` → render; events pumped each frame |
| Present | DirectDraw page-flip, vblank-synced when HW allows | Swapchain present, `SyncToVerticalBlank = true` |
| Frame pacing | `QueryPerformanceCounter`/`timeGetTime`, time-based sim; FPS counter | `Stopwatch` delta ([T-028](tickets/T-028-frame-cpu-hygiene.md)), vsync-paced |
| GPU resources | textures/materials created **once**, reused; per-frame **execute buffer** batches geometry | **now** caches resource sets + batches UI geometry ([T-026](tickets/T-026-render-resource-churn.md)/[T-027](tickets/T-027-ui-draw-batching.md)) |

**Verdict.** The upstream C# re-implementation's original "create a GPU resource set per draw + a
full queue submit per uniform bind" matched **neither** modern practice **nor** the 1999 original —
the original explicitly created device resources once and submitted batched per-frame execute
buffers. The T-026/T-027 work (cache resource sets, record uniform updates on the frame command
list, merge UI draws) brings OpenTPW back in line with how the original actually worked.

## Recovered function addresses (Ghidra, image base `0x00400000`)

| Address | Role |
|---------|------|
| `FUN_0045a960` | main message pump / game loop (Peek+Get+Translate+Dispatch) |
| `FUN_005f86e0`, `FUN_004798e0` | secondary modal/wait pumps |
| `FUN_005f5f10` | frame timer (QueryPerformanceCounter + timeGetTime) |
| `FUN_00563460`, `FUN_005fa2b0` | DirectDraw init (`DirectDrawCreate`) |

## Not yet recovered

- The exact per-frame render-dispatch / execute-buffer fill and the `Flip`/present call site (the
  pump's "no messages -> run a frame" branch calls into the game tick; following that chain to the
  D3D `Execute` + DirectDraw flip is the next depth, not needed for the architectural conclusions).
- Whether HAL or the MMX software renderer is selected on a given machine (a run-time capability
  check; the strings confirm both paths and a HAL->software fallback exist).
