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

    /// <summary>One product in one department: shelf stock, back-room stock and today's numbers.</summary>
    public class ProductStock
    {
        public readonly ProductDef Def;
        public readonly Department Dept;
        public readonly StockPile Shelf = new StockPile();
        public readonly StockPile Back = new StockPile();
        public int OnOrder;
        public int Par;
        public int ShelfCap;
        public int ShelfIndex;
        public Employee ClaimedBy;

        public int SoldToday, MissedToday, PriceSkipsToday, WastedLastNight;

        public ProductStock(ProductDef def, Department dept)
        {
            Def = def;
            Dept = dept;
        }

        public float Price { get { return Def.BasePrice * Dept.PriceMult; } }
        public int Life { get { return Def.ShelfLifeDays > 0 ? Def.ShelfLifeDays : -1; } }
        public float ShelfFill { get { return ShelfCap > 0 ? Mathf.Clamp01(Shelf.Total / (float)ShelfCap) : 0f; } }
        public int OnHand { get { return Shelf.Total + Back.Total; } }

        public void ResetDaily()
        {
            SoldToday = 0;
            MissedToday = 0;
            PriceSkipsToday = 0;
        }
    }
}
