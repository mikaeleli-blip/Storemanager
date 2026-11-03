using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class Spill
    {
        public Vector2Int Tile;
        public GameObject Visual;
        public Employee ClaimedBy;
    }

    /// <summary>Spills customers leave behind. Customers near one get unhappy; cleaners remove them.</summary>
    public class DirtManager
    {
        public readonly List<Spill> Spills = new List<Spill>();
        public const int Max = 40;
        readonly Transform root;

        public DirtManager(Transform parent)
        {
            root = new GameObject("Spills").transform;
            root.SetParent(parent, false);
        }

        public void AddSpill(Vector3 pos)
        {
            if (Spills.Count >= Max) return;
            var tile = StoreGrid.TileAt(pos);
            for (int i = 0; i < Spills.Count; i++) if (Spills[i].Tile == tile) return;
            var s = new Spill { Tile = tile };
            s.Visual = Mats.Prim(PrimitiveType.Cylinder, "Spill", root, StoreGrid.World(tile) + new Vector3(0f, 0.02f, 0f),
                new Vector3(0.9f, 0.01f, 0.7f), new Color(0.45f, 0.3f, 0.12f), false);
            Spills.Add(s);
        }

        public bool AnyWithin(Vector3 pos, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < Spills.Count; i++)
            {
                Vector3 d = StoreGrid.World(Spills[i].Tile) - pos;
                d.y = 0f;
                if (d.sqrMagnitude < r2) return true;
            }
            return false;
        }

        public Spill NearestWithin(Vector3 pos, float radius)
        {
            Spill best = null;
            float bestD = radius * radius;
            for (int i = 0; i < Spills.Count; i++)
            {
                Vector3 d = StoreGrid.World(Spills[i].Tile) - pos;
                d.y = 0f;
                if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = Spills[i]; }
            }
            return best;
        }

        public Spill ClaimNearest(Vector3 pos, Employee who)
        {
            Spill best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Spills.Count; i++)
            {
                var s = Spills[i];
                if (s.ClaimedBy != null && s.ClaimedBy != who) continue;
                float d = (StoreGrid.World(s.Tile) - pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best != null) best.ClaimedBy = who;
            return best;
        }

        public void Remove(Spill s)
        {
            if (s.Visual != null) Object.Destroy(s.Visual);
            Spills.Remove(s);
        }
    }
}
