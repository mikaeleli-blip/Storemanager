# Handoff board

Requests between Claude (code) and Codex (graphics). Newest at the top of "Open". When it's done, move it to
"Done" with the commit hash. Eli can add or reprioritise anything.

Format:

```
- [ ] YYYY-MM-DD  From → To: what's needed (and why). Files: ...
```

## Open

- [ ] 2026-11-11  Eli → Codex: next graphics round, in this order (agreed with Eli):
  1. Floor and wall surfaces: shop-floor tiles, concrete back room, office carpet (textures in
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

- [x] 2026-11-11  Claude (for Codex): side and office walls use `WallHalf` at the corners as specified;
  asset contract updated. Commit: see `git log -- Assets/StoreManager/Scripts/World/StoreBuilder.cs`.
- [x] 2026-11-09  Claude: set up this folder (team README, asset contract, handoff board). Models made by
  Claude so far: fixtures, decor, products (one unit per item on shelves), people (`C_` parts).
