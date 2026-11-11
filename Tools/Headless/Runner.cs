// Headless simulation run of Storemanager on top of FakeUnity.
// The scripted "player" builds a store from empty, chooses products, orders, hires and expands.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StoreManager;
using UnityEngine;

public static class Runner
{
    static Store store;
    static readonly Dictionary<Customer, float> born = new Dictionary<Customer, float>();
    static int maxCustomers, stuckReports;
    static bool overstock;

    public static int Main(string[] args)
    {
        int days = args.Length > 0 ? int.Parse(args[0]) : 8;
        bool scripted = args.Length < 2 || args[1] != "idle";
        UnityEngine.Random.InitState(args.Length > 2 ? int.Parse(args[2]) : 7);

        typeof(Bootstrap).GetMethod("Boot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        store = Store.I;
        Engine.Tick(1f / 30f);   // Start(): sits at the desk, opens build mode
        overstock = args.Length > 3 && args[3] == "overstock";
        GuiPass();   // the new-game screen
        bool crew = args.Length > 3 && args[3] == "crew";
        store.ChooseStart(!crew);
        Console.WriteLine("   >> START: " + (crew ? "small crew, open from day one" : "hiring everyone, opening when ready"));
        if (overstock) { store.DateChecks = DateCheckPolicy.WhenRestocking; Console.WriteLine("   >> TEST: over-ordering x3, date checks only when restocking"); }
        if (scripted) BuildStarterStore();
        store.Clock.SetSpeed(3); // 4x

        const float dt = 1f / 30f;
        int lastDay = store.Clock.Day, lastHour = store.Clock.Hour;
        var done = new HashSet<string>();

        while (store.Clock.Day <= days)
        {
            Engine.Tick(dt);
            var clk = store.Clock;
            if (clk.Hour != lastHour)
            {
                lastHour = clk.Hour;
                if (scripted) Script(clk.Day, clk.Hour, done);
                if (clk.Hour == 12 || clk.Hour == 18) Snapshot();
                if (clk.Hour == 20) GuiPass();
            }
            if (clk.Day != lastDay)
            {
                lastDay = clk.Day;
                PrintDay(store.Economy.History[store.Economy.History.Count - 1]);
            }
            Track();
            PrintLog();
        }
        if (args.Length > 3 && args[3] == "savetest") SaveRoundTrip(dt, scripted, done);
        Console.WriteLine();
        if (overstock) foreach (var p in store.Products.Where(p => p.Carried && p.Life > 0))
            Console.WriteLine("   batches " + p.Name + " par " + p.Par + " back[" + string.Join(" ", p.Back.Batches.Select(b => b.Qty + "@" + b.DaysLeft))
                + "] shelves[" + string.Join(" ", p.Fixtures.SelectMany(f => f.Stock.Batches).Select(b => b.Qty + "@" + b.DaysLeft)) + "] sold " + p.SoldToday);
        Console.WriteLine("Frames " + Engine.Frame + ", exceptions " + Engine.Errors + ", max customers at once " + maxCustomers);
        return Engine.Errors > 0 ? 1 : 0;
    }

    /// <summary>Saves, loads into a fresh game, saves again and compares; then plays on for two days.</summary>
    static void SaveRoundTrip(float dt, bool scripted, HashSet<string> done)
    {
        // Save in the middle of a busy afternoon: customers in the store, staff on shift, stock moving.
        int h = store.Clock.Hour;
        while (store.Clock.Hour != 14)
        {
            Engine.Tick(dt);
            if (store.Clock.Hour != h) { h = store.Clock.Hour; if (scripted) Script(store.Clock.Day, h, done); }
        }
        Console.WriteLine("   >> SAVE TEST at " + store.Clock.Weekday + " " + store.Clock.TimeText + ": " + store.Customers.Count + " customers in the store");
        string a = store.WriteSave();
        int employees = store.Staff.All.Count, fixtures = store.Fixtures.Count(f => !f.Removed);
        string why = GameSession.LoadText(a);
        if (why != null) { Console.WriteLine("   !! load failed: " + why); Engine.Errors++; return; }
        store = Store.I;
        born.Clear();
        string b = store.WriteSave();
        Engine.Tick(dt);
        var la = a.Split('\n').Where(l => !l.StartsWith("log\t") && !l.StartsWith("clock\t")).Skip(1).ToList();
        var lb = b.Split('\n').Where(l => !l.StartsWith("log\t") && !l.StartsWith("clock\t")).Skip(1).ToList();
        int diffs = 0;
        for (int i = 0; i < Math.Max(la.Count, lb.Count); i++)
        {
            string x = i < la.Count ? la[i] : "(none)", y = i < lb.Count ? lb[i] : "(none)";
            if (x == y) continue;
            if (diffs++ < 8) Console.WriteLine("   SAVE DIFF\n     before: " + x.Replace('\t', '|') + "\n     after:  " + y.Replace('\t', '|'));
        }
        Console.WriteLine("   >> SAVE TEST: " + a.Length + " chars, " + la.Count + " records, " + employees + " staff, " + fixtures
            + " fixtures; after load " + store.Staff.All.Count + " staff, " + store.Fixtures.Count + " fixtures; " + diffs + " records differ");
        if (diffs > 0) Engine.Errors++;
        store.Clock.SetSpeed(3);
        int end = store.Clock.Day + 2, lastDay = store.Clock.Day, lastHour = store.Clock.Hour;
        while (store.Clock.Day <= end)
        {
            Engine.Tick(dt);
            if (store.Clock.Hour != lastHour)
            {
                lastHour = store.Clock.Hour;
                if (scripted) Script(store.Clock.Day, store.Clock.Hour, done);
            }
            if (store.Clock.Day != lastDay)
            {
                lastDay = store.Clock.Day;
                PrintDay(store.Economy.History[store.Economy.History.Count - 1]);
            }
            Track();
            PrintLog();
        }

        // And a new game from there: an empty shop waiting for you to choose how to start.
        GameSession.NewGame();
        store = Store.I;
        born.Clear();
        for (int i = 0; i < 60; i++) Engine.Tick(dt);
        Console.WriteLine("   >> NEW GAME: day " + store.Clock.Day + ", " + store.Fixtures.Count + " fixtures, " + store.Staff.All.Count
            + " staff, cash " + Economy.Money(store.Economy.Cash) + ", start chosen " + store.StartChosen);
        if (store.Fixtures.Count != 0 || store.StartChosen) Engine.Errors++;
    }

    static Fixture Place(string id, int x, int z, int rot = 0, string product = null)
    {
        var def = Catalog.FixtureById(id);
        var o = new Vector2Int(x, z);
        string why = store.CanPlace(def, o, rot);
        if (why != null) return null;
        var f = store.Place(def, o, rot);
        if (product != null && !store.AssignProduct(f, Catalog.ProductByName(product)))
            Console.WriteLine("   !! can't put " + product + " on " + id);
        return f;
    }

    /// <summary>Finds a tidy spot: checkouts in a row by the entrance, shelves in aisles, big things at the back.</summary>
    static Fixture PlaceAnywhere(string id, string product = null)
    {
        var def = Catalog.FixtureById(id);
        int W = store.Grid.W;
        if (id == "checkout")
        {
            for (int x = 4; x < W - 2; x += 3) { var f = Place(id, x, 6, 0, product); if (f != null) return f; }
            return null;
        }
        if (id == "deli_counter" || id == "oven")
        {
            for (int x = W - 3; x >= 2; x--) { var f = Place(id, x, id == "oven" ? 23 : 23, 0, product); if (f != null) return f; }
            for (int z = 21; z >= 13; z--)
                for (int x = W - 3; x >= 2; x--) { var f = Place(id, x, z, 0, product); if (f != null) return f; }
            return null;
        }
        for (int x = 2; x < W - 1; x += 3)
            for (int z = 13; z <= 21; z += 2) { var f = Place(id, x, z, 0, product); if (f != null) return f; }
        return null;
    }

    static float Reserve { get { return 2500f + 120f * store.Staff.All.Count; } }

    static void BuildStarterStore()
    {
        float cash0 = store.Economy.Cash;
        PlaceAnywhere("checkout");
        PlaceAnywhere("checkout");
        foreach (var name in new[] { "Bubbla Cola", "Snacks", "Pasta", "Canned Soup", "Zing Cola", "Snacks" })
            PlaceAnywhere("gondola", name);
        int n = FillCart();
        store.FitToSuppliers(store.Cart, true);
        store.PlaceOrder(store.Cart, true);
        Note("PLAYER: built " + store.Fixtures.Count + " fixtures for " + Economy.Money(cash0 - store.Economy.Cash)
            + ", ordered " + n + " products by express. Cash " + Economy.Money(store.Economy.Cash));
    }

    /// <summary>The player's own shopping: cases up to the par they keep in their head, trimmed to space and cash.</summary>
    static int FillCart()
    {
        var s = store;
        // Pay the oldest bills of any supplier whose credit is running low, as long as cash allows.
        foreach (var sup in s.Suppliers)
        {
            var bills = s.Invoices.Where(i => !i.Paid && i.Supplier == sup).OrderBy(i => i.IssuedDay).ToList();
            foreach (var inv in bills)
            {
                if (s.CreditLeft(sup) >= s.CreditLimit(sup) * 0.5f || s.Economy.Cash - inv.Amount < 1500f) break;
                s.PayInvoice(inv);
            }
        }
        s.Cart.Clear();
        foreach (var p in s.Products)
        {
            if (!p.Carried || p.Baked) continue;
            var sup = s.SupplierOf(p);
            if (sup == null || sup.OnHold) continue;
            int need = p.Par * (overstock ? 3 : 1) - (p.FreshOnHand + p.Incoming);
            if (need > 0) s.Cart.Add(p, Mathf.CeilToInt(need / (float)p.Def.CaseSize));
        }
        s.FitToSuppliers(s.Cart, false);
        return s.Cart.Lines.Count;
    }

    static bool CanCover(Applicant a, int d, ShiftSlot slot)
    {
        if (slot == ShiftSlot.Night && a.NoNights) return false;
        if (slot == ShiftSlot.Morning && a.NoMornings && d < 5) return false;
        if (a.NoWeekends && d >= 5) return false;
        return true;
    }

    /// <summary>
    /// Hires until every day has `want` people of this role on this shift: a full-timer for five uncovered days,
    /// a part-timer for one to three. Interviews a couple of candidates first and picks the best-sounding one.
    /// </summary>
    static void HireIf(Role r, ShiftSlot slot, int want, Func<Applicant, float> score = null, bool essential = false)
    {
        var s = store;
        if (s.RoleLock(r) != null) return;
        for (int guard = 0; guard < 4; guard++)
        {
            // an extra cashier on the busy days (Fri, Sat)
            var missing = Enumerable.Range(0, 7).Where(d => s.Staff.Coverage(r, d, slot) < want + (r == Role.Cashier && Store.DayFactor[d] >= 1.1f ? 1 : 0)).ToList();
            if (missing.Count == 0 || s.Staff.Applicants.Count == 0 || (!essential && s.Economy.Cash < Reserve)) return;
            bool full = missing.Count >= 4;
            var days = full ? missing.OrderBy(d => d >= 5 ? 1 : 0).Take(5).ToList() : missing.Take(3).ToList();
            var pool = s.Staff.Applicants.Where(a => days.All(d => CanCover(a, d, slot)) && (full ? !a.WantsPartTime : !a.WantsFullTime)).ToList();
            if (pool.Count == 0) return;
            foreach (var a in pool.Take(2))
                for (int q = 0; q < 8 && a.Asked.Count < Interview.MaxQuestions; q += 3) s.Staff.Ask(a, q % 8);
            var pick = score != null ? pool.OrderByDescending(score).First()
                : pool.OrderByDescending(a => a.Mid(0) + a.Mid(1) + a.Mid(4)).First();
            string result;
            var e = s.Staff.Offer(pick, r, full ? Contract.FullTime : Contract.PartTime, slot, pick.AskingWage, out result);
            if (e == null) { Note("PLAYER: offer failed: " + result); return; }
            for (int d = 0; d < 7; d++) e.Week[d] = ShiftSlot.Off;
            foreach (int d in days) e.Week[d] = slot;
            Note("PLAYER: hired " + e.DisplayName + " as " + Catalog.RoleName(r) + " " + (full ? "FT" : "PT") + " " + slot + " on "
                + string.Join(",", days.Select(d => StaffManager.DayNames[d])));
        }
    }

    /// <summary>Reviews, warnings, dismissals and promotions, like a player looking at the Staff app each morning.</summary>
    static void ManageStaff(int day)
    {
        var s = store;
        var staff = s.Staff;
        foreach (var e in staff.All.ToList())
        {
            if (e == null || e.Leaving) continue;
            if (staff.ReviewDue(e))
            {
                string sum;
                int rating = staff.Rating(e, out sum);
                Note("PLAYER: " + staff.Review(e, rating >= 5 ? 2f : rating >= 4 ? 1f : 0f));
            }
            var inc = e.WarnableIncident;
            if (inc != null && (inc.Kind == IncidentKind.NoShow || inc.Kind == IncidentKind.LongBreak || inc.Kind == IncidentKind.Late))
            {
                if (e.ActiveWrittenWarnings >= 2 && staff.FireProblem(e) == null) { Note("PLAYER: dismissed " + e.DisplayName + " after two written warnings"); staff.Fire(e); continue; }
                if (e.Record.Count(r => r.Day > day - 7 && (r.Kind == IncidentKind.Late || r.Kind == IncidentKind.LongBreak || r.Kind == IncidentKind.NoShow)) >= 2)
                    Note("PLAYER: " + e.WrittenWarning());
            }
        }
        // one shift lead per main shift, the best leaders first
        foreach (var slot in new[] { ShiftSlot.Morning, ShiftSlot.Evening })
        {
            bool hasLead = staff.All.Any(e => e != null && e.IsLead && e.Week.Count(w => w == slot) >= 3);
            if (hasLead) continue;
            var cand = staff.All.Where(e => e != null && e.Rank == Rank.Staff && e.Role != Role.AssistantManager && e.Week.Count(w => w == slot) >= 3
                && staff.PromoteProblem(e, null) == null).OrderByDescending(e => e.Leadership).FirstOrDefault();
            if (cand != null) Note("PLAYER: " + staff.Promote(cand, null));
        }
        // a department manager for dairy once there is one
        var dairy = s.Categories.FirstOrDefault(c => c.Def.Id == "dairy");
        if (dairy != null && dairy.Products.Any(p => p.Carried) && !s.DepartmentOrdered(dairy))
        {
            var lead = staff.All.FirstOrDefault(e => e != null && e.Rank == Rank.ShiftLead && staff.PromoteProblem(e, dairy) == null);
            if (lead != null) Note("PLAYER: " + staff.Promote(lead, dairy));
        }
    }

    /// <summary>A sensible but unhurried player: checks the store twice a day.</summary>
    static void Script(int day, int hour, HashSet<string> done)
    {
        var s = store;
        if (!s.Opened && s.OpenProblem() == null)
        {
            s.OpenStore();
            Note("PLAYER: opened the store on day " + day + " at " + s.Clock.TimeText + " with " + s.Staff.All.Count + " staff");
        }
        if (hour == 21)
        {
            foreach (var p in s.Products)
            {
                if (!p.Carried || p.Baked) continue;
                int cs = p.Def.CaseSize, sug = s.SuggestedPar(p);
                if (p.MissedToday > 0 && p.Back.Total == 0) p.Par = Mathf.Min(p.Par + Mathf.Max(cs, (p.Par / 5) / cs * cs), sug * 3);
                else if (p.Back.Total > p.Par / 2) p.Par = Mathf.Max(sug, p.Par - Mathf.Max(cs, (p.Par / 5) / cs * cs));
            }
            int n = FillCart();
            string why = s.OrderProblem(s.Cart, false);
            if (n > 0 && !s.PlaceOrder(s.Cart, false)) Note("PLAYER: couldn't order: " + why);
            s.Cart.Clear();
        }
        if (hour != 9 && hour != 15) return;
        if (hour == 9)
        {
            float before = s.Economy.Cash;
            int paid = s.PayBillsDueWithin(2, 500f);
            if (paid > 0 && day % 5 == 0) Note("PLAYER: paid " + paid + " bills (" + Economy.Money(before - s.Economy.Cash) + ")");
        }

        // 1. licences: cheapest unlocked first, keep a reserve for fixtures
        foreach (var l in Catalog.Licences.OrderBy(l => l.Level).ThenBy(l => l.Cost))
        {
            if (s.Owns(l) || s.LicenceLock(l) != null) continue;
            float fixtures = 0f;
            foreach (var fid in l.Fixtures) fixtures += Catalog.FixtureById(fid).Cost;
            fixtures += l.Products.Count * 900f;
            if (s.Economy.Cash - l.Cost - fixtures < Reserve) continue;
            s.BuyLicence(l);
            Note("PLAYER: bought licence " + l.Name + " (day " + day + ", level " + s.Level + "). Cash " + Economy.Money(s.Economy.Cash));
        }

        // 2. ovens before bakery shelves
        if (s.Owns(Catalog.LicenceById("bakery")) && !s.Fixtures.Any(f => f.Def.Id == "oven") && s.Economy.Cash > 6000f + Reserve)
            if (PlaceAnywhere("oven") != null) Note("PLAYER: placed an oven");

        // 3. a fixture for every licensed product not on sale yet
        foreach (var p in s.Products)
        {
            if (p.Carried || !s.IsLicensed(p.Def)) continue;
            var fid = p.Def.Category.Fixture;
            if (p.Baked && !s.Fixtures.Any(f => f.Def.Id == "oven")) continue;
            if (s.Economy.Cash < Catalog.FixtureById(fid).Cost + Reserve) continue;
            if (PlaceAnywhere(fid, p.Name) != null) Note("PLAYER: new " + fid + " for " + p.Name);
            else if (done.Add("full-" + fid)) Note("PLAYER: no room for a " + fid);
        }

        // 4. second shelves for best sellers (evening check only)
        if (hour == 15)
            foreach (var p in s.Products.Where(p => p.Carried && !p.Baked).OrderByDescending(p => p.SoldToday).Take(3))
            {
                if (p.SoldToday < p.ShelfCap * 0.9f || p.Fixtures.Count >= 3) continue;
                var fid = p.Def.Category.Fixture;
                if (s.Economy.Cash < Catalog.FixtureById(fid).Cost + Reserve * 2f) continue;
                if (PlaceAnywhere(fid, p.Name) != null) Note("PLAYER: extra " + fid + " for " + p.Name + " (sold " + p.SoldToday + " by 15:00)");
            }

        // 5. checkouts by queue pressure
        var y = s.Economy.History.Count > 0 ? s.Economy.History[s.Economy.History.Count - 1] : null;
        int lanes = s.Lanes.Count;
        if (y != null && hour == 9 && y.Walkouts >= 4 && lanes < 6 && s.Economy.Cash > 1500f + Reserve * 2f)
            if (PlaceAnywhere("checkout") != null) { lanes++; Note("PLAYER: extra checkout (" + y.Walkouts + " walkouts yesterday)"); }

        // 6. staff
        int shelves = s.Fixtures.Count(f => f.HoldsProducts && !f.IsCounter);
        int cashiers = Mathf.Min(lanes, 1 + (int)(s.ExpectedCustomersPerDay / 220f));
        if (hour == 9) ManageStaff(day);
        HireIf(Role.Cashier, ShiftSlot.Morning, cashiers, null, true);
        HireIf(Role.Cashier, ShiftSlot.Evening, cashiers, null, true);
        HireIf(Role.Stocker, ShiftSlot.Morning, 1 + shelves / 10, null, true);
        HireIf(Role.Stocker, ShiftSlot.Evening, Mathf.Max(1, shelves / 12), null, true);
        HireIf(Role.Cleaner, ShiftSlot.Morning, 1);
        if (s.Economy.Cash > 12000f && s.Staff.Assistant == null) HireIf(Role.AssistantManager, ShiftSlot.Evening, 1, a => a.Mid(3));
        if (s.Staff.Assistant != null && done.Add("delegate")) { s.Delegation.Ordering = true; s.Delegation.Complaints = true; s.Delegation.Checkouts = true; }
        if (s.Fixtures.Any(f => f.Def.Id == "oven")) { HireIf(Role.Baker, ShiftSlot.Night, 1, a => a.Specialty == Specialty.Baking ? 1 : 0); HireIf(Role.Baker, ShiftSlot.Morning, 1); }
        if (s.Fixtures.Any(f => f.Def.Id == "deli_counter")) { HireIf(Role.DeliClerk, ShiftSlot.Morning, 1, a => a.Specialty == Specialty.Butchery ? 1 : 0); HireIf(Role.DeliClerk, ShiftSlot.Evening, 1); }

        // 7. expansion
        if (s.CanExpand && s.Economy.Cash > s.ExpansionCost + Reserve * 2f)
        {
            s.Expand();
            Note("PLAYER: expansion ordered (day " + day + ", level " + s.Level + "). Cash " + Economy.Money(s.Economy.Cash));
        }
    }

    static void Note(string s) { Console.WriteLine("   >> " + s); }

    static void Track()
    {
        maxCustomers = Math.Max(maxCustomers, store.Customers.Count);
        float now = Time.time;
        foreach (var c in store.Customers)
        {
            if (c == null) continue;
            if (!born.ContainsKey(c)) born[c] = now;
            else if (now - born[c] > 600f && stuckReports < 15)
            {
                stuckReports++;
                Console.WriteLine("   !! STUCK customer " + c.DisplayName + " for " + (now - born[c]).ToString("0") + "s: " + c.Status
                    + " at " + c.Tile + " moving=" + c.IsMoving + " lane=" + (c.Lane != null ? c.Lane.Index.ToString() : "-"));
                born[c] = now;
            }
        }
        if (Engine.Frame % 3000 == 0)
        {
            var dead = born.Keys.Where(k => k == null).ToList();
            foreach (var d in dead) born.Remove(d);
        }
    }

    static void Snapshot()
    {
        var c = store.Clock;
        var lanes = string.Join(" ", store.Lanes.Select(l => (l.Staffed ? (l.Closing ? "c" : "o") : "x") + l.Queue.Count));
        var staff = string.Join(", ", store.Staff.All.Where(e => e != null && e.OnShift).Select(e => e.Role.ToString().Substring(0, 3) + ":" + e.Status));
        Console.WriteLine("   [" + c.Weekday + " " + c.TimeText + "] in store " + store.Customers.Count + "  lanes " + lanes
            + "  spills " + store.Dirt.Spills.Count + "  back " + store.BackStockTotal);
        Console.WriteLine("      staff: " + staff);
        var empties = store.Products.Where(p => p.Carried && p.ShelfTotal == 0)
            .Select(p => p.Name + (p.Back.Total > 0 ? "(back " + p.Back.Total + ")" : "(none)")).ToList();
        if (empties.Count > 0) Console.WriteLine("      empty: " + string.Join(", ", empties));
    }

    static void PrintDay(DayLedger d)
    {
        Console.WriteLine("LEVEL " + store.Level + " xp " + store.XP + "  licences " + Catalog.Licences.Count(l => store.Owns(l)) + "/" + Catalog.Licences.Count
            + "  fixtures " + store.Fixtures.Count + "  staff " + store.Staff.All.Count + "  width " + store.Grid.W);
        Console.WriteLine(string.Format(
            "DAY {0}: customers {1}, items {2}, sales {3}, wholesale {4}, wages {5}, rent {6}, build {7}, PROFIT {8} | sat {9:0} rep {10:0.0} | walkouts {11}, complaints {12}/{13} resolved | waste {14} | cash {15}",
            d.Day, d.Customers, d.ItemsSold, Economy.Money(d.Sales), Economy.Money(d.Wholesale), Economy.Money(d.Wages),
            Economy.Money(d.Rent), Economy.Money(d.Construction), Economy.Money(d.Profit), d.AvgSatisfaction, store.Reputation,
            d.Walkouts, d.ComplaintsResolved, d.Complaints, Economy.Money(d.WasteValue), Economy.Money(store.Economy.Cash)));
        Console.WriteLine("      expiry: binned " + d.WrittenOff + " (" + Economy.Money(d.WasteValue) + ", credit " + Economy.Money(d.ReturnCredit)
            + ")  customers found expired " + d.ExpiredFound + "  still on shelves " + store.ExpiredOnShelvesTotal
            + " | bills unpaid " + Economy.Money(store.UnpaidTotal) + " overdue " + store.OverdueCount + " late fees " + Economy.Money(d.LateFees));
        var st = store.Staff.All.Where(e => e != null).ToList();
        int lateToday = st.Sum(e => e.Record.Count(r => r.Day == d.Day && r.Kind == IncidentKind.Late));
        int longB = st.Sum(e => e.Record.Count(r => r.Day == d.Day && r.Kind == IncidentKind.LongBreak));
        int sick = st.Sum(e => e.Record.Count(r => r.Day == d.Day && (r.Kind == IncidentKind.Sick || r.Kind == IncidentKind.NoShow)));
        float slack = st.Sum(e => e.Reports.Where(r => r.Day == d.Day).Sum(r => r.SlackMinutes));
        Console.WriteLine("      staff: " + st.Count + " (" + st.Count(e => e.Contract == Contract.FullTime) + " FT, " + st.Count(e => e.IsLead) + " leads)  late " + lateToday
            + "  long breaks " + longB + "  sick/no-show " + sick + "  slack " + slack.ToString("0") + " min  wages " + Economy.Money(d.Wages));
        if (d.SatisfactionLost.Count > 0)
            Console.WriteLine("      upset by: " + string.Join(", ", d.SatisfactionLost.OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key + " " + (kv.Value / Math.Max(1f, d.Customers)).ToString("0.0") + "/cust")));
        if (d.NotCarried.Count > 0)
            Console.WriteLine("      not carried: " + string.Join(", ", d.NotCarried.Select(kv => kv.Key + " " + kv.Value)));
    }

