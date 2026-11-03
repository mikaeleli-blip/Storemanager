using UnityEngine;

namespace StoreManager
{
    /// <summary>Builds the tile grid and the grey-box visuals for the current store size.</summary>
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

        public StoreBuilder(Store store) { this.store = store; }

        public static Vector2Int BackPickFor(int slot, int width)
        {
            return new Vector2Int(Mathf.Clamp(Layout.SlotX(slot) + 6, 10, width - 2), 28);
        }

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

            // Checkout counters
            for (int i = 0; i < store.Lanes.Count; i++)
            {
                int cx = store.Lanes[i].CounterX;
                g.SetBlocked(cx, 6, true);
                g.SetBlocked(cx, 7, true);
            }
            // Office desk
            for (int x = 2; x <= 4; x++) g.SetBlocked(x, 31, true);
            // Back-room racks, with gaps to walk through
            for (int x = 10; x <= W - 2; x++)
            {
                if ((x - 10) % 8 == 7) continue;
                g.SetBlocked(x, 29, true);
                g.SetBlocked(x, 32, true);
            }

            if (staticRoot != null) Object.Destroy(staticRoot.gameObject);
            staticRoot = new GameObject("Store Building").transform;
            staticRoot.SetParent(store.transform, false);
            BuildStatic(W, slots);

