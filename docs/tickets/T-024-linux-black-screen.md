# T-024 — Linux: the window renders black (scene not visibly drawn)

- **Priority**: 🟠 Medium (the game runs but shows nothing)
- **Type**: Rendering
- **Status**: ✅ Resolved — the lobby renders; the "black" was the load, not a draw bug.
- **Follow-up of**: [T-023](T-023-linux-vulkan-libdl.md).

## Symptom

After the [T-023](T-023-linux-vulkan-libdl.md) fix the game **runs** on Linux (window titled
"Theme Park World", Vulkan device created, render loop running, no crash), but the window appeared
**black** — the jungle level didn't seem to be rendered.

## Resolution

It renders correctly. The lobby draws the island, the advisor model, water, sky, the purple
buttons and (now) their text labels — verified interactively on an AMD Radeon (Mesa/Vulkan).

What looked like a "black screen" was the **synchronous level load**: `Game.Run` constructs
`new Level("jungle")` on the main thread, which takes several seconds (textures/models), during
which the window can't pump events and the WM marks it "not responding" while showing the last
frame. Two things addressed this:

- A **loading screen** (sky-blue clear + "LOADING…") is presented before the load so the window
  shows something instead of an undefined/black frame (`Renderer.RenderLoadingScreen`,
  `Renderer.SetupPresent`, `Renderer.ClearColor`).
- Bitmap-font **text rendering** was fixed (UV V-flip to match the UI shader's `1 - y` sampling,
  and Y-up baseline alignment) so the loading text and the button labels draw cleanly.

## Follow-ups

- The level load is still synchronous (window frozen on the loading frame, not animating). A
  background/threaded load — or at least re-presenting the loading screen during the load — would
  keep it responsive. Tracked informally; not blocking.
- Antialiased fonts render as noise: [T-025](T-025-bf4-antialiased-fonts.md).

## Affected files

`source/OpenTPW/Client/Renderer.cs` (loading screen / present), `source/OpenTPW/Client/Game.cs`
(loading screen), `source/OpenTPW/Client/Graphics.Text.cs`,
`source/OpenTPW.Files/Formats/Font/FontAtlas.cs` (text rendering),
`source/OpenTPW/UI/Widgets/PurpleButton.cs` (button labels).
