using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Footprint maths and placement rules. A fixture is W x D tiles in local space with its origin at the
    /// footprint's min corner; rotation r (0..3) turns it 90 degrees clockwise seen from above (Unity yaw 90 * r).
    /// </summary>
    public static class Placement
    {
        public static Vector2Int Size(FixtureDef d, int rot)
        {
            return (rot & 1) == 0 ? new Vector2Int(d.W, d.D) : new Vector2Int(d.D, d.W);
        }

        public static Vector2Int Tile(FixtureDef d, Vector2Int origin, int rot, int i, int j)
        {
            int x, z;
            switch (rot & 3)
            {
                case 0: x = i; z = j; break;
                case 1: x = j; z = d.W - 1 - i; break;
                case 2: x = d.W - 1 - i; z = d.D - 1 - j; break;
                default: x = d.D - 1 - j; z = i; break;
            }
            return new Vector2Int(origin.x + x, origin.y + z);
        }

        public static Vector3 Point(FixtureDef d, Vector2Int origin, int rot, float px, float pz)
        {
            float x, z;
            switch (rot & 3)
            {
                case 0: x = px; z = pz; break;
                case 1: x = pz; z = d.W - px; break;
                case 2: x = d.W - px; z = d.D - pz; break;
                default: x = d.D - pz; z = px; break;
            }
            return new Vector3(origin.x + x, 0f, origin.y + z);
        }

        public static Vector2Int Dir(Vector2Int local, int rot)
        {
            switch (rot & 3)
            {
                case 0: return local;
                case 1: return new Vector2Int(local.y, -local.x);
                case 2: return new Vector2Int(-local.x, -local.y);
                default: return new Vector2Int(-local.y, local.x);
            }
        }

        public static float Yaw(int rot) { return (rot & 3) * 90f; }

        /// <summary>Origin that centres the footprint on a hovered tile.</summary>
        public static Vector2Int OriginFor(FixtureDef d, int rot, Vector2Int hovered)
        {
            var s = Size(d, rot);
            return new Vector2Int(hovered.x - (s.x - 1) / 2, hovered.y - (s.y - 1) / 2);
        }

        public static bool Buildable(StoreGrid g, Vector2Int t)
        {
            if (!g.In(t.x, t.y)) return false;
            var z = g.ZoneAt(t);
            if (z != Zone.Front && z != Zone.Floor) return false;
            if (t.y == Layout.LobbyStart && t.x >= 2 && t.x <= 5) return false;   // inside the entrance
            if (t.y == Layout.FloorEnd && (t.x % Layout.SlotWidth) >= 10) return false;   // in front of staff doors
            return true;
        }

        static readonly Queue<Vector2Int> bfs = new Queue<Vector2Int>();
        static bool[] seen = new bool[0];

        /// <summary>
        /// Null if the fixture can go there, otherwise the reason it can't. `ignore` is a fixture being moved.
        /// Checks the footprint, the spots people stand on, and that every fixture stays reachable.
        /// </summary>
        public static string Validate(Store store, FixtureDef def, Vector2Int origin, int rot, Fixture ignore)
        {
            var g = store.Grid;
            var probe = new Fixture(def, -1);
            probe.SetPlacement(origin, rot);

            for (int i = 0; i < probe.Tiles.Count; i++)
            {
                var t = probe.Tiles[i];
                if (!Buildable(g, t)) return "Can't build there.";
                var other = store.FixtureAt(t);
                if (other != null && other != ignore) return "Something is already there.";
                if (other == null && g.IsBlocked(t.x, t.y)) return "Something is already there.";
                if (store.IsReserved(t, ignore)) return "That spot has to stay free for staff or customers.";
            }
            var spots = new List<Vector2Int>();
            if (def.HasWork) spots.Add(probe.Work);
            if (def.HasService) spots.Add(probe.Service);
            if (def.HasCashier) spots.Add(probe.Cashier);
            for (int i = 0; i < spots.Count; i++)
            {
                var t = spots[i];
                if (!Buildable(g, t)) return "Staff or customers would have nowhere to stand.";
                var other = store.FixtureAt(t);
                if ((other != null && other != ignore) || (other == null && g.IsBlocked(t.x, t.y)))
                    return "Staff or customers would have nowhere to stand.";
            }

            // Reachability with the new footprint in place.
            var blockedNow = new HashSet<Vector2Int>(probe.Tiles);
            var freed = new HashSet<Vector2Int>();
            if (ignore != null) for (int i = 0; i < ignore.Tiles.Count; i++) freed.Add(ignore.Tiles[i]);
            Flood(g, blockedNow, freed);

            if (!Reached(g, probe, spots)) return "People couldn't reach it from there.";
            var all = store.Fixtures;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f == ignore || f.Removed) continue;
                var fs = new List<Vector2Int>();
                if (f.Def.HasWork) fs.Add(f.Work);
                if (f.Def.HasService) fs.Add(f.Service);
                if (f.Def.HasCashier) fs.Add(f.Cashier);
                if (!Reached(g, f, fs)) return "That would block " + f.Label + ".";
            }
            for (int s = 0; s < store.SlotCount; s++)
            {
                var door = new Vector2Int(Layout.SlotX(s) + 10, Layout.FloorEnd);
                if (!Seen(g, door) && !Seen(g, door + Vector2Int.right)) return "That would block the way to the back room.";
            }
            return null;
        }

        static bool Reached(StoreGrid g, Fixture f, List<Vector2Int> spots)
        {
            for (int i = 0; i < spots.Count; i++)
                if (!Seen(g, spots[i])) return false;
            if (f.IsShelf)
            {
                for (int i = 0; i < f.Access.Count; i++)
                    if (Seen(g, f.Access[i])) return true;
                return false;
            }
            return true;
        }

        static bool Seen(StoreGrid g, Vector2Int t)
        {
            return g.In(t.x, t.y) && seen[t.y * g.W + t.x];
        }

        static void Flood(StoreGrid g, HashSet<Vector2Int> extraBlocked, HashSet<Vector2Int> freed)
        {
            if (seen.Length != g.W * g.D) seen = new bool[g.W * g.D];
            else System.Array.Clear(seen, 0, seen.Length);
            bfs.Clear();
            var start = new Vector2Int(3, Layout.LobbyStart);
            seen[start.y * g.W + start.x] = true;
            bfs.Enqueue(start);
            while (bfs.Count > 0)
            {
                var c = bfs.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    var n = c + (d == 0 ? Vector2Int.right : d == 1 ? Vector2Int.left : d == 2 ? Vector2Int.up : Vector2Int.down);
                    if (!g.In(n.x, n.y) || seen[n.y * g.W + n.x]) continue;
                    var z = g.ZoneAt(n);
                    if (z != Zone.Front && z != Zone.Floor) continue;
                    bool blocked = extraBlocked.Contains(n) || (g.IsBlocked(n.x, n.y) && !freed.Contains(n));
                    if (blocked) continue;
                    seen[n.y * g.W + n.x] = true;
                    bfs.Enqueue(n);
                }
            }
        }
    }
}
