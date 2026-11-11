# Asset contract: what the code expects from the models

The game loads `Assets/StoreManager/Resources/Models/*.fbx` and spawns meshes **by name**
(`World/ModelLibrary.cs`). If a name, frame or number below changes, the game breaks or looks wrong.
Changing any of them needs a request in [HANDOFF.md](HANDOFF.md) so the code side is updated in the same
change. Adding new models is fine any time; they're only used once the code asks for them.

## Pipeline

- Models are scripted in `Tools/Blender/` (bpy pip module, no Blender UI needed):
  `kit.py` helpers, `palette.py` colours, `fixtures.py` (fixtures, building, decor), `products.py`,
  `characters.py`, `people.py` (preview only), `build_models.py` (build, previews, export).
- `python3 Tools/Blender/build_models.py --export` writes:
  - `Fixtures.fbx` (fixtures, building, signs), `Decor.fbx`, `Products.fbx`, `Characters.fbx`
  - `palette.png`, `palette_e.png` (colour and emission palette)
  - `StockLayout.txt` (models folder) and `Assets/StoreManager/Scripts/Sim/ShelfRows.cs` (generated)
  - Flags: `--products-only`, `--fixtures-only`, `--characters-only`; previews: `--preview`, `--items`,
    `--people`, `--people-store`, `--cold`, `--fixes`.
- Authoring is in **Unity coordinates**: metres, x right, y up, z forward. `kit.bl()` converts to Blender.
  The FBX export bakes the axes, so models arrive in Unity unrotated.
- **Origin = the modelled origin.** `kit.finish()` puts every mesh's origin at (0, 0, 0), so the coordinates
  you model in are the coordinates the game uses. Floor objects have their base on y = 0. Don't change this.
- **One material.** Every face is UV-mapped to a cell of the shared palette. The only extra material is the
  `glass` colour (transparent "Store Glass" in Unity). New colours are appended at the end of `palette.py`
  so existing cells keep their place.
- Unity import settings come from `Editor/GraphicsSetup.cs` (`ModelImport`). `Products.fbx` must stay
  read/write enabled (the shelves merge product units at runtime).

## Names the code uses

### Fixtures and building (`Fixtures.fbx`)
`Gondola`, `Fridge`, `Freezer`, `ProduceTable`, `BakeryShelf`, `Oven`, `DeliCounter`, `DeliScale`, `Checkout`,
`LanePole`, `Wall`, `WallHalf`, `WallWindow`, `WallPost`, `Entrance`, `StoreSign`, `DockDoor`.

- Walls: `Wall` is a 1 m segment along X, `WallHalf` a 0.5 m one with the same profile (0.30 m body, origin
  centred on the floor). Side walls run at x = -0.15 and W + 0.15 (yaw 90): full segments for z = 4..34, half
  segments at z = 3.75 and 34.25; the office partition gets halves at z = 26.75 and 34.25.
Exported but not used by the game yet: `GondolaEnd`, `DeliBack`.
`Sign_<category id>` (grocery, produce, dairy, frozen, household, bakery, deli): hung by the code at y = 2.35 over
the middle of each department's shelves, unrotated; the board's base is its origin and it reads from -Z.

- Shelf-type fixtures are 1 m segments along Z, customers on both X sides (see `fixtures.py`).
- Safety checks in `ModelLibrary.Calibrate()`: the **Gondola is 1.86 m tall** (used to detect wrong import
  scale or Z-up), and the **Checkout sticks out further on +X** (used to detect mirroring). Keep both true.

### Decor (`Decor.fbx`)
`Tree`, `Bench`, `CartCorral`, `BasketStack`, `Desk`, `OfficeChair`, `FilingCabinet`, `Whiteboard`, `Plant`,
`Sofa`, `Rack`, `Pallet`, `Truck`, `Car_red`, `Car_sky`, `Car_yellow`, `Car_white`, `Car_darkgrey`.

### Products (`Products.fbx`)
Product names below are the catalogue names with spaces as underscores (`Canned Soup` → `Canned_Soup`).

- `U_<Product>_<k>`: **shelf units, one sellable item each** (one pizza box, one yogurt cup). The game builds
  every shelf row from these at the slots in `StockLayout.txt` and shows exactly as many as are in stock.
  A unit must be one item: if it shows a stack, the shelf count will look wrong.
- `StockLayout.txt` + `ShelfRows.cs`: generated together from `products.LAYOUT`. Row frame: products face +X,
  the row runs along Z from -0.47 to 0.47, depth x = 0.06 (back) to 0.42 (front), base y = 0. The game puts a
  row on each shelf level and turns it 180° for the other side. **Units per row = shelf capacity**, so a new
  layout changes the game's balance: flag it in HANDOFF.md.
- `Item_<Product>`: one item centred on the origin, base on y = 0 (baskets, trolleys, later the till belt).
- `Deli_<Product>`: a counter row of trays (counter frame: along X, facing -Z, placed at y = 0.76).
- `Stock_<Product>`: full rows for Blender previews only; not exported.

### People (`Characters.fbx`)
Parts are `C_<Part>__<role>`. Roles: `skin`, `hair`, `top`, `bottom`, `shoes`, `accent`, `accent2` are tinted
per person in code; `pal` keeps the palette. Each part is spawned on a joint; frames (person faces +Z,
1.76 m to the top of the head before the code scales by height):

| Part group | Origin (joint) |
|---|---|
| `TorsoTee/Sweater/Hoodie/Jacket/Coat/Polo/Shirt`, `Skirt`, `Apron`, `ApronStripes`, `Vest`, `VestStripes`, `Blazer`, `Lanyard` | hips, 0.82 m up; torso top at y = 0.6 |
| `Head`, `HairShort/Long/Bun/Pony/Curly/Buzz/Bald`, `Beard`, `Glasses`, `HatBaker`, `Cap` | neck (top of torso) |
| `ArmShort`, `ArmLong` | shoulder at x = ±0.27 × width, y = 0.56 above hips; hand bottom at y = -0.6 |
| `Leg`, `LegBare` | hip at x = ±0.095 × width; sole at y = -0.82 |
| `Basket`, `Bag`, `Cane`, `Phone` | grip in the hand |
| `Trolley` | middle of the handle; the code holds it 0.99 m above the floor |

The code checks `C_Head__skin` exists before using the people models. Joint numbers live in
`Agents/CharacterRig.cs` and `Tools/Blender/people.py`; change them together.

## Numbers shared by code and art

| What | Code | Art |
|---|---|---|
| Gondola/fridge shelf heights 0.12, 0.52, 0.92, 1.32 | `Sim/Catalog.cs` (`four`) | `fixtures.py` `LEVELS` |
| Freezer floor 0.45 | `Catalog.cs` freezer `Levels` | `fixtures.py` `FREEZER_FLOOR` |
| Bakery shelf heights 0.12, 0.62, 1.12 | `Catalog.cs` bakery `Levels` | `fixtures.py` `BAKERY_LEVELS` |
| Produce crate tilt -14° and pivot | `World/StoreBuilder.cs` `ProduceTilt` | `fixtures.py` `PRODUCE_TILT`, `PRODUCE_PIVOT` |
| Deli tray height 0.76 (rows are modelled at y = 0) | `StoreBuilder.cs` | `build_models.py` preview scene |
| Character joints | `Agents/CharacterRig.cs` | `characters.py`, `people.py` |
| Segment centres (0.5, 0.5), (0.5, 1.5) | `Catalog.cs` `shelfSegs` | fixture segment models |
