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
        if (why != null) { Console.WriteLine("   !! can't place " + id + " at " + x + "," + z + " r" + rot + ": " + why); return null; }
        var f = store.Place(def, o, rot);
        if (product != null && !store.AssignProduct(f, Catalog.ProductByName(product)))
            Console.WriteLine("   !! can't put " + product + " on " + id);
        return f;
    }

    static void BuildStarterStore()
    {
        float cash0 = store.Economy.Cash;
        for (int i = 0; i < 3; i++) Place("checkout", 10 + i * 3, 6);

        string[] dry = { "Soft Drinks", "Snacks", "Pasta", "Cereal", "Canned Soup", "Rice", "Coffee", "Soft Drinks", "Snacks", "Pasta",
                         "Toilet Paper", "Detergent", "Dish Soap", "Batteries", "Cereal" };
        int k = 0;
        foreach (int x in new[] { 2, 5, 8 })
            for (int z = 14; z <= 20; z += 2)
                Place("gondola", x, z, 0, dry[k++ % dry.Length]);

        string[] fresh = { "Bananas", "Apples", "Tomatoes", "Potatoes", "Lettuce" };
        k = 0;
        foreach (int x in new[] { 14 })
            for (int z = 14; z <= 22; z += 2)
                Place("produce", x, z, 0, fresh[k++ % fresh.Length]);

        string[] dairy = { "Milk", "Eggs", "Cheese", "Yogurt", "Frozen Pizza", "Ice Cream", "Milk", "Yogurt", "Cheese", "Eggs" };
        k = 0;
        foreach (int x in new[] { 20, 23 })
            for (int z = 14; z <= 22; z += 2)
                Place("fridge", x, z, 0, dairy[k++ % dairy.Length]);

        // a sideways shelf to check rotation
        Place("gondola", 27, 10, 1, "Snacks");

        int n = store.OrderAllToPar(0f);
        store.OrderExpress();
        Note("PLAYER: built " + store.Fixtures.Count + " fixtures for " + Economy.Money(cash0 - store.Economy.Cash)
            + ", ordered " + n + " products by express. Cash " + Economy.Money(store.Economy.Cash));
    }

    static void Script(int day, int hour, HashSet<string> done)
    {
        var s = store;
        if (day == 1 && hour == 20) { int n = s.OrderAllToPar(0f); Note("PLAYER: ordered " + n + " products to par"); }

        if (day == 2 && hour == 9 && done.Add("am"))
        {
            var best = s.Staff.Applicants.OrderByDescending(a => a.Leadership).First();
            var e = s.Staff.Hire(best, Role.AssistantManager, Shift.Evening, null);
            Note("PLAYER: hired " + e.DisplayName + " as assistant manager (leadership " + e.Leadership + ")");
            var n = s.Staff.Hire(s.Staff.Applicants[0], Role.Stocker, Shift.Night, null);
            Note("PLAYER: hired " + n.DisplayName + " as night stocker");
            var c = s.Staff.Hire(s.Staff.Applicants[0], Role.Cashier, Shift.Evening, null);
            Note("PLAYER: hired " + c.DisplayName + " as evening cashier");
        }

        if (day == 3 && hour == 9 && done.Add("bakery"))
        {
            Place("oven", 31, 14, 0);
            Place("bakery_shelf", 27, 14, 0, "Bread");
            Place("bakery_shelf", 27, 16, 0, "Croissants");
            var pick = s.Staff.Applicants.OrderByDescending(a => a.Specialty == Specialty.Baking ? 10 : 0).First();
            var b = s.Staff.Hire(pick, Role.Baker, Shift.Morning, null);
            var b2 = s.Staff.Hire(s.Staff.Applicants[0], Role.Baker, Shift.Night, null);
            Note("PLAYER: bakery built, bakers " + b.DisplayName + " (" + b.Specialty + ") and " + b2.DisplayName + ". Cash " + Economy.Money(s.Economy.Cash));
        }

        if (day == 4 && hour == 11 && done.Add("move"))
        {
            var g = s.Fixtures.FirstOrDefault(f => f.Def.Id == "gondola" && f.Rot == 1);
            if (g != null)
            {
                string why = s.Move(g, new Vector2Int(36, 14), 0);
                Note("PLAYER: moved the sideways shelf: " + (why ?? "ok") + ", it still holds " + (g.Product != null ? g.Product.Name : "nothing"));
            }
            var sell = s.Fixtures.LastOrDefault(f => f.Def.Id == "gondola");
            if (sell != null) { var p = sell.Product; s.Sell(sell); Note("PLAYER: sold a shelf of " + (p != null ? p.Name : "-") + ", back room now " + (p != null ? p.Back.Total : 0)); }
            var emp = s.Staff.All.FirstOrDefault(x => x.OnShift && x.Role == Role.Stocker);
            if (emp != null) { s.Avatar.StandUp(); s.Avatar.Teleport(emp.Tile); Note("PLAYER: " + emp.Praise()); s.Avatar.SitDown(); }
        }

        if (day == 6 && hour == 9 && done.Add("deli"))
        {
            Place("deli_counter", 32, 21, 0, "Chicken");
            Place("deli_counter", 36, 21, 0, "Sliced Ham");
            Place("deli_counter", 40, 21, 0, "Steak");
            var c = s.Staff.Hire(s.Staff.Applicants[0], Role.DeliClerk, Shift.Morning, null);
            var c2 = s.Staff.Hire(s.Staff.Applicants[0], Role.DeliClerk, Shift.Evening, null);
            s.OrderAllToPar(0f);
            Note("PLAYER: deli built, clerks " + c.DisplayName + " and " + c2.DisplayName + ". Cash " + Economy.Money(s.Economy.Cash));
        }
        if (day == 7 && hour == 9 && done.Add("expand"))
        {
            if (s.CanExpand) { s.Expand(); Note("PLAYER: expansion ordered, cash " + Economy.Money(s.Economy.Cash)); }
            else Note("PLAYER: can't afford expansion (" + Economy.Money(s.Economy.Cash) + ")");
        }
        if (day == 8 && hour == 9 && done.Add("after-expand"))
            Place("gondola", 50, 14, 0, "Coffee");
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
            for (int t = 0; t < 8; t++) { hud.ShowTab(t); onGui.Invoke(hud, null); }
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
