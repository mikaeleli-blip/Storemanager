using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public enum Zone : byte { Outside, Wall, Front, Floor, Back, Office, Dock }

    /// <summary>Fixed rows of the store, front (street) to back (dock). See docs/DESIGN.md.</summary>
    public static class Layout
    {
        public const int SlotWidth = 12;
        public const int Depth = 36;
        public const int StreetEnd = 2;
        public const int FrontWall = 3;
        public const int LobbyStart = 4;
        public const int FloorStart = 12;
        public const int FloorEnd = 25;
        public const int BackWall = 26;
        public const int BackStart = 27;
        public const int BackEnd = 33;
        public const int RearWall = 34;
        public const int DockRow = 35;
        public const int OfficeEndX = 7;
        public const int OfficeWallX = 8;
        public const int StartSlots = 4;
        public const int MaxSlots = 7;

        public static int Width(int slots) { return slots * SlotWidth; }
        public static int SlotX(int slot) { return slot * SlotWidth; }
        public static int LaneCount(int slots) { return slots + 1; }
        public static int LaneCounterX(int lane) { return 10 + lane * 3; }

        public static int SlotOf(int x, int z)
        {
            if (z < FloorStart || z > FloorEnd || x < 0) return -1;
            return x / SlotWidth;
        }
    }

    /// <summary>Tile grid (1 m tiles) with zones, obstacles and A* pathfinding.</summary>
    public class StoreGrid
    {
        public readonly int W, D;
        readonly Zone[] zones;
        readonly bool[] blocked;

        // A* scratch
        readonly float[] g;
        readonly int[] parent;
        readonly int[] seen;
        readonly int[] closed;
        int searchId;
        readonly MinHeap heap;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public StoreGrid(int w, int d)
        {
            W = w; D = d;
            int n = w * d;
            zones = new Zone[n];
            blocked = new bool[n];
            g = new float[n];
            parent = new int[n];
            seen = new int[n];
            closed = new int[n];
            heap = new MinHeap(n);
        }

        public bool In(int x, int z) { return x >= 0 && z >= 0 && x < W && z < D; }
        int Idx(int x, int z) { return z * W + x; }

        public Zone ZoneAt(int x, int z) { return In(x, z) ? zones[Idx(x, z)] : Zone.Wall; }
        public Zone ZoneAt(Vector2Int t) { return ZoneAt(t.x, t.y); }
        public void SetZone(int x, int z, Zone zone) { if (In(x, z)) zones[Idx(x, z)] = zone; }
        public void SetBlocked(int x, int z, bool b) { if (In(x, z)) blocked[Idx(x, z)] = b; }
        public bool IsBlocked(int x, int z) { return !In(x, z) || blocked[Idx(x, z)]; }

        public void FillZone(int x0, int z0, int x1, int z1, Zone zone)
        {
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    SetZone(x, z, zone);
        }

        public void ClearBlocked(int x0, int z0, int x1, int z1)
        {
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    SetBlocked(x, z, false);
        }

        public static bool CustomerZone(Zone z) { return z == Zone.Outside || z == Zone.Front || z == Zone.Floor; }

        public bool Walkable(int x, int z, bool staff)
        {
            if (!In(x, z)) return false;
            int i = Idx(x, z);
            if (blocked[i]) return false;
            Zone zone = zones[i];
            if (zone == Zone.Wall) return false;
            if (!staff && !CustomerZone(zone)) return false;
            return true;
        }

        public bool Walkable(Vector2Int t, bool staff) { return Walkable(t.x, t.y, staff); }

        public static Vector3 World(Vector2Int t) { return new Vector3(t.x + 0.5f, 0f, t.y + 0.5f); }
        public static Vector2Int TileAt(Vector3 p) { return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)); }

        public Vector2Int NearestWalkable(Vector2Int t, bool staff, int maxRadius = 8)
        {
            if (Walkable(t, staff)) return t;
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        if (Walkable(t.x + dx, t.y + dz, staff)) return new Vector2Int(t.x + dx, t.y + dz);
                    }
                }
            }
            return new Vector2Int(-1, -1);
        }

        static float Octile(int ax, int az, int bx, int bz)
        {
            int dx = Mathf.Abs(ax - bx), dz = Mathf.Abs(az - bz);
            return (dx + dz) + (1.4142f - 2f) * Mathf.Min(dx, dz);
        }

        /// <summary>Returns world-space waypoints (excluding the start), or null if unreachable.</summary>
        public List<Vector3> FindPath(Vector2Int from, Vector2Int to, bool staff)
        {
            from = NearestWalkable(from, staff);
            to = NearestWalkable(to, staff);
            if (from.x < 0 || to.x < 0) return null;
            var result = new List<Vector3>();
            if (from == to)
            {
                result.Add(World(to));
                return result;
            }

            searchId++;
            heap.Clear();
            int s = Idx(from.x, from.y), goal = Idx(to.x, to.y);
            g[s] = 0f; seen[s] = searchId; parent[s] = -1;
            heap.Push(s, Octile(from.x, from.y, to.x, to.y));
            bool found = false;

            while (heap.Count > 0)
            {
                int cur = heap.Pop();
                if (cur == goal) { found = true; break; }
                if (closed[cur] == searchId) continue;
                closed[cur] = searchId;
                int cx = cur % W, cz = cur / W;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], nz = cz + DZ[d];
                    if (!Walkable(nx, nz, staff)) continue;
                    bool diag = d >= 4;
                    if (diag && (!Walkable(cx + DX[d], cz, staff) || !Walkable(cx, cz + DZ[d], staff))) continue;
                    int n = Idx(nx, nz);
                    if (closed[n] == searchId) continue;
                    float ng = g[cur] + (diag ? 1.4142f : 1f);
                    if (seen[n] != searchId || ng < g[n])
                    {
                        seen[n] = searchId;
                        g[n] = ng;
                        parent[n] = cur;
                        heap.Push(n, ng + Octile(nx, nz, to.x, to.y));
                    }
                }
            }
            if (!found) return null;

            var tiles = new List<Vector2Int>();
            for (int c = goal; c != -1 && c != s; c = parent[c])
                tiles.Add(new Vector2Int(c % W, c / W));
            tiles.Reverse();
            Smooth(from, tiles, staff, result);
            return result;
        }

        /// <summary>Drops waypoints that have a clear straight line from the previous kept point.</summary>
        void Smooth(Vector2Int from, List<Vector2Int> tiles, bool staff, List<Vector3> output)
        {
            Vector3 anchor = World(from);
            int i = 0;
            while (i < tiles.Count)
            {
                int best = i;
                for (int j = Mathf.Min(tiles.Count - 1, i + 12); j > i; j--)
                {
                    if (LineClear(anchor, World(tiles[j]), staff)) { best = j; break; }
                }
                Vector3 p = World(tiles[best]);
                output.Add(p);
                anchor = p;
                i = best + 1;
            }
        }

        bool LineClear(Vector3 a, Vector3 b, bool staff)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            int steps = Mathf.CeilToInt(len / 0.2f);
            Vector3 side = len > 0.001f ? new Vector3(-d.z, 0f, d.x) / len * 0.3f : Vector3.zero;
            for (int k = 1; k < steps; k++)
            {
                Vector3 p = a + d * (k / (float)steps);
                if (!Walkable(TileAt(p), staff)) return false;
                if (!Walkable(TileAt(p + side), staff)) return false;
                if (!Walkable(TileAt(p - side), staff)) return false;
            }
            return true;
        }

        public bool WalkableAt(Vector3 p, bool staff)
        {
            return Walkable(TileAt(p), staff);
        }

        class MinHeap
        {
            readonly int[] items;
            readonly float[] keys;
            public int Count;

            public MinHeap(int capacity)
            {
                // Nodes can be pushed more than once (lazy deletion), so leave headroom.
                items = new int[capacity * 8 + 16];
                keys = new float[capacity * 8 + 16];
            }

            public void Clear() { Count = 0; }

            public void Push(int item, float key)
            {
                if (Count >= items.Length) return;
                int i = Count++;
                items[i] = item; keys[i] = key;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public int Pop()
            {
                int top = items[0];
                Count--;
                if (Count > 0)
                {
                    items[0] = items[Count]; keys[0] = keys[Count];
                    int i = 0;
                    while (true)
                    {
                        int l = i * 2 + 1, r = l + 1, m = i;
                        if (l < Count && keys[l] < keys[m]) m = l;
                        if (r < Count && keys[r] < keys[m]) m = r;
                        if (m == i) break;
                        Swap(i, m);
                        i = m;
                    }
                }
                return top;
            }

            void Swap(int a, int b)
            {
                int ti = items[a]; items[a] = items[b]; items[b] = ti;
                float tk = keys[a]; keys[a] = keys[b]; keys[b] = tk;
            }
        }
    }
}
