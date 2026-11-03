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

    public static int Main(string[] args)
    {
        int days = args.Length > 0 ? int.Parse(args[0]) : 8;
        bool scripted = args.Length < 2 || args[1] != "idle";
        UnityEngine.Random.InitState(args.Length > 2 ? int.Parse(args[2]) : 7);

        typeof(Bootstrap).GetMethod("Boot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        store = Store.I;
        Engine.Tick(1f / 30f);   // Start(): sits at the desk, opens build mode
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
        Console.WriteLine();
        Console.WriteLine("Frames " + Engine.Frame + ", exceptions " + Engine.Errors + ", max customers at once " + maxCustomers);
        return Engine.Errors > 0 ? 1 : 0;
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
        foreach (var name in new[] { "Soft Drinks", "Snacks", "Pasta", "Canned Soup", "Soft Drinks", "Snacks" })
            PlaceAnywhere("gondola", name);
        int n = store.OrderAllToPar(0f);
        store.OrderExpress();
        Note("PLAYER: built " + store.Fixtures.Count + " fixtures for " + Economy.Money(cash0 - store.Economy.Cash)
            + ", ordered " + n + " products by express. Cash " + Economy.Money(store.Economy.Cash));
    }

    static int Count(Role r, Shift sh) { return store.Staff.All.Count(e => e != null && !e.Leaving && e.Role == r && e.Shift == sh); }

    static void HireIf(Role r, Shift sh, int want, Func<Applicant, float> score = null, bool essential = false)
    {
        if (store.RoleLock(r) != null) return;
        while (Count(r, sh) < want && store.Staff.Applicants.Count > 0 && (essential || store.Economy.Cash > Reserve))
        {
            var a = score != null ? store.Staff.Applicants.OrderByDescending(score).First() : store.Staff.Applicants[0];
            var e = store.Staff.Hire(a, r, sh, null);
            Note("PLAYER: hired " + e.DisplayName + " as " + Catalog.RoleName(r) + " (" + sh + ")");
        }
    }

    /// <summary>A sensible but unhurried player: checks the store twice a day.</summary>
    static void Script(int day, int hour, HashSet<string> done)
    {
        var s = store;
        if (hour == 21)
        {
            foreach (var p in s.Products)
            {
                if (!p.Carried || p.Baked) continue;
                int cs = p.Def.CaseSize, sug = s.SuggestedPar(p);
                if (p.MissedToday > 0 && p.Back.Total == 0) p.Par = Mathf.Min(p.Par + Mathf.Max(cs, (p.Par / 5) / cs * cs), sug * 3);
                else if (p.Back.Total > p.Par / 2) p.Par = Mathf.Max(sug, p.Par - Mathf.Max(cs, (p.Par / 5) / cs * cs));
            }
            int n = s.OrderAllToPar(0f);
            if (n > 0 && day % 5 == 0) Note("PLAYER: ordered " + n + " products to par");
        }
        if (hour != 9 && hour != 15) return;

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
        HireIf(Role.Cashier, Shift.Morning, cashiers, null, true);
        HireIf(Role.Cashier, Shift.Evening, cashiers, null, true);
        HireIf(Role.Stocker, Shift.Morning, 1 + shelves / 10, null, true);
        HireIf(Role.Stocker, Shift.Evening, Mathf.Max(1, shelves / 12), null, true);
        HireIf(Role.Cleaner, Shift.Morning, 1);
        if (s.Economy.Cash > 12000f) HireIf(Role.AssistantManager, Shift.Evening, 1, a => a.Leadership);
        if (s.Staff.Assistant != null && done.Add("delegate")) { s.Delegation.Ordering = true; s.Delegation.Complaints = true; s.Delegation.Checkouts = true; }
        if (s.Fixtures.Any(f => f.Def.Id == "oven")) { HireIf(Role.Baker, Shift.Night, 1, a => a.Specialty == Specialty.Baking ? 1 : 0); HireIf(Role.Baker, Shift.Morning, 1); }
        if (s.Fixtures.Any(f => f.Def.Id == "deli_counter")) { HireIf(Role.DeliClerk, Shift.Morning, 1, a => a.Specialty == Specialty.Butchery ? 1 : 0); HireIf(Role.DeliClerk, Shift.Evening, 1); }

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
            for (int t = 0; t < 9; t++) { hud.ShowTab(t); onGui.Invoke(hud, null); }
            BuildMode.I.StartPlacing(Catalog.FixtureById("checkout"));
            onGui.Invoke(hud, null);
            BuildMode.I.CancelTool();
            foreach (var f in store.Fixtures.Take(12)) { BuildMode.I.Selected = f; onGui.Invoke(hud, null); }
            hud.ShowTab(1);
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
