# T-058 — Sideshow rides (stalls / games)

- **Priority**: 🟢 Low (content)
- **Type**: Engine
- **Status**: ☐ To do (proposed — RE recon done; **5 sideshow WADs present, ride engine reusable**)
- **Related**: [T-032](T-032-ride-engine.md) (ride engine), [T-041](T-041-ride-shop-placement.md) (placement).

## Context

Sideshows are the small games/stalls (coconut shy, puzzle, etc.) — a distinct catalog from rides + shops.
OpenTPW has only VM string stubs (`SideshowTakings`, `EventSideshowWin`); none are placeable. The ride engine
+ catalog already model scripted placeable objects, so sideshows are a new catalog subtype, not new infra.

## What we know (RE recon)

- **Assets:** per-level `sideshow/*.wad` — jungle: `arc2x3`, `hyenas`, `junspray`, `puzzle`, `squark`; space:
  `lunar`, `arcade`, `scipuzz`, `marsmoon`; etc. Each is a normal ride-style WAD (`.MD2` + `.RSE` + `.sam`).
- **VM:** `EVENT`'s sideshow-win path + `SideshowTakings` already exist as opcode/string stubs.
- The `.sam`/`.RSE`/`.MD2` loaders, the build catalog, placement, queue + income tracking are all done.

## Scope

1. Add sideshows to the build catalog (load each `sideshow/<name>` like a ride: shape, mesh, script).
2. Treat a sideshow as a `Ride` subtype (or a thin variant): placement, a small queue, a play interaction,
   and **takings** routed to `ParkFinances` (`SideshowTakings`); wire the `EventSideshowWin` payout.
3. Verify one sideshow (e.g. jungle `puzzle`/`squark`) places, runs its script, and earns money in-game.

## Acceptance criteria

- At least one sideshow per level is placeable from the catalog, peeps use it, and it earns takings.

## Affected files (anticipated)

`source/OpenTPW/World/Level.cs` (catalog), `source/OpenTPW/World/Ride.cs` (or a `Sideshow` variant),
`VM/Handlers/*` (sideshow-win/takings), tests for the takings logic.