    static void PrintLog()
    {
        for (int i = 0; i < store.Log.Count; i++) Console.WriteLine("   " + store.Log[i]);
        store.Log.Clear();
    }

    /// <summary>Runs every office tab, the build palette and the fixture panel through OnGUI to catch null refs.</summary>
    static void GuiPass()
    {
        var hud = store.GetComponent<Hud>();
        var onGui = typeof(Hud).GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            hud.Selected = store.Staff.All.FirstOrDefault(e => e != null && e.OnShift);
            hud.SelectedCustomer = store.Customers.FirstOrDefault(c => c != null);
            BuildMode.I.Selected = store.Fixtures.FirstOrDefault(f => f.Product != null);
            store.Avatar.StandUp();
            onGui.Invoke(hud, null);
            store.Avatar.SitDown();
            store.Cam.LockBlend = 1f;
            onGui.Invoke(hud, null);   // desktop with widgets (and the opening checklist before opening)
            for (int t = 0; t < Hud.AppCount; t++) hud.OpenApp((Hud.App)t);
            onGui.Invoke(hud, null);
            var F = BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var e in store.Staff.All.Where(x => x != null).Take(6))
            {
                typeof(Hud).GetField("staffSel", F).SetValue(hud, e);
                onGui.Invoke(hud, null);
            }
            var lead = store.Staff.All.FirstOrDefault(x => x != null && x.Rank == Rank.ShiftLead);
            if (lead != null) { typeof(Hud).GetField("staffSel", F).SetValue(hud, lead); onGui.Invoke(hud, null); }
            foreach (var a in store.Staff.Applicants.Take(3))
            {
                typeof(Hud).GetField("appSel", F).SetValue(hud, a);
                onGui.Invoke(hud, null);
            }
            typeof(Hud).GetField("statsPage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, 1);
            var some = store.Products.FirstOrDefault(p => p.Carried && !p.Baked);
            if (some != null) store.Cart.Add(some, 2);
            onGui.Invoke(hud, null);
            store.Cart.Clear();
            typeof(Hud).GetField("statsPage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, 0);
            hud.OpenBuildView();
            onGui.Invoke(hud, null);
            BuildMode.I.StartPlacing(Catalog.FixtureById("checkout"));
            onGui.Invoke(hud, null);
            BuildMode.I.CancelTool();
            foreach (var f in store.Fixtures.Take(12)) { BuildMode.I.Selected = f; onGui.Invoke(hud, null); }
            hud.OpenPC();
            hud.Selected = null;
            hud.SelectedCustomer = null;
            BuildMode.I.Selected = null;
        }
        catch (TargetInvocationException e)
        {
            Engine.Errors++;
            Console.WriteLine("EXCEPTION in GUI: " + e.InnerException);
        }
    }
}
