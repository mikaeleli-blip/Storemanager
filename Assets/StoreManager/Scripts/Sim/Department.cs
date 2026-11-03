using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class ShelfUnit
    {
        public readonly List<Vector2Int> Access = new List<Vector2Int>();
        public readonly List<ProductStock> Products = new List<ProductStock>();
        public Transform FillBar;
        public Renderer FillRenderer;
        public float BarLength;
        public float BarStartZ;

        public float Fill
        {
            get
            {
                if (Products.Count == 0) return 0f;
                float f = 0f;
                for (int i = 0; i < Products.Count; i++) f += Products[i].ShelfFill;
                return f / Products.Count;
            }
        }
    }

    /// <summary>One serving position at a counter: a clerk behind it, a customer in front.</summary>
    public class CounterStation
    {
        public Vector2Int ServiceTile;   // customer side
        public Vector2Int WorkTile;      // clerk side
        public Employee Clerk;
        public Customer Customer;

        public bool Open { get { return Clerk != null && Clerk.AtPost; } }
    }

    /// <summary>A department open in one slot of the store.</summary>
    public class Department
    {
        public readonly DepartmentDef Def;
        public readonly int Slot;
        public float PriceMult = 1f;
        public bool UnderConstruction;
        public bool Removed;

        public readonly List<ProductStock> Products = new List<ProductStock>();
        public readonly List<ShelfUnit> Shelves = new List<ShelfUnit>();
        public int ShelfCapacity;

        // Filled in by StoreBuilder.
        public Vector2Int BackPick;
        public Vector2Int WorkTile;       // oven (bakery) or behind the counter (deli)
        public Vector2Int ServiceTile;    // customer side of the counter
        public readonly List<Vector2Int> QueueTiles = new List<Vector2Int>();
        public readonly List<Vector2Int> FloorTiles = new List<Vector2Int>();
        public Transform Root;

        // Counter departments: a waiting line and serving stations.
        public readonly List<Customer> CounterQueue = new List<Customer>();
        public readonly List<CounterStation> Stations = new List<CounterStation>();

        public CounterStation FreeStationForCustomer()
        {
            for (int i = 0; i < Stations.Count; i++)
                if (Stations[i].Open && Stations[i].Customer == null) return Stations[i];
            return null;
        }

        public CounterStation FreeStationForClerk()
        {
            for (int i = 0; i < Stations.Count; i++)
                if (Stations[i].Clerk == null) return Stations[i];
            return null;
        }

        public int WaitingAtCounter
        {
            get
            {
                int n = CounterQueue.Count;
                for (int i = 0; i < Stations.Count; i++) if (Stations[i].Customer != null) n++;
                return n;
            }
        }

        public float SalesToday;

        public Department(DepartmentDef def, int slot)
        {
            Def = def;
            Slot = slot;
            for (int i = 0; i < def.Products.Count; i++)
                Products.Add(new ProductStock(def.Products[i], this));
        }

        public string Name { get { return Def.Name; } }
        public bool IsCounter { get { return Def.Kind == DeptKind.Counter; } }
        public bool IsBakery { get { return Def.Kind == DeptKind.Bakery; } }
        public bool Active { get { return !UnderConstruction && !Removed; } }

        public ProductStock Find(ProductDef def)
        {
            for (int i = 0; i < Products.Count; i++)
                if (Products[i].Def == def) return Products[i];
            return null;
        }

        public float ShelfFill
        {
            get
            {
                if (Products.Count == 0) return 0f;
                float f = 0f;
                for (int i = 0; i < Products.Count; i++) f += Products[i].ShelfFill;
                return f / Products.Count;
            }
        }

        public int BackTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].Back.Total; return t; }
        }

        /// <summary>Where a customer stands to take this product.</summary>
        public Vector2Int PickTile(ProductStock p)
        {
            if (IsCounter || Shelves.Count == 0) return Stations.Count > 0 ? Stations[0].ServiceTile : ServiceTile;
            var shelf = Shelves[p.ShelfIndex % Shelves.Count];
            return shelf.Access[Random.Range(0, shelf.Access.Count)];
        }

        /// <summary>Where staff stand to put this product on display.</summary>
        public Vector2Int RestockTile(ProductStock p)
        {
            if (IsCounter || Shelves.Count == 0) return WorkTile;
            return PickTile(p);
        }

        /// <summary>Unclaimed product with the emptiest shelf below the threshold that has back stock.</summary>
        public ProductStock NeedsRestock(float threshold)
        {
            ProductStock best = null;
            float bestFill = threshold;
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                if (p.ClaimedBy != null || p.Back.Total <= 0) continue;
                if (p.ShelfFill < bestFill) { bestFill = p.ShelfFill; best = p; }
            }
            return best;
        }

        public ProductStock NeedsBaking(float threshold)
        {
            ProductStock best = null;
            float bestFill = threshold;
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                if (p.ClaimedBy != null) continue;
                if (p.ShelfFill < bestFill) { bestFill = p.ShelfFill; best = p; }
            }
            return best;
        }

        public void ResetDaily()
        {
            SalesToday = 0f;
            for (int i = 0; i < Products.Count; i++) Products[i].ResetDaily();
        }

        static readonly Color Full = new Color(0.3f, 0.8f, 0.35f);
        static readonly Color Low = new Color(0.95f, 0.8f, 0.2f);
        static readonly Color Empty = new Color(0.9f, 0.2f, 0.15f);

        public void UpdateVisuals()
        {
            for (int i = 0; i < Shelves.Count; i++)
            {
                var s = Shelves[i];
                if (s.FillBar == null) continue;
                float fill = s.Fill;
                if (s.FillRenderer != null)
                    s.FillRenderer.sharedMaterial = Mats.Get(fill > 0.5f ? Full : fill > 0.2f ? Low : Empty);
                float f = Mathf.Max(0.02f, fill);
                var sc = s.FillBar.localScale;
                sc.z = s.BarLength * f;
                s.FillBar.localScale = sc;
                var pos = s.FillBar.position;
                pos.z = s.BarStartZ + sc.z * 0.5f;
                s.FillBar.position = pos;
            }
        }
    }

    public class CheckoutLane
    {
        public readonly int Index;
        public Employee Cashier;
        public bool Closing;
        public readonly List<Customer> Queue = new List<Customer>();
        public Renderer Light;

        public CheckoutLane(int index) { Index = index; }

        public int CounterX { get { return Layout.LaneCounterX(Index); } }
        public Vector2Int CashierTile { get { return new Vector2Int(CounterX - 1, 6); } }
        public Vector2Int ServiceTile { get { return new Vector2Int(CounterX + 1, 6); } }

        public Vector2Int QueueSpot(int index)
        {
            if (index <= 0) return ServiceTile;
            return new Vector2Int(CounterX + 1, 6 + Mathf.Min(index, 5));
        }

        public bool Staffed { get { return Cashier != null && Cashier.AtPost; } }

        /// <summary>Takes new customers: staffed and not closing, or closing at end of day.</summary>
        public bool Accepting
        {
            get
            {
                if (!Staffed) return false;
                if (!Closing) return true;
                return !Store.I.Clock.IsOpen;
            }
        }

        public Customer Front { get { return Queue.Count > 0 ? Queue[0] : null; } }
    }
}
