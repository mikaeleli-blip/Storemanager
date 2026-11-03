// Headless simulation run of Storemanager on top of FakeUnity.
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
        store.Clock.SetSpeed(3); // 4x, like a player fast-forwarding

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

    static void Script(int day, int hour, HashSet<string> done)
    {
        var s = store;
        // Day 1 evening: the player orders tomorrow's stock by hand.
        if (day == 1 && hour == 20) { int n = s.OrderAllToPar(0f); Note("PLAYER: ordered " + n + " products to par"); }

        // Day 2: hire an assistant manager and a night stocker.
        if (day == 2 && hour == 9 && done.Add("am"))
        {
            var best = s.Staff.Applicants.OrderByDescending(a => a.Leadership).First();
            var e = s.Staff.Hire(best, Role.AssistantManager, Shift.Evening, null);
            Note("PLAYER: hired " + e.DisplayName + " as assistant manager (leadership " + e.Leadership + ")");
            var a2 = s.Staff.Applicants[0];
            var n = s.Staff.Hire(a2, Role.Stocker, Shift.Night, null);
            Note("PLAYER: hired " + n.DisplayName + " as night stocker");
        }

        // Day 3: build a bakery in the empty slot and hire a baker.
        if (day == 3 && hour == 10 && done.Add("bakery"))
        {
            s.BuildDepartment(3, Catalog.Get("bakery"));
            var pick = s.Staff.Applicants.OrderByDescending(a => a.Specialty == Specialty.Baking ? 10 : 0).First();
            var b = s.Staff.Hire(pick, Role.Baker, Shift.Night, null);
            Note("PLAYER: building bakery, hired baker " + b.DisplayName + " (" + b.Specialty + ")");
        }

        // Day 4: the bakery is empty all day with only a night baker; add a morning one.
        if (day == 4 && hour == 9 && done.Add("baker2"))
        {
            var b2 = s.Staff.Hire(s.Staff.Applicants[0], Role.Baker, Shift.Morning, null);
            Note("PLAYER: hired morning baker " + b2.DisplayName + " (" + b2.Specialty + ")");
        }

        // Day 4: walk the floor for a while (manager presence), praise someone.
        if (day == 4 && hour == 11 && done.Add("walk"))
        {
            var emp = s.Staff.All.FirstOrDefault(x => x.OnShift && x.Role == Role.Stocker);
            if (emp != null) { s.Avatar.Teleport(emp.Tile); Note("PLAYER: " + emp.Praise()); }
        }

        // Day 5: expand, then build a deli on day 6 and hire a deli clerk + more cashiers.
        if (day == 5 && hour == 9 && done.Add("expand"))
        {
            if (s.CanExpand) { s.Expand(); Note("PLAYER: expansion ordered, cash now " + Economy.Money(s.Economy.Cash)); }
            else Note("PLAYER: can't afford expansion (" + Economy.Money(s.Economy.Cash) + ")");
        }
        if (day == 8 && hour == 9 && done.Add("deli"))
        {
            if (s.CanBuild(4, Catalog.Get("deli")))
            {
                s.BuildDepartment(4, Catalog.Get("deli"));
                var c = s.Staff.Hire(s.Staff.Applicants[0], Role.DeliClerk, Shift.Morning, null);
                var c2 = s.Staff.Hire(s.Staff.Applicants[0], Role.DeliClerk, Shift.Evening, null);
                Note("PLAYER: building deli, hired " + c.DisplayName + " and " + c2.DisplayName);
            }
            else Note("PLAYER: can't build deli (" + Economy.Money(s.Economy.Cash) + ")");
            var k = s.Staff.Hire(s.Staff.Applicants[0], Role.Cashier, Shift.Evening, null);
            Note("PLAYER: hired extra cashier " + k.DisplayName);
        }
        // Day 7: raise prices on grocery to see the effect.
        if (day == 7 && hour == 8 && done.Add("price"))
        {
            s.Slots[0].PriceMult = 1.15f;
            Note("PLAYER: grocery prices to 115%");
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
        var empties = new List<string>();
        for (int i = 0; i < store.SlotCount; i++)
        {
            var d = store.Slots[i];
            if (d == null || !d.Active) continue;
            foreach (var p in d.Products) if (p.Shelf.Total == 0) empties.Add(p.Def.Name + (p.Back.Total > 0 ? "(back " + p.Back.Total + ")" : "(none)"));
        }
        if (empties.Count > 0) Console.WriteLine("      empty shelves: " + string.Join(", ", empties));
    }

    static void PrintDay(DayLedger d)
    {
        Console.WriteLine(string.Format(
            "DAY {0}: customers {1}, items {2}, sales {3}, wholesale {4}, wages {5}, rent {6}, build {7}, PROFIT {8} | sat {9:0} rep {10:0.0} | walkouts {11}, complaints {12}/{13} resolved | waste {14} | cash {15}",
            d.Day, d.Customers, d.ItemsSold, Economy.Money(d.Sales), Economy.Money(d.Wholesale), Economy.Money(d.Wages),
            Economy.Money(d.Rent), Economy.Money(d.Construction), Economy.Money(d.Profit), d.AvgSatisfaction, store.Reputation,
            d.Walkouts, d.ComplaintsResolved, d.Complaints, Economy.Money(d.WasteValue), Economy.Money(store.Economy.Cash)));
        if (d.SatisfactionLost.Count > 0)
        {
            Console.WriteLine("      upset by: " + string.Join(", ", d.SatisfactionLost.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + (kv.Value / Math.Max(1f, d.Customers)).ToString("0.0") + "/cust")));
        }
        if (d.NotCarried.Count > 0)
            Console.WriteLine("      not carried: " + string.Join(", ", d.NotCarried.Select(kv => kv.Key + " " + kv.Value)));
        Console.WriteLine("      staff morale: " + string.Join(", ", store.Staff.All.Where(e => e != null).Select(e => e.DisplayName.Split(' ')[0] + "(" + e.Role.ToString().Substring(0, 3) + ") " + e.Morale.ToString("0"))));
    }

    static void PrintLog()
    {
        for (int i = 0; i < store.Log.Count; i++) Console.WriteLine("   " + store.Log[i]);
        store.Log.Clear();
    }

    /// <summary>Runs every office tab and floor panel through OnGUI to catch null refs.</summary>
    static void GuiPass()
    {
        var hud = store.GetComponent<Hud>();
        var onGui = typeof(Hud).GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Instance);
        var tabField = typeof(Hud).GetField("tab", BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            hud.Selected = store.Staff.All.FirstOrDefault(e => e != null && e.OnShift);
            hud.SelectedCustomer = store.Customers.FirstOrDefault(c => c != null);
            onGui.Invoke(hud, null);
            store.Avatar.Teleport(new Vector2Int(Layout.SlotX(1) + 1, 15));
            onGui.Invoke(hud, null);
            store.Avatar.Teleport(new Vector2Int(5, 29));
            store.Avatar.SitDown();
            for (int t = 0; t < 8; t++) { tabField.SetValue(hud, t); onGui.Invoke(hud, null); }
            store.Avatar.StandUp();
            hud.Selected = null;
            hud.SelectedCustomer = null;
        }
        catch (TargetInvocationException e)
        {
            Engine.Errors++;
            Console.WriteLine("EXCEPTION in GUI: " + e.InnerException);
        }
    }
}
