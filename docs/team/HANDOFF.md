# Handoff board

Requests between Claude (code) and Codex (graphics). Newest at the top of "Open". When it's done, move it to
"Done" with the commit hash. Eli can add or reprioritise anything.

Format:

```
- [ ] YYYY-MM-DD  From → To: what's needed (and why). Files: ...
```

## Open

- [ ] 2026-11-11  Claude → Codex (part of item 3 below): stockers now carry stock as shipping cases and
  fill shelves one unit at a time (grey-box cardboard cubes for now). Wanted, in `Characters.fbx`:
  `C_Case__pal` + `C_Case__label` (frame in ASSET_CONTRACT, People table). Then `RollCage` (origin at the
  middle of the push handle, ~1.0 m up like the trolley; deck ~0.15 m up, room for 2×3×4 cases) and
  `HandTruck` (origin at the grip); post the frames here and I'll switch big loads to them.

- [ ] 2026-11-11  Eli → Codex: next graphics round, in this order (agreed with Eli):
  1. (Done, 4be457b + wall hookup.) Floor and wall surfaces: shop-floor tiles, concrete back room, office carpet (textures in
     `Resources/Textures/`; tell Claude the file names and tiling so the floors in `StoreBuilder.cs` use them).
  2. Checkout details: products on the belt, till screen, bag stand (new models; Claude places/animates them).
  3. Back room and deliveries: roll cages, hand trucks, pallets of cases, so stockers can visibly carry stock.
  4. Outside: street, kerbs and neighbouring buildings around the car park.
  Post the model names and frames here as each one lands; Claude wires them in.
- [ ] 2026-11-11  Eli → Claude: next code round. Done: walls, save/load + New game, department signs. Left:
  check the UI Toolkit screens at runtime in Unity (they compile); office computer and build mode in UI Toolkit.

- [ ] 2026-11-09  Eli: decide who owns the UI look (`Resources/UI/`: stylesheet, fonts, icons). Claude has it
  for now. (If it goes to Codex: app icons for the office computer.)

## Done

- [x] 2026-11-11  Claude: wall textures hooked up (body faces split off in `ModelLibrary.SpawnFaced`, plaster/block
  by room, offsets line up across pieces, smoothness read from `surfaces.json`); rule in ASSET_CONTRACT.
- [x] 2026-11-11  Codex -> Claude: surface pack ready. Existing `floor_store.png`, `floor_back.png`,
  `floor_office.png`, `asphalt.png`, `sidewalk.png` are now seamless 512px RGB albedos; retain current
  resource names and `(width, depth)` tiling: one repeat = 1 x 1 m. Shop/office tiles are 500 mm;
  office is now blue-grey carpet. Use Repeat, sRGB, mipmaps and bilinear/trilinear filtering.
  New `wall_plaster.png` (shop/office) and `wall_block.png` (back room, 500 x 250 mm blocks) need
  separate body-face materials/UVs in your wall rendering: palette UVs are single-colour cells and
  must NOT be reused for these maps. Use metre-based wall UVs (horizontal distance, height),
  retaining trim/glass on the existing palette. Suggested smoothness and descriptions are in
  `Resources/Textures/surfaces.json`; office carpet smoothness .05 (current generic floor .15 works).
  Preview: `Tools/Blender/surface_preview.py`. Floor maps are already used by existing code;
  wall-map hookup remains for Claude. Source generator: `Tools/Blender/textures.py` (NumPy/Pillow).
- [x] 2026-11-11  Claude (for Codex): side and office walls use `WallHalf` at the corners as specified;
  asset contract updated. Commit: see `git log -- Assets/StoreManager/Scripts/World/StoreBuilder.cs`.
- [x] 2026-11-09  Claude: set up this folder (team README, asset contract, handoff board). Models made by
  Claude so far: fixtures, decor, products (one unit per item on shelves), people (`C_` parts).
