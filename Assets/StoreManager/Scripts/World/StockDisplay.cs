using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace StoreManager
{
    /// <summary>
    /// Where each unit of a product sits on one shelf row (Resources/Models/StockLayout.txt, written by the
    /// Blender build). Slots are grouped in columns, front to back and bottom to top.
    /// </summary>
    public class StockLayout
    {
        public struct Slot
        {
            public int Unit;
            public Vector3 Pos;
            public float Yaw;
        }

        public string Product;
        public string[] Units;
        public readonly List<Slot[]> Columns = new List<Slot[]>();
        public int Count;

        static Dictionary<string, StockLayout> all;

        public static StockLayout For(string product)
        {
            if (all == null) Load();
            StockLayout l;
            return all.TryGetValue(product, out l) ? l : null;
        }

        static void Load()
        {
            all = new Dictionary<string, StockLayout>();
            var text = Resources.Load<TextAsset>("Models/StockLayout");
            if (text == null) return;
            var inv = CultureInfo.InvariantCulture;
            StockLayout cur = null;
            foreach (var raw in text.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("P "))
                {
                    var parts = line.Substring(2).Split('|');
                    cur = new StockLayout { Product = parts[0], Units = parts[1].Split(',') };
                    all[cur.Product] = cur;
                }
                else if (line.StartsWith("C ") && cur != null)
                {
                    var entries = line.Substring(2).Split(';');
                    var col = new Slot[entries.Length];
                    for (int i = 0; i < entries.Length; i++)
                    {
                        var v = entries[i].Split(' ');
                        col[i] = new Slot
                        {
                            Unit = int.Parse(v[0], inv),
                            Pos = new Vector3(float.Parse(v[1], inv), float.Parse(v[2], inv), float.Parse(v[3], inv)),
                            Yaw = float.Parse(v[4], inv)
                        };
                    }
                    cur.Columns.Add(col);
                    cur.Count += col.Length;
                }
            }
        }
    }

    /// <summary>
    /// One shelf row (one level of one side of one segment) that shows exactly n units: the visible units are
    /// merged into one mesh, rebuilt only when the count changes. Columns fill in a shuffled order so a
    /// half-empty shelf looks picked over rather than neatly cut off.
    /// </summary>
    public class StockRow
    {
        readonly GameObject go;
        readonly Mesh mesh;
        readonly Mesh[] unitMeshes;
        readonly StockLayout.Slot[] slots;
        int shown = -1;

        public int Max { get { return slots.Length; } }

        public StockRow(Transform parent, Vector3 pos, Quaternion rot, StockLayout layout, int seed)
        {
            go = new GameObject("Row " + layout.Product);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            mesh = new Mesh { name = "Stock " + layout.Product, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = ModelLibrary.Palette;

            unitMeshes = new Mesh[layout.Units.Length];
            for (int i = 0; i < unitMeshes.Length; i++) unitMeshes[i] = ModelLibrary.GetMesh(layout.Units[i]);

            var order = new int[layout.Columns.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var rng = new System.Random(seed);
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            var list = new List<StockLayout.Slot>(layout.Count);
            for (int i = 0; i < order.Length; i++) list.AddRange(layout.Columns[order[i]]);
            slots = list.ToArray();
            go.SetActive(false);
        }

        public void Show(int n)
        {
            n = Mathf.Clamp(n, 0, slots.Length);
            if (n == shown) return;
            shown = n;
            mesh.Clear();
            if (n == 0) { go.SetActive(false); return; }
            var parts = new List<CombineInstance>(n);
            for (int i = 0; i < n; i++)
            {
                var s = slots[i];
                var m = unitMeshes[s.Unit];
                if (m == null) continue;
                parts.Add(new CombineInstance { mesh = m, transform = ModelLibrary.UnitMatrix(s.Pos, s.Yaw) });
            }
            mesh.CombineMeshes(parts.ToArray(), true, true);
            go.SetActive(true);
        }

        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
        }
    }
}
