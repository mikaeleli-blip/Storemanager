# Storemanager

Top-down 3D supermarket management game in Unity. You are the store manager:
hire staff, run departments, order stock, delegate to your assistant manager
and grow the store. See [docs/DESIGN.md](docs/DESIGN.md).

## Open and play

1. Open the folder in Unity Hub (Unity **6000.6.5f1**).
2. Open any scene (the empty default scene is fine) and press **Play**.
   The game builds itself at startup (`Bootstrap.cs`). You start at the
   office computer with an empty corner shop and time paused: open **Build**,
   place a checkout and shelves, pick products, buy stock in the **Shop** app
   (express for day one), then press 1-4 to start the clock.
   Sales earn XP; store levels and cash unlock licences (Licences tab) for
   new products, departments, staff roles and expansions.

## Controls

| | Floor (walking) | Office computer | Build (from the computer) |
|---|---|---|---|
| Move | WASD, or right-click a spot | — | WASD pans the camera |
| Rotate / zoom | Q / E, mouse wheel | — | Q / E, mouse wheel |
| Talk to staff | Left-click someone within 3 m | — | — |
| Apps | — | Click desktop icons; drag windows by the title bar | — |
| Build | — | Build icon or taskbar button | Pick a fixture, left-click to place, R rotate, right-click cancel |
| Shelf product | — | — | Click a shelf, pick its product |
| Back / close | — | Esc closes the front window | Esc back to the computer |
| Desk | Tab (in the office) sits down | Tab gets up | Tab gets up |
| Time | Space pause, 1–4 speed | same, or taskbar | same |

## Layout

```
Assets/StoreManager/Scripts/
  Core/    bootstrap, clock, input, camera, materials
  World/   store grid, pathfinding, building, placement rules, models
  Sim/     catalog, products, fixtures, economy, orders
  Agents/  customers, staff, assistant manager, manager avatar
  UI/      IMGUI HUD, office screens, build mode
Tools/Blender/   scripted low-poly models (build_models.py) and textures
Tools/Headless/  simulation harness (run.sh)
```

## Headless balance runs

`Tools/Headless/run.sh [days] [scripted|idle] [seed]` compiles the game scripts
against a small fake UnityEngine and simulates whole days without Unity,
printing each day's ledger and what upset customers. Needs Mono. The
`scripted` mode is an auto-player that buys licences, places fixtures, orders
from suppliers, pays bills, hires and expands as money and levels allow. A 4th
argument `overstock` makes it over-order with date checks off (expiry test).
