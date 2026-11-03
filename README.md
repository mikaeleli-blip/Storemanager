# Storemanager

Top-down 3D supermarket management game in Unity. You are the store manager:
hire staff, run departments, order stock, delegate to your assistant manager
and grow the store. See [docs/DESIGN.md](docs/DESIGN.md).

## Open and play

1. Open the folder in Unity Hub (Unity **6000.6.5f1**).
2. Open any scene (the empty default scene is fine) and press **Play**.
   The game builds itself at startup (`Bootstrap.cs`). You start at your
   office desk with an empty store and time paused: build, pick products,
   order (express for day one), then press 1-4 to start the clock.

## Controls

| | Floor (walking) | Office desk |
|---|---|---|
| Move | WASD, or right-click a spot | WASD pans the camera |
| Rotate / zoom | Q / E, mouse wheel | Q / E, mouse wheel |
| Talk to staff | Left-click someone within 3 m | — |
| Build | — | Build tab: pick a fixture, left-click to place, R rotate, right-click cancel |
| Shelf product | — | Click a shelf, pick its product |
| Desk | Tab (when in the office) | Tab to get up |
| Time | Space pause, 1–4 speed | same |

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
printing each day's ledger and what upset customers. Needs Mono.
