using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class Batch
    {
        public int Qty;
        public int DaysLeft; // -1 = does not expire
    }

    /// <summary>A pile of one product, kept oldest-first so taking is FIFO.</summary>
    public class StockPile
    {
        public readonly List<Batch> Batches = new List<Batch>();
        public int Total { get; private set; }

        static int Order(int daysLeft) { return daysLeft < 0 ? int.MaxValue : daysLeft; }

        public void Add(int qty, int daysLeft)
        {
            if (qty <= 0) return;
            Total += qty;
            for (int i = 0; i < Batches.Count; i++)
            {
                if (Batches[i].DaysLeft == daysLeft) { Batches[i].Qty += qty; return; }
            }
            int at = Batches.Count;
            for (int i = 0; i < Batches.Count; i++)
            {
                if (Order(Batches[i].DaysLeft) > Order(daysLeft)) { at = i; break; }
            }
            Batches.Insert(at, new Batch { Qty = qty, DaysLeft = daysLeft });
        }

        /// <summary>Removes up to qty, oldest first. Returns how many were taken.</summary>
        public int Take(int qty)
        {
            return MoveTo(null, qty);
        }

        /// <summary>Moves up to qty (oldest first) into another pile, keeping their age.</summary>
        public int MoveTo(StockPile dest, int qty)
        {
            int taken = 0;
            while (qty > 0 && Batches.Count > 0)
            {
                Batch b = Batches[0];
                int n = Mathf.Min(qty, b.Qty);
                b.Qty -= n;
                qty -= n;
                taken += n;
                Total -= n;
                if (dest != null) dest.Add(n, b.DaysLeft);
                if (b.Qty <= 0) Batches.RemoveAt(0);
            }
            return taken;
        }

        /// <summary>One night passes. Returns the number of units that expired.</summary>
        public int Age()
        {
            int expired = 0;
            for (int i = Batches.Count - 1; i >= 0; i--)
            {
                Batch b = Batches[i];
                if (b.DaysLeft < 0) continue;
                b.DaysLeft--;
                if (b.DaysLeft <= 0)
                {
                    expired += b.Qty;
                    Batches.RemoveAt(i);
                }
            }
            Total -= expired;
            return expired;
        }

        public void Clear()
        {
            Batches.Clear();
            Total = 0;
        }
    }

    /// <summary>A category in this store: its price level and today's sales.</summary>
    public class Category
    {
        public readonly CategoryDef Def;
        public float PriceMult = 1f;
        public float SalesToday;
        public readonly List<Product> Products = new List<Product>();

        public Category(CategoryDef def) { Def = def; }
        public string Name { get { return Def.Name; } }
    }

    /// <summary>
    /// One product in this store: back-room stock, orders and today's numbers. Shelf stock lives on the
    /// fixtures the player assigned it to.
    /// </summary>
    public class Product
    {
        public readonly ProductDef Def;
        public readonly Category Category;
        public readonly StockPile Back = new StockPile();
        public readonly List<Fixture> Fixtures = new List<Fixture>();
        public int OnOrder;
        public int Par;

        public int SoldToday, MissedToday, PriceSkipsToday, WastedLastNight;

        public Product(ProductDef def, Category category)
        {
            Def = def;
            Category = category;
        }

        public string Name { get { return Def.Name; } }
        public bool Carried { get { return Fixtures.Count > 0; } }
        public bool Baked { get { return Category.Def.Baked; } }
        public float Price { get { return Def.BasePrice * Category.PriceMult; } }
        public int Life { get { return Def.ShelfLifeDays > 0 ? Def.ShelfLifeDays : -1; } }

        public int ShelfTotal
        {
            get { int t = 0; for (int i = 0; i < Fixtures.Count; i++) t += Fixtures[i].Stock.Total; return t; }
        }

        public int ShelfCap
        {
            get { int t = 0; for (int i = 0; i < Fixtures.Count; i++) t += Fixtures[i].Def.Capacity; return t; }
        }

        public float ShelfFill { get { int c = ShelfCap; return c > 0 ? Mathf.Clamp01(ShelfTotal / (float)c) : 0f; } }
        public int OnHand { get { return ShelfTotal + Back.Total; } }

        public void ResetDaily()
        {
            SoldToday = 0;
            MissedToday = 0;
            PriceSkipsToday = 0;
        }
    }
}
