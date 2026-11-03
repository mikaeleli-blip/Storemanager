using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>A placed shelf, fridge, counter, oven or checkout.</summary>
    public class Fixture
    {
        public readonly FixtureDef Def;
        public readonly int Id;
        public Vector2Int Origin;
        public int Rot;

        public readonly List<Vector2Int> Tiles = new List<Vector2Int>();
        public readonly List<Vector2Int> Access = new List<Vector2Int>();
        public Vector2Int Work, Service, Cashier, QueueDir;

        // Shelves and counters: the one product on it and its stock.
        public Product Product;
        public readonly StockPile Stock = new StockPile();
        public Employee ClaimedBy;       // stocker/baker filling it
        public bool Removed;

        // Checkouts
        public CheckoutLane Lane;

        // Counters
        public Employee Clerk;
        public readonly List<Customer> Waiting = new List<Customer>();

        // Visuals
        public Transform Root;
        public readonly List<GameObject> Rows = new List<GameObject>();
        int rowsShown = -1;

        public Fixture(FixtureDef def, int id)
        {
            Def = def;
            Id = id;
        }

        public bool IsShelf { get { return Def.Kind == FixtureKind.Shelf; } }
        public bool IsCounter { get { return Def.Kind == FixtureKind.Counter; } }
        public bool IsOven { get { return Def.Kind == FixtureKind.Oven; } }
        public bool IsCheckout { get { return Def.Kind == FixtureKind.Checkout; } }
        public bool HoldsProducts { get { return IsShelf || IsCounter; } }
        public bool IsBakeryShelf { get { return Def.Id == "bakery_shelf"; } }
        public float Fill { get { return Def.Capacity > 0 ? Mathf.Clamp01(Stock.Total / (float)Def.Capacity) : 0f; } }
        public Vector3 Center { get { return Placement.Point(Def, Origin, Rot, Def.W * 0.5f, Def.D * 0.5f); } }

        public string Label
        {
            get
            {
                if (IsCheckout) return Lane != null ? "Checkout " + (Lane.Index + 1) : "Checkout";
                if (IsOven) return "Oven";
                return Product != null ? Product.Name : "Empty " + Def.Name.ToLower();
            }
        }

        public void SetPlacement(Vector2Int origin, int rot)
        {
            Origin = origin;
            Rot = ((rot % 4) + 4) % 4;
            Tiles.Clear();
            Access.Clear();
            for (int i = 0; i < Def.W; i++)
                for (int j = 0; j < Def.D; j++)
                    Tiles.Add(Placement.Tile(Def, Origin, Rot, i, j));
            for (int i = 0; i < Def.Access.Length; i++)
                Access.Add(Placement.Tile(Def, Origin, Rot, Def.Access[i].x, Def.Access[i].y));
            if (Def.HasWork) Work = Placement.Tile(Def, Origin, Rot, Def.Work.x, Def.Work.y);
            if (Def.HasService) Service = Placement.Tile(Def, Origin, Rot, Def.Service.x, Def.Service.y);
            if (Def.HasCashier) Cashier = Placement.Tile(Def, Origin, Rot, Def.Cashier.x, Def.Cashier.y);
            QueueDir = Placement.Dir(Def.QueueDir, Rot);
        }

        /// <summary>A free spot to stand at to take from (or fill) this shelf.</summary>
        public Vector2Int AccessTile(StoreGrid g)
        {
            if (!IsShelf) return Def.HasService ? Service : Origin;
            int n = Access.Count, start = Random.Range(0, Mathf.Max(1, n));
            for (int k = 0; k < n; k++)
            {
                var t = Access[(start + k) % n];
                if (g.Walkable(t, false)) return t;
            }
            return n > 0 ? g.NearestWalkable(Access[0], false) : Origin;
        }

        /// <summary>Queue position: 0 is the service tile, then straight out while the floor is free.</summary>
        public Vector2Int QueueSpot(int index, StoreGrid g)
        {
            var t = Service;
            for (int k = 1; k <= index && k <= 6; k++)
            {
                var next = t + QueueDir;
                if (!g.Walkable(next, false)) break;
                t = next;
            }
            return t;
        }

        public void UpdateVisuals()
        {
            int n = Rows.Count;
            if (n == 0) return;
            int want = Stock.Total <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(Fill * n), 1, n);
            if (want == rowsShown) return;
            for (int i = 0; i < n; i++)
                if (Rows[i] != null) Rows[i].SetActive(i < want);
            rowsShown = want;
        }

        public void ResetVisuals() { rowsShown = -1; }
    }

    public class CheckoutLane
    {
        public readonly Fixture Fixture;
        public int Index;
        public Employee Cashier;
        public bool Closing;
        public readonly List<Customer> Queue = new List<Customer>();
        public Renderer Light;

        public CheckoutLane(Fixture f) { Fixture = f; }

        public bool Removed { get { return Fixture.Removed; } }
        public Vector2Int CashierTile { get { return Fixture.Cashier; } }
        public Vector2Int ServiceTile { get { return Fixture.Service; } }
        public Vector2Int QueueSpot(int index) { return Fixture.QueueSpot(index, Store.I.Grid); }

        public bool Staffed { get { return !Removed && Cashier != null && Cashier.AtPost; } }

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