            for (int s = 0; s < store.Slots.Length; s++)
            {
                var d = store.Slots[s];
                if (s < slots && d != null) BuildDepartment(d);
            }
        }

        void BuildStatic(int W, int slots)
        {
            var r = staticRoot;
            const float fy = -0.05f, fh = 0.05f;

            // Floors
            Mats.Box("Street", r, -6f, -6f, W + 12f, 9f, fy, fh, StreetCol);
            Mats.Box("Lobby", r, 0f, 3f, W, 9f, fy, fh, LobbyCol);
            for (int s = 0; s < slots; s++)
            {
                Mats.Box("Slot Floor " + s, r, Layout.SlotX(s), Layout.FloorStart, Layout.SlotWidth, 14f, fy, fh, EmptySlotCol);
            }
            Mats.Box("Back Room", r, Layout.OfficeWallX, Layout.BackWall, W - Layout.OfficeWallX, 9f, fy, fh, BackCol);
            Mats.Box("Office Floor", r, 0f, Layout.BackWall, Layout.OfficeWallX, 9f, fy, fh, OfficeCol);
            Mats.Box("Dock", r, -6f, Layout.RearWall, W + 12f, 8f, fy, fh, DockCol);

            // Walls (kept low so the top-down camera sees in)
            const float wh = 1.4f, wt = 0.3f;
            Mats.Box("Wall Front L", r, 0f, 3.35f, 2f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Front R", r, 6f, 3.35f, W - 6f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Left", r, -wt, 3.35f, wt, 31.3f, 0f, wh, WallCol);
            Mats.Box("Wall Right", r, W, 3.35f, wt, 31.3f, 0f, wh, WallCol);
            for (int s = 0; s < slots; s++)
            {
                Mats.Box("Wall Back " + s, r, Layout.SlotX(s), Layout.BackWall + 0.35f, 10f, wt, 0f, wh, WallCol);
            }
            Mats.Box("Wall Office A", r, Layout.OfficeWallX + 0.35f, Layout.BackStart, wt, 3f, 0f, wh, WallCol);
            Mats.Box("Wall Office B", r, Layout.OfficeWallX + 0.35f, 31f, wt, 3f, 0f, wh, WallCol);
            Mats.Box("Wall Rear L", r, 0f, Layout.RearWall + 0.35f, W - 6f, wt, 0f, wh, WallCol);
            Mats.Box("Wall Rear R", r, W - 2f, Layout.RearWall + 0.35f, 2f, wt, 0f, wh, WallCol);

            // Checkouts
            for (int i = 0; i < store.Lanes.Count; i++)
            {
                var lane = store.Lanes[i];
                int cx = lane.CounterX;
                Mats.Box("Checkout " + (i + 1), r, cx + 0.1f, 6f, 0.8f, 2f, 0f, 0.9f, CounterCol, true);
                var light = Mats.Box("Checkout Light " + (i + 1), r, cx + 0.3f, 7.6f, 0.4f, 0.4f, 1.6f, 0.4f, Color.red);
                lane.Light = light.GetComponent<Renderer>();
            }

            // Office
            Mats.Box("Desk", r, 2f, 31f, 3f, 1f, 0f, 0.8f, DeskCol, true);
            Mats.Box("Desk Screen", r, 3f, 31.5f, 1f, 0.1f, 0.8f, 0.5f, new Color(0.1f, 0.1f, 0.12f));

            // Racks
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

            // Delivery truck (shown when a delivery arrives)
            Truck = Mats.Box("Delivery Truck", r, W - 7f, Layout.DockRow + 1.2f, 5f, 2.5f, 0f, 2.6f, new Color(0.9f, 0.9f, 0.92f));
            Truck.SetActive(false);
        }

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
            d.BackPick = BackPickFor(d.Slot, g.W);

            Color floor = Color.Lerp(d.Def.Color, Color.white, 0.55f);
            Mats.Box("Floor", d.Root, x0, Layout.FloorStart, Layout.SlotWidth, 14f, -0.04f, 0.05f, floor);

            if (d.UnderConstruction)
            {
                for (int i = 0; i < 6; i++)
                {
                    float px = x0 + 1.5f + (i % 3) * 3.5f;
                    float pz = 15f + (i / 3) * 5f;
                    Mats.Box("Scaffold", d.Root, px, pz, 1.2f, 1.2f, 0f, 2.2f, ScaffoldCol);
                }
                CollectFloorTiles(d, x0);
                return;
            }

            Color shelfCol = Color.Lerp(d.Def.Color, Color.black, 0.25f);
            switch (d.Def.Kind)
            {
                case DeptKind.Shelves:
                    AddShelf(d, x0 + 2, shelfCol);
                    AddShelf(d, x0 + 5, shelfCol);
                    AddShelf(d, x0 + 8, shelfCol);
                    d.ShelfCapacity = 300;
                    break;

                case DeptKind.Bakery:
                    AddShelf(d, x0 + 2, shelfCol);
                    AddShelf(d, x0 + 5, shelfCol);
                    for (int z = 21; z <= 23; z++) { g.SetBlocked(x0 + 9, z, true); g.SetBlocked(x0 + 10, z, true); }
                    Mats.Box("Ovens", d.Root, x0 + 9.05f, 21.05f, 1.9f, 2.9f, 0f, 1.8f, new Color(0.35f, 0.3f, 0.28f), true);
                    d.WorkTile = new Vector2Int(x0 + 8, 22);
                    d.ShelfCapacity = 120;
                    break;

                case DeptKind.Counter:
                    for (int x = x0 + 1; x <= x0 + 10; x++) g.SetBlocked(x, 20, true);
                    for (int x = x0 + 2; x <= x0 + 9; x++) g.SetBlocked(x, 24, true);
                    Mats.Box("Counter", d.Root, x0 + 1.05f, 20.05f, 9.9f, 0.9f, 0f, 1.1f, shelfCol, true);
                    Mats.Box("Cold Room", d.Root, x0 + 2.05f, 24.05f, 7.9f, 0.9f, 0f, 2f, new Color(0.75f, 0.85f, 0.9f), true);
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
            CollectFloorTiles(d, x0);
            d.UpdateVisuals();
        }

        void AddShelf(Department d, int x, Color col)
        {
            var g = store.Grid;
            const int z0 = 14, z1 = 23;
            var shelf = new ShelfUnit();
            for (int z = z0; z <= z1; z++)
            {
                g.SetBlocked(x, z, true);
                shelf.Access.Add(new Vector2Int(x - 1, z));
                shelf.Access.Add(new Vector2Int(x + 1, z));
            }
            float len = z1 - z0 + 1;
            Mats.Box("Shelf", d.Root, x + 0.1f, z0 + 0.05f, 0.8f, len - 0.1f, 0f, 1.3f, col, true);
            var bar = Mats.Box("Stock", d.Root, x + 0.25f, z0 + 0.05f, 0.5f, len - 0.1f, 1.3f, 0.18f, Color.green);
            shelf.FillBar = bar.transform;
            shelf.FillRenderer = bar.GetComponent<Renderer>();
            shelf.BarLength = len - 0.1f;
            shelf.BarStartZ = z0 + 0.05f;
            d.Shelves.Add(shelf);
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
