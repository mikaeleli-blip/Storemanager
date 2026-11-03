using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Builds the tile grid and the store's visuals for the current size. Uses the Blender models from
    /// ModelLibrary; falls back to coloured primitives when they aren't available.
    /// </summary>
    public class StoreBuilder
    {
        readonly Store store;
        Transform staticRoot;
        public GameObject Truck;

        static readonly Color StreetCol = new Color(0.32f, 0.33f, 0.35f);
        static readonly Color LobbyCol = new Color(0.86f, 0.84f, 0.8f);
        static readonly Color EmptySlotCol = new Color(0.62f, 0.62f, 0.6f);
        static readonly Color BackCol = new Color(0.55f, 0.56f, 0.58f);
        static readonly Color OfficeCol = new Color(0.55f, 0.42f, 0.3f);
        static readonly Color DockCol = new Color(0.25f, 0.25f, 0.27f);
        static readonly Color WallCol = new Color(0.93f, 0.92f, 0.88f);
        static readonly Color RackCol = new Color(0.3f, 0.38f, 0.5f);
        static readonly Color CounterCol = new Color(0.25f, 0.27f, 0.3f);
        static readonly Color DeskCol = new Color(0.4f, 0.27f, 0.15f);
        static readonly Color ScaffoldCol = new Color(0.95f, 0.75f, 0.1f);

        // Shelf heights of each fixture model (match Tools/Blender/fixtures.py).
        static readonly float[] ShelfLevels = { 0.12f, 0.52f, 0.92f, 1.32f };
        static readonly float[] BakeryLevels = { 0.12f, 0.62f, 1.12f };
        const float ProducePivotY = 0.8f, ProduceTilt = -14f;

        public StoreBuilder(Store store) { this.store = store; }

        static bool Models { get { return ModelLibrary.Available; } }

        public static Vector2Int BackPickFor(int slot, int width)
        {
            return new Vector2Int(Mathf.Clamp(Layout.SlotX(slot) + 6, 10, width - 2), 28);
        }

        // ------------------------------------------------------------------ grid

        /// <summary>Rebuilds everything: grid, walls, checkouts and every department.</summary>
        public void BuildAll()
        {
            int slots = store.SlotCount;
            int W = Layout.Width(slots);
            var g = new StoreGrid(W, Layout.Depth);
            store.Grid = g;

            g.FillZone(0, 0, W - 1, Layout.StreetEnd, Zone.Outside);
            g.FillZone(0, Layout.FrontWall, W - 1, Layout.FrontWall, Zone.Wall);
            for (int x = 2; x <= 5; x++) g.SetZone(x, Layout.FrontWall, Zone.Front);
            g.FillZone(0, Layout.LobbyStart, W - 1, Layout.FloorStart - 1, Zone.Front);
            g.FillZone(0, Layout.FloorStart, W - 1, Layout.FloorEnd, Zone.Floor);
            g.FillZone(0, Layout.BackWall, W - 1, Layout.BackWall, Zone.Wall);
            for (int s = 0; s < slots; s++)
            {
                g.SetZone(Layout.SlotX(s) + 10, Layout.BackWall, Zone.Back);
                g.SetZone(Layout.SlotX(s) + 11, Layout.BackWall, Zone.Back);
            }
            g.FillZone(0, Layout.BackStart, Layout.OfficeEndX, Layout.BackEnd, Zone.Office);
            g.FillZone(Layout.OfficeWallX, Layout.BackStart, Layout.OfficeWallX, Layout.BackEnd, Zone.Wall);
            g.SetZone(Layout.OfficeWallX, 30, Zone.Office);
            g.FillZone(Layout.OfficeWallX + 1, Layout.BackStart, W - 1, Layout.BackEnd, Zone.Back);
            g.FillZone(0, Layout.RearWall, W - 1, Layout.RearWall, Zone.Wall);
            for (int x = W - 6; x <= W - 3; x++) g.SetZone(x, Layout.RearWall, Zone.Dock);
            g.FillZone(0, Layout.DockRow, W - 1, Layout.DockRow, Zone.Dock);

            for (int i = 0; i < store.Lanes.Count; i++)
            {
                int cx = store.Lanes[i].CounterX;
                g.SetBlocked(cx, 6, true);
                g.SetBlocked(cx, 7, true);
            }
            for (int x = 2; x <= 4; x++) g.SetBlocked(x, 31, true);          // desk
            for (int x = 10; x <= W - 2; x++)                                  // racks
            {
                if ((x - 10) % 8 == 7) continue;
                g.SetBlocked(x, 29, true);
                g.SetBlocked(x, 32, true);
            }
            // furniture and decor
            BlockAll(g, new[] { new Vector2Int(0, 33), new Vector2Int(1, 33), new Vector2Int(7, 33), new Vector2Int(0, 27),
                                new Vector2Int(7, 27), new Vector2Int(7, 28), new Vector2Int(7, 29), new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(0, 5),
                                new Vector2Int(1, 5), new Vector2Int(0, 6), new Vector2Int(1, 6), new Vector2Int(6, 4),
                                new Vector2Int(0, 10), new Vector2Int(W - 1, 4), new Vector2Int(W - 2, 33), new Vector2Int(W - 1, 33),
                                new Vector2Int(W - 2, 27), new Vector2Int(W - 1, 27) });

            if (staticRoot != null) Object.Destroy(staticRoot.gameObject);
            staticRoot = new GameObject("Store Building").transform;
            staticRoot.SetParent(store.transform, false);
            if (Models) BuildStaticModels(W, slots);
            else BuildStaticPrimitives(W, slots);

            for (int s = 0; s < store.Slots.Length; s++)
            {
                var d = store.Slots[s];
                if (s < slots && d != null) BuildDepartment(d);
            }
        }

        static void BlockAll(StoreGrid g, Vector2Int[] tiles)
        {
            for (int i = 0; i < tiles.Length; i++) g.SetBlocked(tiles[i].x, tiles[i].y, true);
        }

        // ------------------------------------------------------------------ building (models)

        static GameObject M(string name, Transform parent, float x, float y, float z, float yaw = 0f, float tilt = 0f)
        {
            return ModelLibrary.Spawn(name, parent, new Vector3(x, y, z), yaw, tilt);
        }

        static void Floor(Transform parent, string name, string tex, float x, float z, float w, float d, Color tint, float y = 0f)
        {
            var go = Mats.Box(name, parent, x, z, w, d, y - 0.05f, 0.05f, Color.white);
            go.GetComponent<Renderer>().sharedMaterial = Mats.Textured(tex, new Vector2(w, d), tint);
        }

        void BuildStaticModels(int W, int slots)
        {
            var r = staticRoot;

            // ground
            Floor(r, "Car Park", "asphalt", -16f, -18f, W + 32f, 18f, Color.white);
            Floor(r, "Sidewalk", "sidewalk", -16f, 0f, W + 32f, 3.4f, Color.white);
            Floor(r, "Shop Floor", "floor_store", 0f, 3f, W, 23f, Color.white);
            Floor(r, "Back Room", "floor_back", Layout.OfficeWallX, Layout.BackWall, W - Layout.OfficeWallX, 9f, Color.white);
            Floor(r, "Office Floor", "floor_office", 0f, Layout.BackWall, Layout.OfficeWallX, 9f, Color.white);
            Floor(r, "Yard", "asphalt", -16f, Layout.RearWall, W + 32f, 14f, new Color(0.85f, 0.85f, 0.85f));
            for (int s = 0; s < slots; s++)
                Floor(r, "Slot Floor " + s, "floor_store", Layout.SlotX(s) + 0.1f, Layout.FloorStart, Layout.SlotWidth - 0.2f, 14f, new Color(0.82f, 0.82f, 0.8f), 0.012f);

            // car park lines, cars, trees
            var stripe = new Color(0.92f, 0.92f, 0.88f);
            for (float x = -10f; x <= W + 10f; x += 2.8f)
                Mats.Box("Bay Line", r, x, -6.5f, 0.12f, 4.6f, 0.001f, 0.01f, stripe);
            Mats.Box("Kerb", r, -16f, -0.25f, W + 32f, 0.25f, 0f, 0.12f, new Color(0.73f, 0.71f, 0.68f));
            string[] cars = { "Car_red", "Car_sky", "Car_yellow", "Car_white", "Car_darkgrey" };
            int ci = 0;
            for (float x = -8.6f; x <= W + 8f; x += 2.8f * 2f)
            {
                if (((int)(x * 7.3f) & 3) == 0) continue;
                M(cars[ci++ % cars.Length], r, x + 1.4f, 0f, -4.2f, ((ci & 1) == 0) ? 0f : 180f);
            }
            for (float x = -12f; x <= W + 12f; x += 9f)
                M("Tree", r, x, 0f, -11f);
            M("Tree", r, -3f, 0f, 1.6f);
            M("Tree", r, W + 3f, 0f, 1.6f);
            M("Bench", r, -2.5f, 0f, 2.6f, 180f);
            M("Bench", r, W + 2.5f, 0f, 2.6f, 180f);

            // front wall with windows and the entrance
            for (int x = 0; x < W; x++)
            {
                if (x >= 2 && x <= 5) continue;
                M("WallWindow", r, x + 0.5f, 0f, 3.5f);
            }
            M("Entrance", r, 4f, 0f, 3.5f);
            M("StoreSign", r, Mathf.Max(13f, W * 0.5f + 2f), 1.85f, 3.5f);

            // side walls
            for (int z = 3; z <= 34; z++)
            {
                M("Wall", r, -0.15f, 0f, z + 0.5f, 90f);
                M("Wall", r, W + 0.15f, 0f, z + 0.5f, 90f);
            }
            M("WallPost", r, -0.15f, 0f, 3.5f);
            M("WallPost", r, W + 0.15f, 0f, 3.5f);
            M("WallPost", r, -0.15f, 0f, 34.5f);
            M("WallPost", r, W + 0.15f, 0f, 34.5f);

            // back wall with staff doorways
            for (int x = 0; x < W; x++)
            {
                int local = x % Layout.SlotWidth;
                if (local == 10 || local == 11) continue;
                M("Wall", r, x + 0.5f, 0f, Layout.BackWall + 0.5f);
            }
            for (int s = 0; s < slots; s++)
            {
                M("WallPost", r, Layout.SlotX(s) + 10f, 0f, Layout.BackWall + 0.5f);
                M("WallPost", r, Layout.SlotX(s) + 12f, 0f, Layout.BackWall + 0.5f);
            }

            // office wall (door at z = 30)
            for (int z = Layout.BackStart; z <= Layout.BackEnd; z++)
            {
                if (z == 30) continue;
                M("Wall", r, Layout.OfficeWallX + 0.5f, 0f, z + 0.5f, 90f);
            }

            // rear wall and loading dock
            for (int x = 0; x < W; x++)
            {
                if (x >= W - 6 && x <= W - 3) continue;
                M("Wall", r, x + 0.5f, 0f, Layout.RearWall + 0.5f);
            }
            M("DockDoor", r, W - 4f, 0f, Layout.RearWall + 0.5f);

            // checkouts
            for (int i = 0; i < store.Lanes.Count; i++)
            {
                var lane = store.Lanes[i];
                float cx = lane.CounterX + 0.5f;
                M("Checkout", r, cx, 0f, 7f);
                M("LanePole", r, cx, 0f, 8.3f);
                var light = Mats.Prim(PrimitiveType.Sphere, "Lane Light " + (i + 1), r, new Vector3(cx, 2.2f, 8.3f),
                    new Vector3(0.2f, 0.2f, 0.2f), Color.red, false);
                lane.Light = light.GetComponent<Renderer>();
            }

            // lobby
            M("CartCorral", r, 0.9f, 0f, 4.6f);
            M("BasketStack", r, 6.5f, 0f, 4.5f);
            M("Plant", r, 0.6f, 0f, 10.6f);
            M("Plant", r, W - 0.6f, 0f, 4.6f);

            // office
            M("Desk", r, 3.5f, 0f, 31.5f);
            M("OfficeChair", r, 3.5f, 0f, 30.55f);
            M("FilingCabinet", r, 0.5f, 0f, 33.5f);
            M("FilingCabinet", r, 1.15f, 0f, 33.5f);
            M("Whiteboard", r, 4.8f, 0f, 34.36f);
            M("Plant", r, 7.4f, 0f, 33.4f);
            M("Plant", r, 0.6f, 0f, 27.6f);
            M("Sofa", r, 7.35f, 0f, 28.6f, 90f);

            // back room racks and pallets
            for (int row = 0; row < 2; row++)
            {
                int z = row == 0 ? 29 : 32;
                for (int x = 10; x <= W - 2; x++)
                {
                    if ((x - 10) % 8 == 7) continue;
                    M("Rack", r, x + 0.5f, 0f, z + 0.5f);
                }
            }
            M("Pallet", r, W - 1.3f, 0f, 33.4f);
            M("Pallet", r, W - 1.3f, 0f, 27.6f, 90f);

            Truck = M("Truck", r, W - 4f, 0f, Layout.DockRow + 0.6f);
            if (Truck != null) Truck.SetActive(false);
        }

        // ------------------------------------------------------------------ building (grey-box fallback)

        void BuildStaticPrimitives(int W, int slots)
        {
            var r = staticRoot;
            const float fy = -0.05f, fh = 0.05f;

            Mats.Box("Street", r, -6f, -6f, W + 12f, 9f, fy, fh, StreetCol);
            Mats.Box("Lobby", r, 0f, 3f, W, 9f, fy, fh, LobbyCol);
            for (int s = 0; s < slots; s++)
                Mats.Box("Slot Floor " + s, r, Layout.SlotX(s), Layout.FloorStart, Layout.SlotWidth, 14f, fy, fh, EmptySlotCol);
            Mats.Box("Back Room", r, Layout.OfficeWallX, Layout.BackWall, W - Layout.OfficeWallX, 9f, fy, fh, BackCol);
            Mats.Box("Office Floor", r, 0f, Layout.BackWall, Layout.OfficeWallX, 9f, fy, fh, OfficeCol);
            Mats.Box("Dock", r, -6f, Layout.RearWall, W + 12f, 8f, fy, fh, DockCol);

            const float wh = 1.4f, wt = 0.3f;
            Mats.Box("Wall Front L", r, 0f, 3.35f, 2f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Front R", r, 6f, 3.35f, W - 6f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Left", r, -wt, 3.35f, wt, 31.3f, 0f, wh, WallCol);
            Mats.Box("Wall Right", r, W, 3.35f, wt, 31.3f, 0f, wh, WallCol);
            for (int s = 0; s < slots; s++)
                Mats.Box("Wall Back " + s, r, Layout.SlotX(s), Layout.BackWall + 0.35f, 10f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Office A", r, Layout.OfficeWallX + 0.35f, Layout.BackStart, wt, 3f, 0f, wh, WallCol);
            Mats.Box("Wall Office B", r, Layout.OfficeWallX + 0.35f, 31f, wt, 3f, 0f, wh, WallCol);
            Mats.Box("Wall Rear L", r, 0f, Layout.RearWall + 0.35f, W - 6f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Rear R", r, W - 2f, Layout.RearWall + 0.35f, 2f, wt, 0f, wh, WallCol);

            for (int i = 0; i < store.Lanes.Count; i++)
            {
                var lane = store.Lanes[i];
                int cx = lane.CounterX;
                Mats.Box("Checkout " + (i + 1), r, cx + 0.1f, 6f, 0.8f, 2f, 0f, 0.9f, CounterCol, true);
                var light = Mats.Box("Checkout Light " + (i + 1), r, cx + 0.3f, 7.6f, 0.4f, 0.4f, 1.6f, 0.4f, Color.red);
                lane.Light = light.GetComponent<Renderer>();
            }

            Mats.Box("Desk", r, 2f, 31f, 3f, 1f, 0f, 0.8f, DeskCol, true);
            for (int row = 0; row < 2; row++)
            {
                int z = row == 0 ? 29 : 32;
                int start = 10;
                for (int x = 10; x <= W - 1; x++)
                {
                    bool gap = x > W - 2 || (x - 10) % 8 == 7;
                    if (gap)
                    {
                        if (x > start) Mats.Box("Rack", r, start + 0.05f, z + 0.1f, x - start - 0.1f, 0.8f, 0f, 2f, RackCol, true);
                        start = x + 1;
                    }
                }
            }
            Truck = Mats.Box("Delivery Truck", r, W - 7f, Layout.DockRow + 1.2f, 5f, 2.5f, 0f, 2.6f, new Color(0.9f, 0.9f, 0.92f));
            Truck.SetActive(false);
        }

        // ------------------------------------------------------------------ departments

        /// <summary>Lays out one department in its slot (obstacles, shelves, counters) and builds its visuals.</summary>
        public void BuildDepartment(Department d)
        {
            var g = store.Grid;
            int x0 = Layout.SlotX(d.Slot);
            g.ClearBlocked(x0, Layout.FloorStart, x0 + Layout.SlotWidth - 1, Layout.FloorEnd);

            if (d.Root != null) Object.Destroy(d.Root.gameObject);
            d.Root = new GameObject("Dept " + d.Name).transform;
            d.Root.SetParent(store.transform, false);
            d.Shelves.Clear();
            d.QueueTiles.Clear();
            d.FloorTiles.Clear();
            d.ClearRows();
            d.BackPick = BackPickFor(d.Slot, g.W);
            bool models = Models;

            if (models)
            {
                Floor(d.Root, "Floor", "floor_store", x0 + 0.1f, Layout.FloorStart, Layout.SlotWidth - 0.2f, 14f,
                    Color.Lerp(d.Def.Color, Color.white, 0.62f), 0.024f);
                M("Sign_" + d.Def.Id, d.Root, x0 + 5f, 1.62f, Layout.BackWall + 0.33f);
            }
            else
            {
                Color floor = Color.Lerp(d.Def.Color, Color.white, 0.55f);
                Mats.Box("Floor", d.Root, x0, Layout.FloorStart, Layout.SlotWidth, 14f, -0.04f, 0.05f, floor);
            }

            if (d.UnderConstruction)
            {
                for (int i = 0; i < 6; i++)
                {
                    float px = x0 + 1.5f + (i % 3) * 3.5f;
                    float pz = 15f + (i / 3) * 5f;
                    Mats.Box("Scaffold", d.Root, px, pz, 1.2f, 1.2f, 0f, 2.2f, ScaffoldCol);
                    if (models && i % 2 == 0) M("Pallet", d.Root, px + 2.2f, 0f, pz + 0.6f, 90f * i);
                }
                CollectFloorTiles(d, x0);
                return;
            }

            Color shelfCol = Color.Lerp(d.Def.Color, Color.black, 0.25f);
            switch (d.Def.Kind)
            {
                case DeptKind.Shelves:
                    AddShelf(d, x0 + 2);
                    AddShelf(d, x0 + 5);
                    AddShelf(d, x0 + 8);
                    d.ShelfCapacity = 300;
                    break;

                case DeptKind.Bakery:
                    AddShelf(d, x0 + 2);
                    AddShelf(d, x0 + 5);
                    for (int z = 21; z <= 23; z++) { g.SetBlocked(x0 + 9, z, true); g.SetBlocked(x0 + 10, z, true); }
                    if (models) M("Oven", d.Root, x0 + 10f, 0f, 22.5f);
                    else Mats.Box("Ovens", d.Root, x0 + 9.05f, 21.05f, 1.9f, 2.9f, 0f, 1.8f, new Color(0.35f, 0.3f, 0.28f), true);
                    d.WorkTile = new Vector2Int(x0 + 8, 22);
                    d.ShelfCapacity = 120;
                    break;

                case DeptKind.Counter:
                    for (int x = x0 + 1; x <= x0 + 10; x++) g.SetBlocked(x, 20, true);
                    for (int x = x0 + 2; x <= x0 + 9; x++) g.SetBlocked(x, 24, true);
                    if (!models)
                    {
                        Mats.Box("Counter", d.Root, x0 + 1.05f, 20.05f, 9.9f, 0.9f, 0f, 1.1f, shelfCol, true);
                        Mats.Box("Cold Room", d.Root, x0 + 2.05f, 24.05f, 7.9f, 0.9f, 0f, 2f, new Color(0.75f, 0.85f, 0.9f), true);
                    }
                    d.WorkTile = new Vector2Int(x0 + 5, 21);
                    d.ServiceTile = new Vector2Int(x0 + 5, 19);
                    for (int z = 17; z >= 13; z--) d.QueueTiles.Add(new Vector2Int(x0 + 5, z));
                    if (d.Stations.Count == 0)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            int sx = x0 + 2 + i * 3;
                            d.Stations.Add(new CounterStation { ServiceTile = new Vector2Int(sx, 19), WorkTile = new Vector2Int(sx, 21) });
                        }
                    }
                    d.ShelfCapacity = 160;
                    break;
            }

            int count = Mathf.Max(1, d.Products.Count);
            for (int i = 0; i < d.Products.Count; i++)
            {
                var p = d.Products[i];
                p.ShelfCap = d.ShelfCapacity / count;
                p.ShelfIndex = d.Shelves.Count > 0 ? i % d.Shelves.Count : 0;
                if (d.Shelves.Count > 0) d.Shelves[p.ShelfIndex].Products.Add(p);
            }

            if (models)
            {
                if (d.IsCounter) BuildCounterModels(d, x0);
                else for (int i = 0; i < d.Shelves.Count; i++) BuildShelfModels(d, d.Shelves[i]);
                d.ShuffleRows();
            }
            else
            {
                for (int i = 0; i < d.Shelves.Count; i++) BuildShelfBar(d, d.Shelves[i], shelfCol);
            }

            CollectFloorTiles(d, x0);
            d.UpdateVisuals();
        }

        const int ShelfZ0 = 14, ShelfZ1 = 23;

        void AddShelf(Department d, int x)
        {
            var g = store.Grid;
            var shelf = new ShelfUnit { Column = x };
            for (int z = ShelfZ0; z <= ShelfZ1; z++)
            {
                g.SetBlocked(x, z, true);
                shelf.Access.Add(new Vector2Int(x - 1, z));
                shelf.Access.Add(new Vector2Int(x + 1, z));
            }
            d.Shelves.Add(shelf);
        }

        void BuildShelfBar(Department d, ShelfUnit shelf, Color col)
        {
            int x = shelf.Column;
            float len = ShelfZ1 - ShelfZ0 + 1;
            Mats.Box("Shelf", d.Root, x + 0.1f, ShelfZ0 + 0.05f, 0.8f, len - 0.1f, 0f, 1.3f, col, true);
            var bar = Mats.Box("Stock", d.Root, x + 0.25f, ShelfZ0 + 0.05f, 0.5f, len - 0.1f, 1.3f, 0.18f, Color.green);
            shelf.FillBar = bar.transform;
            shelf.FillRenderer = bar.GetComponent<Renderer>();
            shelf.BarLength = len - 0.1f;
            shelf.BarStartZ = ShelfZ0 + 0.05f;
        }

        /// <summary>Fixture segments along the aisle plus a stock row on every level of every side.</summary>
        void BuildShelfModels(Department d, ShelfUnit shelf)
        {
            string fixture = d.Def.Fixture;
            float x = shelf.Column + 0.5f;
            int segments = ShelfZ1 - ShelfZ0 + 1;
            bool produce = fixture == "ProduceTable";
            float[] levels = fixture == "BakeryShelf" ? BakeryLevels : ShelfLevels;
            int nProducts = Mathf.Max(1, shelf.Products.Count);

            for (int k = 0; k < segments; k++)
            {
                float z = ShelfZ0 + k + 0.5f;
                M(fixture, d.Root, x, 0f, z);
                if (shelf.Products.Count == 0) continue;
                var p = shelf.Products[Mathf.Min(nProducts - 1, k * nProducts / segments)];
                string mesh = ModelLibrary.StockMesh(p.Def, false);
                for (int side = 0; side < 2; side++)
                {
                    float yaw = side == 0 ? 0f : 180f;
                    if (produce)
                    {
                        var row = M(mesh, d.Root, x, ProducePivotY, z, yaw, ProduceTilt);
                        if (row != null) d.AddRow(p, row);
                    }
                    else
                    {
                        for (int l = 0; l < levels.Length; l++)
                        {
                            var row = M(mesh, d.Root, x, levels[l], z, yaw);
                            if (row != null) d.AddRow(p, row);
                        }
                    }
                }
            }
            if (fixture == "Gondola")
            {
                M("GondolaEnd", d.Root, x, 0f, ShelfZ0);
                M("GondolaEnd", d.Root, x, 0f, ShelfZ1 + 1f, 180f);
            }
        }

        void BuildCounterModels(Department d, int x0)
        {
            int n = Mathf.Max(1, d.Products.Count);
            for (int i = 1; i <= 10; i++)
            {
                float x = x0 + i + 0.5f;
                M("DeliCounter", d.Root, x, 0f, 20.5f);
                if (d.Products.Count > 0)
                {
                    var p = d.Products[Mathf.Min(n - 1, (i - 1) * n / 10)];
                    var row = M(ModelLibrary.StockMesh(p.Def, true), d.Root, x, 0.76f, 20.5f);
                    if (row != null) d.AddRow(p, row);
                }
            }
            for (int i = 2; i <= 9; i++) M("DeliBack", d.Root, x0 + i + 0.5f, 0f, 24.5f);
            for (int i = 0; i < d.Stations.Count; i++)
                M("DeliScale", d.Root, d.Stations[i].ServiceTile.x + 0.5f, 0f, 20.75f, 180f);
        }

        void CollectFloorTiles(Department d, int x0)
        {
            var g = store.Grid;
            for (int z = Layout.FloorStart; z <= Layout.FloorEnd; z++)
                for (int x = x0; x < x0 + Layout.SlotWidth; x++)
                    if (g.Walkable(x, z, false)) d.FloorTiles.Add(new Vector2Int(x, z));
        }

        public void ClearSlot(int slot)
        {
            var g = store.Grid;
            int x0 = Layout.SlotX(slot);
            g.ClearBlocked(x0, Layout.FloorStart, x0 + Layout.SlotWidth - 1, Layout.FloorEnd);
        }
    }
}
