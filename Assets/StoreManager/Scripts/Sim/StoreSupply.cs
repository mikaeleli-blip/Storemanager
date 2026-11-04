using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>What the store owes and is owed by one supplier.</summary>
    public class SupplierState
    {
        public SupplierDef Def;
        public int PendingDay = -1;     // day the next truck order arrives (-1 = nothing ordered)
        public float Credit;            // credit notes not yet used against an invoice
        public float ReturnsValue;      // expired stock waiting to go back (credit value)
        public int ReturnsUnits;
        public bool OnHold;             // stopped taking orders over unpaid bills
        public string Name { get { return Def.Name; } }
    }

    /// <summary>A bill (reikningur) that comes with each delivery.</summary>
    public class Invoice
    {
        public int Number;
        public SupplierState Supplier;
        public int IssuedDay, DueDay, PaidDay;
        public int Units;
        public bool Express, Paid;
        public float Goods, CreditUsed, LateFee;
        public float Amount { get { return Mathf.Max(0f, Goods - CreditUsed) + LateFee; } }
    }

    public enum DateCheckPolicy { WhenRestocking, OnceADay, EveryShift }

    public partial class Store
    {
        public readonly List<SupplierState> Suppliers = new List<SupplierState>();
        public readonly List<Invoice> Invoices = new List<Invoice>();
        public DateCheckPolicy DateChecks = DateCheckPolicy.OnceADay;
        public const float LateFeeShare = 0.05f;
        public const int HoldAfterDays = 7;

        int nextInvoice = 1001;
        float lastDailyCheck = -99999f;
        readonly float[] lastShiftCheck = { -99999f, -99999f, -99999f };

        void InitSuppliers()
        {
            for (int i = 0; i < Catalog.Suppliers.Count; i++) Suppliers.Add(new SupplierState { Def = Catalog.Suppliers[i] });
        }

        public SupplierState SupplierState(SupplierDef d)
        {
            if (Suppliers.Count == 0) InitSuppliers();
            for (int i = 0; i < Suppliers.Count; i++) if (Suppliers[i].Def == d) return Suppliers[i];
            return null;
        }

        public SupplierState SupplierOf(Product p) { return p.Def.Supplier != null ? SupplierState(p.Def.Supplier) : null; }

        // ------------------------------------------------------------------ stock totals

        public int BackStockTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].Back.Total; return t; }
        }

        /// <summary>Ordered and on its way (truck or express).</summary>
        public int OnOrderTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].OnOrder + Products[i].OnExpress; return t; }
        }

        /// <summary>Back-room space not yet spoken for by stock or deliveries on the way.</summary>
        public int BackFreeForOrders { get { return Mathf.Max(0, BackCapacity - BackStockTotal - OnOrderTotal); } }

        /// <summary>The cart on the office computer.</summary>
        public readonly Cart Cart = new Cart();

        public static float CaseCost(Product p, bool express)
        {
            return p.Def.Cost * p.Def.CaseSize * (express ? 1f + ExpressSurcharge : 1f);
        }

        // ------------------------------------------------------------------ delivery days

        static int DayOfWeek(int day) { return (day - 1) % 7; }

        /// <summary>The day a truck order to this supplier placed now would arrive.</summary>
        public int NextDelivery(SupplierDef s)
        {
            int start = Clock.Hour < 6 ? Clock.Day : Clock.Day + 1;
            for (int d = start; d < start + 8; d++) if (s.Days[DayOfWeek(d)]) return d;
            return start;
        }

        public string DayText(int day)
        {
            if (day == Clock.Day) return "today";
            if (day == Clock.Day + 1) return "tomorrow";
            string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            return names[DayOfWeek(day)] + " (day " + day + ")";
        }

        // ------------------------------------------------------------------ credit

        public float CreditLimit(SupplierState s) { return s.Def.CreditBase + s.Def.CreditPerLevel * Level; }

        /// <summary>Unpaid bills plus goods ordered but not delivered yet.</summary>
        public float Owed(SupplierState s)
        {
            float t = 0f;
            for (int i = 0; i < Invoices.Count; i++) if (!Invoices[i].Paid && Invoices[i].Supplier == s) t += Invoices[i].Amount;
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                if (p.Def.Supplier != s.Def) continue;
                t += p.OnOrder * p.Def.Cost + p.OnExpress * p.Def.Cost * (1f + ExpressSurcharge);
            }
            return t;
        }

        public float CreditLeft(SupplierState s) { return Mathf.Max(0f, CreditLimit(s) - Owed(s)); }

        // ------------------------------------------------------------------ ordering

        /// <summary>Why the cart can't be ordered like this, or null if it can.</summary>
        public string OrderProblem(Cart c, bool express)
        {
            if (c.Lines.Count == 0) return "The cart is empty.";
            if (c.Units > BackFreeForOrders)
                return "Not enough room in the back room: " + BackFreeForOrders + " units free (stock on the way counts), the cart has " + c.Units + ".";
            for (int i = 0; i < Suppliers.Count; i++)
            {
                var s = Suppliers[i];
                float value = c.SupplierCost(s.Def, express);
                if (value <= 0f) continue;
                if (s.OnHold) return s.Name + " won't take orders until their overdue bills are paid.";
                if (value > CreditLeft(s))
                    return s.Name + ": over your credit limit (" + Economy.Money(CreditLimit(s)) + "). You owe " + Economy.Money(Owed(s))
                        + ", so " + Economy.Money(CreditLeft(s)) + " left. Pay some bills first.";
                if (!express && value < s.Def.MinOrder)
                    return s.Name + " has a minimum order of " + Economy.Money(s.Def.MinOrder) + " (cart has " + Economy.Money(value) + ").";
            }
            return null;
        }

        /// <summary>Sends the cart to the suppliers. Nothing is paid now: each delivery comes with an invoice.</summary>
        public bool PlaceOrder(Cart c, bool express)
        {
            if (OrderProblem(c, express) != null) return false;
            var parts = new List<string>();
            for (int i = 0; i < Suppliers.Count; i++)
            {
                var s = Suppliers[i];
                int units = 0;
                for (int k = 0; k < c.Lines.Count; k++)
                {
                    var l = c.Lines[k];
                    if (l.Product.Def.Supplier != s.Def) continue;
                    if (express) l.Product.OnExpress += l.Units;
                    else l.Product.OnOrder += l.Units;
                    units += l.Units;
                }
                if (units == 0) continue;
                if (!express)
                {
                    int day = NextDelivery(s.Def);
                    if (s.PendingDay < 0 || day < s.PendingDay) s.PendingDay = day;
                    parts.Add(s.Name + " " + DayText(s.PendingDay));
                }
                else parts.Add(s.Name);
            }
            c.Clear();
            if (express)
            {
                if (expressTimer < 0f) expressTimer = 60f;
                Message("Express order sent (" + string.Join(", ", parts.ToArray()) + "). Arrives in about an hour, +25%.");
            }
            else Message("Order sent. Deliveries: " + string.Join(", ", parts.ToArray()) + " at 06:00.");
            return true;
        }

        /// <summary>
        /// The assistant manager's order: everything on sale up to the next-but-one delivery, within the back-room
        /// space, skipping suppliers on hold and orders under a supplier's minimum. Returns products ordered.
        /// </summary>
        public int AutoOrder(float error) { return AutoOrder(error, null, true); }

        /// <summary>Department managers who order for their own department.</summary>
        public bool DepartmentOrdered(Category c)
        {
            for (int i = 0; i < Staff.All.Count; i++)
            {
                var e = Staff.All[i];
                if (e != null && !e.Leaving && e.Rank == Rank.DeptManager && e.Department == c && e.OrdersForDepartment) return true;
            }
            return false;
        }

        /// <summary>Orders products (only one department if given), skipping departments whose manager orders them.</summary>
        public int AutoOrder(float error, Category only, bool skipManaged)
        {
            var c = new Cart();
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                if (!p.Carried || p.Baked) continue;
                if (only != null && p.Category != only) continue;
                if (only == null && skipManaged && DepartmentOrdered(p.Category)) continue;
                var s = SupplierOf(p);
                if (s == null || s.OnHold) continue;
                int need = SuggestedPar(p) - (p.FreshOnHand + p.OnOrder + p.OnExpress);
                if (need <= 0) continue;
                float q = need;
                if (error > 0f) q *= Random.Range(1f - error, 1f + error);
                c.Add(p, Mathf.Max(1, Mathf.CeilToInt(q / p.Def.CaseSize)));
            }
            FitToSuppliers(c, false);
            int lines = c.Lines.Count;
            return lines > 0 && PlaceOrder(c, false) ? lines : 0;
        }

        /// <summary>Trims a cart to the back-room space and each supplier's credit, and drops parts under a minimum order.</summary>
        public void FitToSuppliers(Cart c, bool express)
        {
            c.Trim(BackFreeForOrders, float.MaxValue, express);
            for (int i = 0; i < Suppliers.Count; i++)
            {
                var s = Suppliers[i];
                c.TrimSupplier(s.Def, CreditLeft(s), express);
                float v = c.SupplierCost(s.Def, express);
                if (v > 0f && (s.OnHold || (!express && v < s.Def.MinOrder))) c.RemoveSupplier(s.Def);
            }
        }

        // ------------------------------------------------------------------ deliveries

        void Deliver(bool express)
        {
            if (express) expressTimer = -1f;
            int space = Mathf.Max(0, BackCapacity - BackStockTotal);
            int ordered = 0;
            var due = new int[Products.Count];
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                var s = SupplierOf(p);
                if (s == null) continue;
                due[i] = express ? p.OnExpress : (s.PendingDay >= 0 && s.PendingDay <= Clock.Day ? p.OnOrder : 0);
                ordered += due[i];
            }
            if (ordered == 0) { if (!express) ReleaseDueSuppliers(); return; }

            // Space was checked when ordering, but stock cleared off shelves since then can fill the back room.
            // Then everything gets a fair share (whole cases), spare room goes to what's lowest, the rest goes back.
            var take = new int[Products.Count];
            float ratio = ordered > space ? (float)space / ordered : 1f;
            int left = space;
            for (int i = 0; i < Products.Count; i++)
            {
                if (due[i] <= 0) continue;
                int cs = Products[i].Def.CaseSize;
                take[i] = ratio >= 1f ? due[i] : Mathf.FloorToInt(due[i] * ratio / cs) * cs;
                left -= take[i];
            }
            if (ratio < 1f)
            {
                var order = new List<int>();
                for (int i = 0; i < Products.Count; i++) if (due[i] > take[i]) order.Add(i);
                order.Sort((a, b) => ((float)Products[a].OnHand / Mathf.Max(1, Products[a].Par)).CompareTo((float)Products[b].OnHand / Mathf.Max(1, Products[b].Par)));
                bool gave = true;
                while (gave && left > 0)
                {
                    gave = false;
                    for (int k = 0; k < order.Count; k++)
                    {
                        int i = order[k], cs = Products[i].Def.CaseSize;
                        if (take[i] + cs > due[i] || cs > left) continue;
                        take[i] += cs;
                        left -= cs;
                        gave = true;
                    }
                }
            }

            int refused = 0;
            var goods = new Dictionary<SupplierState, float>();
            var units = new Dictionary<SupplierState, int>();
            for (int i = 0; i < Products.Count; i++)
            {
                if (due[i] <= 0) continue;
                var p = Products[i];
                var s = SupplierOf(p);
                p.Back.Add(take[i], ArrivalLife(p));
                refused += due[i] - take[i];
                if (express) p.OnExpress = 0; else p.OnOrder = 0;
                float g;
                goods.TryGetValue(s, out g);
                goods[s] = g + take[i] * p.Def.Cost * (express ? 1f + ExpressSurcharge : 1f);
                int u;
                units.TryGetValue(s, out u);
                units[s] = u + take[i];
            }
            if (!express) ReleaseDueSuppliers();

            var notes = new List<string>();
            foreach (var kv in goods)
            {
                var s = kv.Key;
                if (s.ReturnsValue > 0f)
                {
                    s.Credit += s.ReturnsValue;
                    notes.Add(s.Name + " took back " + s.ReturnsUnits + " expired items (credit " + Economy.Money(s.ReturnsValue) + ")");
                    s.ReturnsValue = 0f;
                    s.ReturnsUnits = 0;
                }
                if (units[s] > 0) IssueInvoice(s, kv.Value, units[s], express);
            }

            int total = 0;
            foreach (var kv in units) total += kv.Value;
            if (total > 0)
            {
                Message((express ? "Express delivery" : "Deliveries") + " arrived: " + total + " units, " + goods.Count + " invoice"
                    + (goods.Count == 1 ? "" : "s") + " in the Bills app."
                    + (refused > 0 ? " " + refused + " units didn't fit in the back room and went back on the truck." : ""));
                for (int i = 0; i < notes.Count; i++) Message(notes[i] + ".");
                if (Builder.Truck != null) Builder.Truck.SetActive(true);
                truckTimer = 60f;
            }
        }

        /// <summary>Stock rarely arrives fresh off the line: up to a third of its shelf life may be used already.</summary>
        static int ArrivalLife(Product p)
        {
            int life = p.Life;
            if (life <= 0) return -1;
            return Mathf.Max(1, life - Random.Range(0, life / 3 + 1));
        }

        void ReleaseDueSuppliers()
        {
            for (int i = 0; i < Suppliers.Count; i++)
                if (Suppliers[i].PendingDay >= 0 && Suppliers[i].PendingDay <= Clock.Day) Suppliers[i].PendingDay = -1;
        }

        void IssueInvoice(SupplierState s, float goods, int units, bool express)
        {
            var inv = new Invoice
            {
                Number = nextInvoice++, Supplier = s, IssuedDay = Clock.Day, DueDay = Clock.Day + s.Def.TermsDays,
                Units = units, Express = express, Goods = goods,
            };
            inv.CreditUsed = Mathf.Min(s.Credit, goods);
            s.Credit -= inv.CreditUsed;
            Invoices.Add(inv);
        }

        // ------------------------------------------------------------------ bills

        public float UnpaidTotal
        {
            get { float t = 0f; for (int i = 0; i < Invoices.Count; i++) if (!Invoices[i].Paid) t += Invoices[i].Amount; return t; }
        }

        /// <summary>Unpaid and due within this many days (overdue included).</summary>
        public float DueWithin(int days)
        {
            float t = 0f;
            for (int i = 0; i < Invoices.Count; i++)
                if (!Invoices[i].Paid && Invoices[i].DueDay <= Clock.Day + days) t += Invoices[i].Amount;
            return t;
        }

        public int OverdueCount
        {
            get { int n = 0; for (int i = 0; i < Invoices.Count; i++) if (!Invoices[i].Paid && Clock.Day > Invoices[i].DueDay) n++; return n; }
        }

        public bool PayInvoice(Invoice inv)
        {
            if (inv.Paid || Economy.Cash < inv.Amount) return false;
            Economy.PayWholesale(inv.Amount);
            Economy.Today.LateFees += inv.LateFee;
            inv.Paid = true;
            inv.PaidDay = Clock.Day;
            var s = inv.Supplier;
            if (s.OnHold)
            {
                bool still = false;
                for (int i = 0; i < Invoices.Count; i++)
                    if (!Invoices[i].Paid && Invoices[i].Supplier == s && Clock.Day > Invoices[i].DueDay) still = true;
                if (!still) { s.OnHold = false; Message(s.Name + " is taking orders again."); }
            }
            return true;
        }

        /// <summary>Pays unpaid bills due within the given days, oldest due first, while cash allows. Returns how many.</summary>
        public int PayBillsDueWithin(int days, float keepCash)
        {
            var list = new List<Invoice>();
            for (int i = 0; i < Invoices.Count; i++)
                if (!Invoices[i].Paid && Invoices[i].DueDay <= Clock.Day + days) list.Add(Invoices[i]);
            list.Sort((a, b) => a.DueDay.CompareTo(b.DueDay));
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (Economy.Cash - list[i].Amount < keepCash) continue;
                if (PayInvoice(list[i])) n++;
            }
            return n;
        }

        void AssistantPaysBills(Employee am)
        {
            float before = Economy.Cash;
            int n = PayBillsDueWithin(1, 300f);
            if (n > 0) Message(am.DisplayName + " paid " + n + " bill" + (n == 1 ? "" : "s") + " (" + Economy.Money(before - Economy.Cash) + ").");
        }

        /// <summary>Midnight: late fees on overdue bills; a week overdue and the supplier stops taking orders.</summary>
        void BillsNightly()
        {
            for (int i = 0; i < Invoices.Count; i++)
            {
                var inv = Invoices[i];
                if (inv.Paid) continue;
                if (Clock.Day > inv.DueDay && inv.LateFee <= 0f)
                {
                    inv.LateFee = Mathf.Max(10f, Mathf.Round(inv.Amount * LateFeeShare));
                    Message("Bill #" + inv.Number + " from " + inv.Supplier.Name + " is overdue: late fee " + Economy.Money(inv.LateFee) + ".");
                }
                if (Clock.Day > inv.DueDay + HoldAfterDays && !inv.Supplier.OnHold)
                {
                    inv.Supplier.OnHold = true;
                    Message(inv.Supplier.Name + " has stopped taking orders until their overdue bills are paid.");
                }
            }
            // keep the list short: drop bills paid more than 30 days ago
            for (int i = Invoices.Count - 1; i >= 0; i--)
                if (Invoices[i].Paid && Clock.Day - Invoices[i].PaidDay > 30) Invoices.RemoveAt(i);
        }

        // ------------------------------------------------------------------ expiry

        /// <summary>Expired stock scanned out and binned. Suppliers with a returns policy credit part of it.</summary>
        public void WriteOff(Product p, int qty)
        {
            if (qty <= 0) return;
            var t = Economy.Today;
            float value = qty * p.Def.Cost;
            t.WasteValue += value;
            t.WrittenOff += qty;
            p.BinnedToday += qty;
            var s = SupplierOf(p);
            if (s != null && s.Def.ReturnCredit > 0f)
            {
                s.ReturnsValue += value * s.Def.ReturnCredit;
                s.ReturnsUnits += qty;
                t.ReturnCredit += value * s.Def.ReturnCredit;
            }
        }

        /// <summary>Should this stocker do a date-check round now? Claims it so only one goes.</summary>
        public bool ClaimDateCheck(Employee e)
        {
            float now = Clock.AbsMinutes;
            switch (DateChecks)
            {
                case DateCheckPolicy.OnceADay:
                    if (now - lastDailyCheck < 20f * 60f || Clock.Hour < 4 || Clock.Hour > 16) return false;
                    lastDailyCheck = now;
                    return true;
                case DateCheckPolicy.EveryShift:
                    int k = (int)e.Shift;
                    if (now - lastShiftCheck[k] < 10f * 60f) return false;
                    lastShiftCheck[k] = now;
                    return true;
                default:
                    return false;
            }
        }

        public int ExpiredOnShelvesTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].ShelfExpired; return t; }
        }
    }
}
