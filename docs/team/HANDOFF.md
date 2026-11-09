# Handoff board

Requests between Claude (code) and Codex (graphics). Newest at the top of "Open". When it's done, move it to
"Done" with the commit hash. Eli can add or reprioritise anything.

Format:

```
- [ ] YYYY-MM-DD  From → To: what's needed (and why). Files: ...
```

## Open

- [ ] 2026-11-09  Codex -> Claude: finish wall alignment in `Scripts/World/StoreBuilder.cs`.
  The current side-wall loop covers z=3..35 while the front/rear centres are 3.5/34.5;
  the office partition covers z=27..30 and 31..34, leaving 0.35 m gaps to the cross-wall bodies.
  New exported `WallHalf` is a 0.5 m segment along X (origin centred on the floor), with exactly
  the same profile as `Wall`. Use full side walls only for z=4..33; add `WallHalf` at
  (-0.15,0,3.75), (W+0.15,0,3.75), (-0.15,0,34.25), (W+0.15,0,34.25), all yaw=90.
  Keep the office loop, and add `WallHalf` at (OfficeWallX+0.5,0,26.75) and
  (OfficeWallX+0.5,0,34.25), yaw=90. This preserves the office opening z=30..31.
  Validate in Unity at each expansion width and run the headless check. Update the asset contract
  with the new name/profile when adopting it. `building_preview.py --aligned` shows the proposed
  front corner; ordinary `--walls` previews deliberately show CURRENT game placement.
  Art now uses 0.30 m wall bodies (side centres are +/-0.15), flush 1 m caps, 1.85 m wall/window
  tops and 1.87 m post caps. Entrance jambs fit inside their 4 m opening; glazing uses the existing
  Glass material. No existing model names, shelf heights, product layouts or character joints changed.

- [ ] 2026-11-09  Eli: decide who owns the UI look (`Resources/UI/`: stylesheet, fonts, icons). Claude has it
  for now.

## Done

- [x] 2026-11-09  Claude: set up this folder (team README, asset contract, handoff board). Models made by
  Claude so far: fixtures, decor, products (one unit per item on shelves), people (`C_` parts).
