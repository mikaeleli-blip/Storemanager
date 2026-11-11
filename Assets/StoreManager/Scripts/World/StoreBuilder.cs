using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Builds the tile grid and the building (walls, office, back room, outside), plus the visuals of every
    /// placed fixture. Uses the Blender models from ModelLibrary; falls back to coloured primitives without them.
    /// </summary>
    public class StoreBuilder
    {
        readonly Store store;
        Transform staticRoot;
        public GameObject Truck;

        static readonly Color StreetCol = new Color(0.32f, 0.33f, 0.35f);
        static readonly Color LobbyCol = new Color(0.86f, 0.84f, 0.8f);
        static readonly Color BackCol = new Color(0.55f, 0.56f, 0.58f);
        static readonly Color OfficeCol = new Color(0.55f, 0.42f, 0.3f);
        static readonly Color DockCol = new Color(0.25f, 0.25f, 0.27f);
        static readonly Color WallCol = new Color(0.93f, 0.92f, 0.88f);
        static readonly Color RackCol = new Color(0.3f, 0.38f, 0.5f);
        static readonly Color DeskCol = new Color(0.4f, 0.27f, 0.15f);

        const float ProduceTilt = -14f;

        public StoreBuilder(Store store) { this.store = store; }

        static bool Models { get { return ModelLibrary.Available; } }

        // ------------------------------------------------------------------ grid

        /// <summary>Rebuilds the grid and the building. Call Store.ReapplyFixtures afterwards.</summary>
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

            for (int x = 2; x <= 4; x++) g.SetBlocked(x, 31, true);          // desk
            for (int x = 10; x <= W - 2; x++)                                  // racks
            {
                if ((x - 10) % 8 == 7) continue;
                g.SetBlocked(x, 29, true);
                g.SetBlocked(x, 32, true);
            }
            BlockAll(g, new[] { new Vector2Int(0, 33), new Vector2Int(1, 33), new Vector2Int(7, 33), new Vector2Int(0, 27),
                                new Vector2Int(7, 27), new Vector2Int(7, 28), new Vector2Int(7, 29), new Vector2Int(0, 4),
                                new Vector2Int(1, 4), new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(0, 6),
                                new Vector2Int(1, 6), new Vector2Int(6, 4), new Vector2Int(W - 2, 33), new Vector2Int(W - 1, 33),
                                new Vector2Int(W - 2, 27), new Vector2Int(W - 1, 27) });

            if (staticRoot != null) Object.Destroy(staticRoot.gameObject);
            staticRoot = new GameObject("Store Building").transform;
            staticRoot.SetParent(store.transform, false);
            if (Models) BuildStaticModels(W, slots);
            else BuildStaticPrimitives(W, slots);
        }

        static void BlockAll(StoreGrid g, Vector2Int[] tiles)
        {
            for (int i = 0; i < tiles.Length; i++) g.SetBlocked(tiles[i].x, tiles[i].y, true);
        }

        // ------------------------------------------------------------------ department signs

        Transform signRoot;
        readonly Dictionary<string, Transform> signs = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> signSum = new Dictionary<string, Vector3>();
        readonly Dictionary<string, int> signCount = new Dictionary<string, int>();
        readonly List<string> signGone = new List<string>();
        public const float SignHeight = 2.35f;

        /// <summary>
        /// Hangs a department sign (Sign_&lt;category&gt;) over the middle of each department's shelves, and moves or
        /// takes it down as the shelves change.
        /// </summary>
        public void UpdateSigns()
        {
            if (!Models) return;
            if (signRoot == null)
            {
                signRoot = new GameObject("Department Signs").transform;
                signRoot.SetParent(store.transform, false);
            }
            signSum.Clear();
            signCount.Clear();
            for (int i = 0; i < store.Fixtures.Count; i++)
            {
                var f = store.Fixtures[i];
                if (f.Removed || f.Product == null || !f.HoldsProducts) continue;
                string id = f.Product.Category.Def.Id;
                Vector3 s;
                signSum.TryGetValue(id, out s);
                signSum[id] = s + f.Center;
                int n;
                signCount.TryGetValue(id, out n);
                signCount[id] = n + 1;
            }
            signGone.Clear();
            foreach (var kv in signs) if (!signCount.ContainsKey(kv.Key)) signGone.Add(kv.Key);
            for (int i = 0; i < signGone.Count; i++)
            {
                if (signs[signGone[i]] != null) Object.Destroy(signs[signGone[i]].gameObject);
                signs.Remove(signGone[i]);
            }
            foreach (var kv in signCount)
            {
                Vector3 p = signSum[kv.Key] / kv.Value;
                p.y = SignHeight;
                Transform t;
                if (!signs.TryGetValue(kv.Key, out t) || t == null)
                {
                    string model = "Sign_" + kv.Key;
                    if (!ModelLibrary.Has(model)) continue;
                    var go = M(model, signRoot, p.x, p.y, p.z);
                    if (go == null) continue;
                    signs[kv.Key] = go.transform;
                }
                else if ((t.localPosition - p).sqrMagnitude > 0.0001f) t.localPosition = p;
            }
        }

        // ------------------------------------------------------------------ building (models)

        static GameObject M(string name, Transform parent, float x, float y, float z, float yaw = 0f, float tilt = 0f)
        {
            return ModelLibrary.Spawn(name, parent, new Vector3(x, y, z), yaw, tilt);
        }

        /// <summary>
        /// A wall piece with Codex's surface textures on both faces: painted blocks facing the back room and the
        /// yard, plaster facing the shop, the office and the street side.
        /// </summary>
        static void Wall(string name, Transform parent, float x, float z, float yaw = 0f)
        {
            var at = new Vector3(x, 0f, z);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            ModelLibrary.SpawnFaced(name, parent, at, yaw, WallSurface(at + rot * new Vector3(0f, 0f, -0.4f)),
                WallSurface(at + rot * new Vector3(0f, 0f, 0.4f)));
        }

        static string WallSurface(Vector3 p)
        {
            bool backRoom = p.z > Layout.BackWall + 0.5f && p.z < Layout.RearWall + 0.5f && p.x > Layout.OfficeWallX + 0.5f;
            bool yard = p.z > Layout.RearWall + 0.5f;
            return backRoom || yard ? "wall_block" : "wall_plaster";
        }

        static void Floor(Transform parent, string name, string tex, float x, float z, float w, float d, Color tint, float y = 0f)
        {
            var go = Mats.Box(name, parent, x, z, w, d, y - 0.05f, 0.05f, Color.white);
            go.GetComponent<Renderer>().sharedMaterial = Mats.Textured(tex, new Vector2(w, d), tint);
        }

        void BuildStaticModels(int W, int slots)
        {
            var r = staticRoot;

            Floor(r, "Car Park", "asphalt", -16f, -18f, W + 32f, 18f, Color.white);
            Floor(r, "Sidewalk", "sidewalk", -16f, 0f, W + 32f, 3.4f, Color.white);
            Floor(r, "Shop Floor", "floor_store", 0f, 3f, W, 23f, Color.white);
            Floor(r, "Back Room", "floor_back", Layout.OfficeWallX, Layout.BackWall, W - Layout.OfficeWallX, 9f, Color.white);
            Floor(r, "Office Floor", "floor_office", 0f, Layout.BackWall, Layout.OfficeWallX, 9f, Color.white);
            Floor(r, "Yard", "asphalt", -16f, Layout.RearWall, W + 32f, 14f, new Color(0.85f, 0.85f, 0.85f));

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

            for (int x = 0; x < W; x++)
            {
                if (x >= 2 && x <= 5) continue;
                M("WallWindow", r, x + 0.5f, 0f, 3.5f);
            }
            M("Entrance", r, 4f, 0f, 3.5f);
            M("StoreSign", r, Mathf.Max(13f, W * 0.5f + 2f), 1.85f, 3.5f);

            // Side walls: full 1 m segments between the front and rear walls, half segments into the corners
            // so the 0.3 m wall bodies meet without gaps (Codex, HANDOFF 2026-11-09).
            for (int z = Layout.LobbyStart; z <= Layout.BackEnd; z++)
            {
                Wall("Wall", r, -0.15f, z + 0.5f, 90f);
                Wall("Wall", r, W + 0.15f, z + 0.5f, 90f);
            }
            foreach (float z in new[] { Layout.FrontWall + 0.75f, Layout.RearWall + 0.25f })
            {
                Wall("WallHalf", r, -0.15f, z, 90f);
                Wall("WallHalf", r, W + 0.15f, z, 90f);
            }
            M("WallPost", r, -0.15f, 0f, 3.5f);
            M("WallPost", r, W + 0.15f, 0f, 3.5f);
            M("WallPost", r, -0.15f, 0f, 34.5f);
            M("WallPost", r, W + 0.15f, 0f, 34.5f);

            for (int x = 0; x < W; x++)
            {
                int local = x % Layout.SlotWidth;
                if (local == 10 || local == 11) continue;
                Wall("Wall", r, x + 0.5f, Layout.BackWall + 0.5f);
            }
            for (int s = 0; s < slots; s++)
            {
                M("WallPost", r, Layout.SlotX(s) + 10f, 0f, Layout.BackWall + 0.5f);
                M("WallPost", r, Layout.SlotX(s) + 12f, 0f, Layout.BackWall + 0.5f);
            }

            for (int z = Layout.BackStart; z <= Layout.BackEnd; z++)
            {
                if (z == 30) continue;   // office doorway
                Wall("Wall", r, Layout.OfficeWallX + 0.5f, z + 0.5f, 90f);
            }
            Wall("WallHalf", r, Layout.OfficeWallX + 0.5f, Layout.BackWall + 0.75f, 90f);
            Wall("WallHalf", r, Layout.OfficeWallX + 0.5f, Layout.RearWall + 0.25f, 90f);

            for (int x = 0; x < W; x++)
            {
                if (x >= W - 6 && x <= W - 3) continue;
                Wall("Wall", r, x + 0.5f, Layout.RearWall + 0.5f);
            }
            M("DockDoor", r, W - 4f, 0f, Layout.RearWall + 0.5f);

            M("CartCorral", r, 0.9f, 0f, 4.6f);
            M("BasketStack", r, 6.5f, 0f, 4.5f);

            M("Desk", r, 3.5f, 0f, 31.5f);
            M("OfficeChair", r, 3.5f, 0f, 30.55f);
            M("FilingCabinet", r, 0.5f, 0f, 33.5f);
            M("FilingCabinet", r, 1.15f, 0f, 33.5f);
            M("Whiteboard", r, 4.8f, 0f, 34.36f);
            M("Plant", r, 7.4f, 0f, 33.4f);
            M("Plant", r, 0.6f, 0f, 27.6f);
            M("Sofa", r, 7.35f, 0f, 28.6f, 90f);

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
            Mats.Box("Shop Floor", r, 0f, 3f, W, 23f, fy, fh, LobbyCol);
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

        // ------------------------------------------------------------------ fixtures

        static Color KindColor(FixtureDef d)
        {
            switch (d.Id)
            {
                case "fridge": return new Color(0.5f, 0.7f, 0.9f);
                case "freezer": return new Color(0.75f, 0.88f, 0.95f);
                case "produce": return new Color(0.55f, 0.4f, 0.25f);
                case "bakery_shelf": return new Color(0.8f, 0.65f, 0.45f);
                case "deli_counter": return new Color(0.85f, 0.4f, 0.35f);
                case "oven": return new Color(0.45f, 0.45f, 0.48f);
                case "checkout": return new Color(0.25f, 0.27f, 0.3f);
                default: return new Color(0.85f, 0.86f, 0.88f);
            }
        }

        /// <summary>Spawns the fixture's model segments under parent. Returns false if a model was missing.</summary>
        bool SpawnFixtureModel(FixtureDef def, Vector2Int origin, int rot, Transform parent)
        {
            float yaw = Placement.Yaw(rot);
            bool ok = true;
            for (int i = 0; i < def.Segments.Length; i++)
            {
                var p = Placement.Point(def, origin, rot, def.Segments[i].x, def.Segments[i].y);
                if (M(def.Model, parent, p.x, 0f, p.z, yaw) == null) ok = false;
            }
            return ok;
        }

        public void BuildFixture(Fixture f)
        {
            if (f.Root != null) Object.Destroy(f.Root.gameObject);
            f.Root = new GameObject(f.Def.Name + " " + f.Id).transform;
            f.Root.SetParent(store.transform, false);
            f.Rows.Clear();
            f.ResetVisuals();
            float yaw = Placement.Yaw(f.Rot);

            bool models = Models && SpawnFixtureModel(f.Def, f.Origin, f.Rot, f.Root);
            if (!models)
            {
                for (int i = 0; i < f.Tiles.Count; i++)
                {
                    var t = f.Tiles[i];
                    float h = f.IsCheckout ? 0.9f : f.IsOven ? 1.8f : f.IsCounter ? 1.1f : 1.3f;
                    Mats.Box("Block", f.Root, t.x + 0.08f, t.y + 0.08f, 0.84f, 0.84f, 0f, h, KindColor(f.Def));
                }
            }

            if (f.IsCheckout)
            {
                var p = Placement.Point(f.Def, f.Origin, f.Rot, 0.5f, 1.85f);
                if (models) M("LanePole", f.Root, p.x, 0f, p.z, yaw);
                var light = Mats.Prim(PrimitiveType.Sphere, "Lane Light", f.Root, new Vector3(p.x, models ? 2.2f : 1.4f, p.z),
                    new Vector3(0.2f, 0.2f, 0.2f), Color.red, false);
                if (f.Lane != null) f.Lane.Light = light.GetComponent<Renderer>();
            }
            if (f.IsCounter && models)
            {
                var p = Placement.Point(f.Def, f.Origin, f.Rot, 1.5f, 0.75f);
                M("DeliScale", f.Root, p.x, 0f, p.z, yaw + 180f);
            }
            BuildStockRows(f);
        }

        /// <summary>(Re)builds the product rows on a shelf or counter for its current product.</summary>
        public void BuildStockRows(Fixture f)
        {
            if (f.Root == null) return;
            var old = f.Root.Find("Stock");
            if (old != null) { old.name = "Stock (old)"; Object.Destroy(old.gameObject); }
            f.ClearVisuals();
            if (f.Product == null || !f.HoldsProducts) return;

            var root = new GameObject("Stock").transform;
            root.SetParent(f.Root, false);
            float yaw = Placement.Yaw(f.Rot);
            var def = f.Def;

            var layout = Models && !f.IsCounter ? StockLayout.For(f.Product.Def.Name) : null;
            if (layout != null)
            {
                // Shelves: rows built unit by unit, so what you see is what's on the shelf.
                int seed = f.Id * 7919;
                for (int s = 0; s < def.Segments.Length; s++)
                {
                    var p = Placement.Point(def, f.Origin, f.Rot, def.Segments[s].x, def.Segments[s].y);
                    for (int side = 0; side < 2; side++)
                        for (int l = 0; l < def.Levels.Length; l++)
                        {
                            var rot = Quaternion.Euler(0f, yaw + (side == 0 ? 0f : 180f), def.Tilted ? ProduceTilt : 0f);
                            f.UnitRows.Add(new StockRow(root, new Vector3(p.x, def.Levels[l], p.z), rot, layout, seed++));
                        }
                }
                var rr = new System.Random(f.Id * 31);
                for (int i = f.UnitRows.Count - 1; i > 0; i--)
                {
                    int j = rr.Next(i + 1);
                    var t = f.UnitRows[i]; f.UnitRows[i] = f.UnitRows[j]; f.UnitRows[j] = t;
                }
            }
            else if (Models)
            {
                string mesh = ModelLibrary.StockMesh(f.Product.Def, f.IsCounter);
                for (int s = 0; s < def.Segments.Length; s++)
                {
                    var p = Placement.Point(def, f.Origin, f.Rot, def.Segments[s].x, def.Segments[s].y);
                    if (f.IsCounter)
                    {
                        var row = M(mesh, root, p.x, 0.76f, p.z, yaw);
                        if (row != null) f.Rows.Add(row);
                        continue;
                    }
                    for (int side = 0; side < 2; side++)
                    {
                        float y2 = yaw + (side == 0 ? 0f : 180f);
                        for (int l = 0; l < def.Levels.Length; l++)
                        {
                            var row = M(mesh, root, p.x, def.Levels[l], p.z, y2, def.Tilted ? ProduceTilt : 0f);
                            if (row != null) f.Rows.Add(row);
                        }
                    }
                }
            }
            else
            {
                Color c = f.Product.Category.Def.Color;
                for (int i = 0; i < f.Tiles.Count; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        var t = f.Tiles[i];
                        f.Rows.Add(Mats.Box("Stock", root, t.x + 0.2f, t.y + 0.2f, 0.6f, 0.6f, 1.3f + k * 0.12f, 0.1f, c));
                    }
            }

            // Shuffle so a half-empty shelf has gaps spread around.
            var rng = new System.Random(f.Id * 7919);
            for (int i = f.Rows.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = f.Rows[i]; f.Rows[i] = f.Rows[j]; f.Rows[j] = t;
            }
            f.UpdateVisuals();
        }

        public void DestroyFixture(Fixture f)
        {
            if (f.Root != null) Object.Destroy(f.Root.gameObject);
            f.Root = null;
            f.ClearVisuals();
        }

        /// <summary>A see-through preview of a fixture for build mode.</summary>
        public GameObject BuildGhost(FixtureDef def, Vector2Int origin, int rot, Material mat, Material markerMat)
        {
            var root = new GameObject("Ghost").transform;
            root.SetParent(store.transform, false);
            bool models = Models && SpawnFixtureModel(def, origin, rot, root);
            var probe = new Fixture(def, -1);
            probe.SetPlacement(origin, rot);
            if (!models)
                for (int i = 0; i < probe.Tiles.Count; i++)
                    Mats.Box("Block", root, probe.Tiles[i].x + 0.08f, probe.Tiles[i].y + 0.08f, 0.84f, 0.84f, 0f, 1.2f, Color.white);
            var rs = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                var mats = new Material[rs[i].sharedMaterials.Length];
                for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                rs[i].sharedMaterials = mats;
            }
            // footprint and the spots people stand on
            for (int i = 0; i < probe.Tiles.Count; i++)
                Mats.Box("Tile", root, probe.Tiles[i].x + 0.04f, probe.Tiles[i].y + 0.04f, 0.92f, 0.92f, 0.03f, 0.02f, Color.white)
                    .GetComponent<Renderer>().sharedMaterial = mat;
            if (def.HasWork) Marker(root, probe.Work, markerMat);
            if (def.HasService) Marker(root, probe.Service, markerMat);
            if (def.HasCashier) Marker(root, probe.Cashier, markerMat);
            return root.gameObject;
        }

        static void Marker(Transform root, Vector2Int t, Material mat)
        {
            Mats.Box("Spot", root, t.x + 0.25f, t.y + 0.25f, 0.5f, 0.5f, 0.03f, 0.03f, Color.white)
                .GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
