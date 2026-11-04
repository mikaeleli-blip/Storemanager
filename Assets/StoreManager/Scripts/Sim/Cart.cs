using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class CartLine
    {
        public Product Product;
        public int Cases;
        public int Units { get { return Cases * Product.Def.CaseSize; } }
    }

    /// <summary>A wholesale order being put together on the office computer. Paid for at checkout.</summary>
    public class Cart
    {
        public readonly List<CartLine> Lines = new List<CartLine>();

        public CartLine LineOf(Product p)
        {
            for (int i = 0; i < Lines.Count; i++) if (Lines[i].Product == p) return Lines[i];
            return null;
        }

        public int CasesOf(Product p)
        {
            var l = LineOf(p);
            return l != null ? l.Cases : 0;
        }

        /// <summary>Adds (or with a negative number removes) whole cases.</summary>
        public void Add(Product p, int cases)
        {
            if (p == null || p.Baked || cases == 0) return;
            var l = LineOf(p);
            if (l == null)
            {
                if (cases < 0) return;
                l = new CartLine { Product = p };
                Lines.Add(l);
            }
            l.Cases = Mathf.Max(0, l.Cases + cases);
            if (l.Cases == 0) Lines.Remove(l);
        }

        public void Clear() { Lines.Clear(); }

        public int Units
        {
            get { int t = 0; for (int i = 0; i < Lines.Count; i++) t += Lines[i].Units; return t; }
        }

        public int Cases
        {
            get { int t = 0; for (int i = 0; i < Lines.Count; i++) t += Lines[i].Cases; return t; }
        }

        public float Cost(bool express)
        {
            float t = 0f;
            for (int i = 0; i < Lines.Count; i++) t += Lines[i].Cases * Store.CaseCost(Lines[i].Product, express);
            return t;
        }

        public float SupplierCost(SupplierDef s, bool express)
        {
            float t = 0f;
            for (int i = 0; i < Lines.Count; i++)
                if (Lines[i].Product.Def.Supplier == s) t += Lines[i].Cases * Store.CaseCost(Lines[i].Product, express);
            return t;
        }

        /// <summary>Scales one supplier's lines down (whole cases) to fit a budget.</summary>
        public void TrimSupplier(SupplierDef s, float maxCost, bool express)
        {
            float cost = SupplierCost(s, express);
            if (cost <= maxCost || cost <= 0f) return;
            float ratio = Mathf.Clamp01(maxCost / cost) * 0.98f;
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].Product.Def.Supplier != s) continue;
                Lines[i].Cases = Mathf.FloorToInt(Lines[i].Cases * ratio);
                if (Lines[i].Cases <= 0) Lines.RemoveAt(i);
            }
        }

        public void RemoveSupplier(SupplierDef s)
        {
            for (int i = Lines.Count - 1; i >= 0; i--) if (Lines[i].Product.Def.Supplier == s) Lines.RemoveAt(i);
        }

        /// <summary>Scales every line down (whole cases) until the order fits the space and the money.</summary>
        public void Trim(int maxUnits, float maxCost, bool express)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                int units = Units;
                float cost = Cost(express);
                if (units <= maxUnits && cost <= maxCost) return;
                float ratio = Mathf.Min(units > 0 ? (float)maxUnits / units : 1f, cost > 0f ? maxCost / cost : 1f);
                ratio = Mathf.Clamp01(ratio) * 0.98f;
                for (int i = Lines.Count - 1; i >= 0; i--)
                {
                    Lines[i].Cases = Mathf.FloorToInt(Lines[i].Cases * ratio);
                    if (Lines[i].Cases <= 0) Lines.RemoveAt(i);
                }
            }
        }
    }
}
