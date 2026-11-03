using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Grey-box IMGUI interface: top bar, alerts, message log, selection panels, build mode and the office
    /// desk screens. To be replaced by UI Toolkit later.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static bool PointerOverUI;
        public static Hud I;

        Store store;
        readonly List<Rect> rects = new List<Rect>();
        float S = 1f;

        public Employee Selected;
        public Customer SelectedCustomer;

        int tab = 1;
        Vector2 scroll;
        bool minimized;
        int hireRole, hireShift;
        Employee confirmFire;
        float confirmTime;
        string toast = "";
        float toastTime;
        float alertsHeight;

        readonly List<string> alerts = new List<string>();
        float alertTimer;

        GUIStyle small, bold, header, rich, center, centerShadow;
        public GUIStyle Small { get { return small; } }
        public GUIStyle Bold { get { return bold; } }
        public GUIStyle Header { get { return header; } }
        public GUIStyle Rich { get { return rich; } }

        static readonly string[] Tabs = { "Overview", "Build", "Products", "Staff", "Hire", "Delegation", "Expansion", "Finances" };
        const int BuildTab = 1;
        static readonly string[] RoleNames = { "Cashier", "Stocker", "Cleaner", "Baker", "Deli Clerk", "Asst. Mgr" };
        static readonly string[] ShiftNames = { "Morning", "Evening", "Night" };

        static GUILayoutOption W(float w) { return GUILayout.Width(w); }
        static string Money(float v) { return Economy.Money(v); }

        void Awake() { I = this; }

        void Start()
        {
            store = Store.I;
        }

        public void ShowTab(int t)
        {
            tab = t;
            scroll = Vector2.zero;
            minimized = false;
            if (t == BuildTab) BuildMode.I.Open();
            else BuildMode.I.Close();
        }

        public static void Toast(string text)
        {
            if (I == null || string.IsNullOrEmpty(text)) return;
            I.toast = text;
            I.toastTime = 3f;
            I.store.Message(text);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (store == null) store = Store.I;
            if (store == null) return;

            Vector2 m = GameInput.MousePosition;
            var gm = new Vector2(m.x / S, (Screen.height - m.y) / S);
            bool over = GUIUtility.hotControl != 0;
            for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(gm)) { over = true; break; }
            PointerOverUI = over;

            if (GameInput.Down(GameKey.Escape) && !BuildMode.I.Busy) { Selected = null; SelectedCustomer = null; }
            if (store.Avatar.AtDesk && tab == BuildTab && !BuildMode.I.Active) BuildMode.I.Open();
            HandleWorldClicks();

            alertTimer -= Time.unscaledDeltaTime;
            if (alertTimer <= 0f) { alertTimer = 0.5f; BuildAlerts(); }
        }

        void HandleWorldClicks()
        {
            if (PointerOverUI || store.Cam == null) return;
            var cam = store.Cam.GetComponent<Camera>();
            if (cam == null) return;
            if (BuildMode.I.Active && BuildMode.I.Busy) return;

            if (GameInput.MouseDown(0))
            {
                Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
                RaycastHit hit;
                Selected = null;
                SelectedCustomer = null;
                if (Physics.Raycast(ray, out hit, 500f))
                {
                    var e = hit.collider.GetComponentInParent<Employee>();
                    if (e != null) { Selected = e; BuildMode.I.Selected = null; return; }
                    var c = hit.collider.GetComponentInParent<Customer>();
                    if (c != null) { SelectedCustomer = c; BuildMode.I.Selected = null; return; }
                }
                if (!BuildMode.I.Active)
                {
                    var ground = new Plane(Vector3.up, Vector3.zero);
                    float enter;
                    if (ground.Raycast(ray, out enter))
                        BuildMode.I.Selected = store.FixtureAt(StoreGrid.TileAt(ray.GetPoint(enter)));
                }
            }

            if (GameInput.MouseDown(1) && !store.Avatar.AtDesk)
            {
                Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
                var ground = new Plane(Vector3.up, Vector3.zero);
                float enter;
                if (ground.Raycast(ray, out enter))
                    store.Avatar.WalkTo(StoreGrid.TileAt(ray.GetPoint(enter)));
            }
        }

        void BuildAlerts()
        {
            alerts.Clear();
            var clk = store.Clock;

            for (int i = 0; i < store.Complaints.Count; i++)
            {
                var c = store.Complaints[i];
                if (c == null || !c.Complaining) continue;
                int left = Mathf.CeilToInt((Customer.ComplaintLimit - c.ComplaintTime) * GameClock.GameMinutesPerSecond);
                string who = c.Responder != null && c.Responder != store.Avatar ? " (assistant on the way)" : "";
                alerts.Add("<color=#ff6655>! " + c.DisplayName + " wants the manager, " + left + " min left" + who + "</color>");
            }

            if (store.Lanes.Count == 0) alerts.Add("<color=#ff6655>No checkouts yet. Build some at your desk.</color>");
            else if (clk.IsOpen && store.OpenLaneCount == 0) alerts.Add("<color=#ff6655>No checkout is open!</color>");
            else if (clk.IsOpen && store.QueuePressure()) alerts.Add("<color=#ffcc55>Long checkout queues.</color>");

            int emptyFixtures = 0, carried = 0;
            for (int i = 0; i < store.Fixtures.Count; i++)
                if (store.Fixtures[i].HoldsProducts && store.Fixtures[i].Product == null) emptyFixtures++;
            for (int i = 0; i < store.Products.Count; i++) if (store.Products[i].Carried) carried++;
            if (store.Fixtures.Count > 0 && carried == 0) alerts.Add("<color=#ffcc55>No products on sale. Click a shelf at your desk to choose one.</color>");
            if (emptyFixtures > 0) alerts.Add("<color=#ffcc55>" + emptyFixtures + " shelves have no product chosen.</color>");

            if (store.CountFixtures("bakery_shelf") > 0)
            {
                if (store.CountFixtures("oven") == 0) alerts.Add("<color=#ffcc55>Bakery shelves need an oven.</color>");
                if (!store.Staff.AnyScheduled(Role.Baker)) alerts.Add("<color=#ffcc55>Nobody bakes: hire a baker.</color>");
            }
            if (store.CountFixtures("deli_counter") > 0 && !store.Staff.AnyScheduled(Role.DeliClerk))
                alerts.Add("<color=#ffcc55>Deli counters need a deli clerk.</color>");

            int needStocking = 0, soldOut = 0;
            for (int i = 0; i < store.Products.Count; i++)
            {
                var p = store.Products[i];
                if (!p.Carried || p.ShelfTotal > 0) continue;
                if (p.Back.Total > 0) needStocking++;
                else if (!p.Baked && p.OnOrder == 0) soldOut++;
            }
            if (needStocking > 0) alerts.Add("<color=#ffcc55>" + needStocking + " products sit in the back with empty shelves.</color>");
            if (soldOut > 0) alerts.Add("<color=#ff8866>" + soldOut + " products have no stock and nothing on order.</color>");

            if (store.Dirt.Spills.Count >= 3)
            {
                string extra = store.Staff.OnShiftCount(Role.Cleaner) == 0 ? " No cleaner on shift." : "";
                alerts.Add("<color=#ffcc55>" + store.Dirt.Spills.Count + " spills on the floor." + extra + "</color>");
            }
            if (clk.Hour >= 18 && store.OnOrderTotal == 0 && carried > 0)
                alerts.Add("<color=#ffcc55>Nothing on order for tomorrow's delivery.</color>");
            if (store.Economy.Cash < 0f) alerts.Add("<color=#ff6655>The store is in the red.</color>");
        }

        // ------------------------------------------------------------------ drawing

        void EnsureStyles()
        {
            if (small != null) return;
            small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, richText = true };
            bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = true };
            header = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, richText = true };
            rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            centerShadow = new GUIStyle(center);
            centerShadow.normal.textColor = Color.black;
        }

        public void Panel(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            GUI.Box(r, GUIContent.none);
            if (Event.current.type == EventType.Layout) rects.Add(r);
        }

        void OnGUI()
        {
            if (store == null) store = Store.I;
            if (store == null || store.Avatar == null) return;
            EnsureStyles();

            S = Mathf.Clamp(Screen.height / 1080f, 1f, 2.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));
            float sw = Screen.width / S, sh = Screen.height / S;
            if (Event.current.type == EventType.Layout) rects.Clear();

            DrawWorldLabels();
            DrawTopBar(sw);
            DrawAlerts(sw);
            DrawLog(sh);
            if (store.Avatar.AtDesk) DrawOffice(sw, sh);
            else DrawFloor(sw, sh);
            BuildMode.I.DrawFixturePanel(this, sw, sh, 48f + alertsHeight);
            DrawSelection(sw, sh);
            DrawToast(sw, sh);
        }

        void Label3D(Camera cam, Vector3 world, string text, float width)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            var r = new Rect(sp.x / S - width * 0.5f, (Screen.height - sp.y) / S - 11f, width, 22f);
            var shadow = new Rect(r.x + 1f, r.y + 1f, r.width, r.height);
            GUI.Label(shadow, StripColor(text), centerShadow);
            GUI.Label(r, text, center);
        }

        static string StripColor(string s)
        {
            int a = s.IndexOf('>');
            if (!s.StartsWith("<color") || a < 0) return s;
            return s.Substring(a + 1).Replace("</color>", "");
        }

        void DrawWorldLabels()
        {
            if (store.Cam == null) return;
            var cam = store.Cam.GetComponent<Camera>();
            if (cam == null) return;

            bool desk = store.Avatar.AtDesk;
            if (desk)
            {
                Label3D(cam, new Vector3(4f, 2f, 30f), "<color=#ffe08a>Office</color>", 100f);
                for (int i = 0; i < store.Fixtures.Count; i++)
                {
                    var f = store.Fixtures[i];
                    var pos = f.Center + new Vector3(0f, 2.3f, 0f);
                    string text;
                    if (f.HoldsProducts)
                    {
                        if (f.Product == null) text = "<color=#ffcc55>(empty)</color>";
                        else if (f.Stock.Total == 0) text = "<color=#ff7766>" + f.Product.Name + "</color>";
                        else text = f.Product.Name;
                    }
                    else text = "<color=#cfe0ff>" + f.Label + "</color>";
                    if (f == BuildMode.I.Selected) text = "<color=#9fe3ff>[ " + StripColor(text) + " ]</color>";
                    Label3D(cam, pos, text, 160f);
                }
            }
            else
            {
                for (int i = 0; i < store.Lanes.Count; i++)
                    Label3D(cam, store.Lanes[i].Fixture.Center + new Vector3(0f, 2.6f, 0f), (i + 1).ToString(), 30f);
            }
            for (int i = 0; i < store.Complaints.Count; i++)
            {
                var c = store.Complaints[i];
                if (c != null && c.Complaining)
                    Label3D(cam, c.transform.position + new Vector3(0f, 3.2f, 0f), "<color=#ff5544>! " + c.DisplayName + "</color>", 200f);
            }
            if (Selected != null && Selected.gameObject.activeInHierarchy)
                Label3D(cam, Selected.transform.position + new Vector3(0f, 2.5f, 0f), "<color=#9fe3ff>" + Selected.DisplayName + "</color>", 200f);
            if (SelectedCustomer != null)
                Label3D(cam, SelectedCustomer.transform.position + new Vector3(0f, 2.5f, 0f), "<color=#9fe3ff>" + SelectedCustomer.DisplayName + "</color>", 200f);
        }

        void DrawTopBar(float sw)
        {
            var clk = store.Clock;
            Panel(new Rect(0f, 0f, sw, 34f));
            GUILayout.BeginArea(new Rect(8f, 6f, sw - 16f, 26f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Day " + clk.Day + " (" + clk.Weekday + ")  " + clk.TimeText + "  "
                + (clk.IsOpen ? "<color=#7dff8a>OPEN</color>" : "<color=#ff8a7d>CLOSED</color>"), bold, W(220));
            GUILayout.Label("Cash " + Money(store.Economy.Cash), bold, W(140));
            int rep = Mathf.RoundToInt(store.Reputation);
            GUILayout.Label("Reputation " + rep + "  " + new string('*', Mathf.Clamp(rep / 20 + 1, 1, 5)), bold, W(170));
            GUILayout.Label("Sales today " + Money(store.Economy.Today.Sales), W(150));
            GUILayout.Label("In store " + store.Customers.Count, W(90));
            GUILayout.FlexibleSpace();
            if (clk.Paused) GUILayout.Label("<color=#ffcc55>PAUSED</color>", bold, W(70));
            for (int i = 0; i < GameClock.Speeds.Length; i++)
            {
                string label = i == 0 ? "||" : GameClock.Speeds[i] + "x";
                bool on = clk.SpeedIndex == i;
                if (GUILayout.Toggle(on, label, "Button", W(38)) && !on) clk.SetSpeed(i);
            }
            clk.ClosedBoost = GUILayout.Toggle(clk.ClosedBoost, " Fast nights", W(100));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawAlerts(float sw)
        {
            if (alerts.Count == 0) { alertsHeight = 0f; return; }
            float h = 12f + alerts.Count * 20f;
            alertsHeight = h;
            var r = new Rect(sw - 408f, 40f, 400f, h);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 8f));
            for (int i = 0; i < alerts.Count; i++) GUILayout.Label(alerts[i], rich);
            GUILayout.EndArea();
        }

        void DrawLog(float sh)
        {
            var r = new Rect(8f, sh - 178f, 560f, 170f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 10f));
            int start = Mathf.Max(0, store.Log.Count - 8);
            for (int i = start; i < store.Log.Count; i++) GUILayout.Label(store.Log[i], small);
            GUILayout.EndArea();
        }

        void DrawToast(float sw, float sh)
        {
            if (toastTime <= 0f) return;
            if (Event.current.type == EventType.Repaint) toastTime -= Time.unscaledDeltaTime;
            var r = new Rect(sw * 0.5f - 260f, 44f, 520f, 30f);
            Panel(r);
            GUI.Label(r, toast, center);
        }

        // ------------------------------------------------------------------ floor mode

        void DrawFloor(float sw, float sh)
        {
            var av = store.Avatar;
            float hintX = 576f, hintW = Mathf.Max(300f, sw - 576f - 416f);
            var hint = new Rect(hintX, sh - 40f, hintW, 32f);
            Panel(hint);
            GUI.Label(new Rect(hint.x + 8f, hint.y + 6f, hint.width - 16f, 22f),
                "WASD / right-click: walk   Q/E: rotate   Wheel: zoom   Click staff to talk   Space: pause   1-4: speed", small);

            if (av.InOffice)
            {
                var b = new Rect(hintX, sh - 82f, 220f, 34f);
                if (Event.current.type == EventType.Layout) rects.Add(b);
                if (GUI.Button(b, "Sit at desk (Tab)")) av.SitDown();
            }
        }

        // ------------------------------------------------------------------ selection

        void DrawSelection(float sw, float sh)
        {
            if (Selected != null && Selected.Leaving) Selected = null;
            if (Selected != null) DrawEmployeePanel(Selected, sw, sh);
            else if (SelectedCustomer != null) DrawCustomerPanel(SelectedCustomer, sw, sh);
        }

        void DrawEmployeePanel(Employee e, float sw, float sh)
        {
            var r = new Rect(sw - 408f, sh - 258f, 400f, 250f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 10f));
            GUILayout.Label(e.DisplayName + "  <size=12>" + Catalog.RoleName(e.Role) + ", " + StaffManager.ShiftName(e.Shift) + "</size>", header);
            GUILayout.Label("Status: " + (e.OnShift ? e.Status : e.Absent ? "Called in sick" : "Off duty"), rich);
            GUILayout.Label("Morale " + MoraleText(e.Morale) + "   Wage $" + e.Wage.ToString("0") + "/h (asked $" + e.AskingWage.ToString("0") + ")", rich);
            if (e.MoraleNote.Length > 0) GUILayout.Label("Morale " + e.MoraleNote, small);
            GUILayout.Label(SkillText(e), small);
            if (e.Role == Role.Stocker)
                GUILayout.Label("Restocks: " + (e.AssignedCategory != null ? e.AssignedCategory.Name : "everything"), small);
            if (e.OnShift) GUILayout.Label("Busy " + Mathf.RoundToInt(e.Utilisation * 100f) + "% of this shift" + (e.WarnedThisShift ? ", warned" : ""), small);

            var av = store.Avatar;
            bool near = e.OnShift && !av.AtDesk && av.DistanceTo(e.transform.position) <= ManagerAvatar.TalkRange;
            GUILayout.BeginHorizontal();
            GUI.enabled = near;
            if (GUILayout.Button("Praise", W(110))) Toast(e.Praise());
            if (GUILayout.Button("Warn", W(110))) Toast(e.Warn());
            GUI.enabled = true;
            if (GUILayout.Button("Close", W(80))) Selected = null;
            GUILayout.EndHorizontal();
            if (!near)
                GUILayout.Label(av.AtDesk ? "Get up from the desk and walk over to talk." : e.OnShift ? "Walk within 3 m to talk." : "Not at work right now.", small);
            GUILayout.EndArea();
        }

        void DrawCustomerPanel(Customer c, float sw, float sh)
        {
            var r = new Rect(sw - 408f, sh - 150f, 400f, 142f);
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 10f));
            GUILayout.Label(c.DisplayName + "  <size=12>customer</size>", header);
            GUILayout.Label("Status: " + c.Status, rich);
            GUILayout.Label("Satisfaction " + MoraleText(c.Satisfaction) + "   Cart " + c.Items + " items, " + Money(c.CartValue), rich);
            if (c.Complaining) GUILayout.Label("<color=#ff7766>Complaint: " + c.ComplaintReason + "</color>", small);
            if (GUILayout.Button("Close", W(80))) SelectedCustomer = null;
            GUILayout.EndArea();
        }

        static string MoraleText(float v)
        {
            string col = v >= 60f ? "#9dff9d" : v >= 35f ? "#ffd166" : "#ff7766";
            return "<color=" + col + ">" + Mathf.RoundToInt(v) + "</color>";
        }

        static string SkillText(Employee e)
        {
            string s = "Speed " + e.Speed.ToString("0.#") + "  Service " + e.Service.ToString("0.#")
                + "  Reliability " + e.Reliability.ToString("0.#") + "  Leadership " + e.Leadership.ToString("0.#");
            if (e.Specialty != Specialty.None) s += "  [" + e.Specialty + "]";
            return s;
        }

        // ------------------------------------------------------------------ office mode

        void DrawOffice(float sw, float sh)
        {
            var hint = new Rect(576f, sh - 40f, Mathf.Max(300f, sw - 576f - 416f), 32f);
            Panel(hint);
            string hintText = tab == BuildTab
                ? "Build: pick a fixture, click to place, R rotates, right-click cancels. Click a shelf to choose its product.  WASD/Q/E/wheel: camera"
                : "At your desk.  WASD: move camera   Q/E: rotate   Wheel: zoom   Click a shelf to see it   Tab: get up";
            GUI.Label(new Rect(hint.x + 8f, hint.y + 6f, hint.width - 16f, 22f), hintText, small);

            // Tab strip (always visible at the desk)
            float tw = Mathf.Min(760f, sw - 640f);
            var strip = new Rect(sw * 0.5f - tw * 0.5f, 40f, tw, 34f);
            if (tab == BuildTab) strip = new Rect(316f, 40f, Mathf.Min(760f, sw - 316f - 416f), 34f);
            Panel(strip);
            GUILayout.BeginArea(new Rect(strip.x + 4f, strip.y + 4f, strip.width - 8f, 28f));
            GUILayout.BeginHorizontal();
            int newTab = GUILayout.Toolbar(tab, Tabs);
            if (newTab != tab) ShowTab(newTab);
            if (tab != BuildTab && GUILayout.Button(minimized ? "+" : "_", W(28))) minimized = !minimized;
            if (GUILayout.Button("Get up", W(70))) { BuildMode.I.Close(); store.Avatar.StandUp(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (tab == BuildTab)
            {
                BuildMode.I.DrawPalette(this, sw, sh);
                return;
            }
            if (minimized) return;

            float w = Mathf.Min(900f, sw - 16f);
            var r = new Rect(sw * 0.5f - w * 0.5f, 80f, w, Mathf.Max(300f, sh - 80f - 190f));
            Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));
            scroll = GUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: OverviewTab(); break;
                case 2: ProductsTab(); break;
                case 3: StaffTab(); break;
                case 4: HireTab(); break;
                case 5: DelegationTab(); break;
                case 6: ExpansionTab(); break;
                default: FinancesTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void OverviewTab()
        {
            var t = store.Economy.Today;
            GUILayout.Label("Today so far", header);
            GUILayout.Label("Customers served " + t.Customers + "   Walk-outs " + t.Walkouts
                + "   Complaints " + t.ComplaintsResolved + " resolved of " + t.Complaints, rich);
            GUILayout.Label("Sales " + Money(t.Sales) + "   Costs " + Money(t.Costs) + "   Profit " + Money(t.Profit), rich);
            GUILayout.Label("Average satisfaction " + (t.Customers > 0 ? MoraleText(t.AvgSatisfaction) : "-")
                + "   (reputation moves towards this each night)", rich);
            GUILayout.Label("You sell " + Mathf.RoundToInt(store.Variety * 100f) + "% of what customers ask for. Expected traffic: about "
                + Mathf.RoundToInt(store.ExpectedCustomersPerDay) + " customers a day.", rich);

            GUILayout.Space(8f);
            GUILayout.Label("What upset customers today", header);
            if (t.SatisfactionLost.Count == 0) GUILayout.Label("Nothing yet.", small);
            else
            {
                var losses = new List<KeyValuePair<string, float>>(t.SatisfactionLost);
                losses.Sort((a, b) => b.Value.CompareTo(a.Value));
                float total = 0f;
                for (int i = 0; i < losses.Count; i++) total += losses[i].Value;
                for (int i = 0; i < losses.Count && i < 6; i++)
                    GUILayout.Label(losses[i].Key + ": " + Mathf.RoundToInt(losses[i].Value / Mathf.Max(1f, total) * 100f) + "%", rich);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Staff", header);
            int onShift = 0;
            var absent = new List<string>();
            for (int i = 0; i < store.Staff.All.Count; i++)
            {
                var e = store.Staff.All[i];
                if (e == null) continue;
                if (e.OnShift) onShift++;
                if (e.Absent) absent.Add(e.DisplayName);
            }
            GUILayout.Label(onShift + " of " + store.Staff.All.Count + " at work now."
                + (absent.Count > 0 ? "  Off sick: " + string.Join(", ", absent.ToArray()) : ""), rich);

            GUILayout.Space(8f);
            GUILayout.Label("Asked for but not sold here (today)", header);
            if (t.NotCarried.Count == 0) GUILayout.Label("Nothing yet.", small);
            foreach (var kv in t.NotCarried)
                GUILayout.Label(kv.Key + ": " + kv.Value + " times", rich);

            GUILayout.Space(8f);
            GUILayout.Label("Empty shelves", header);
            int shown = 0;
            for (int i = 0; i < store.Products.Count; i++)
            {
                var p = store.Products[i];
                if (!p.Carried || p.ShelfTotal > 0) continue;
                string why = p.Baked ? "needs baking" : p.Back.Total > 0 ? p.Back.Total + " in the back, needs stocking"
                    : p.OnOrder > 0 ? p.OnOrder + " on order" : "<color=#ff7766>no stock, order more</color>";
                GUILayout.Label(p.Name + ": " + why, rich);
                shown++;
            }
            if (shown == 0) GUILayout.Label("None.", small);
            GUILayout.Label("Back room " + store.BackStockTotal + " / " + store.BackCapacity + " units.  On order: " + store.OnOrderTotal + " units.", small);
        }

        void ProductsTab()
        {
            GUILayout.Label("Products", header);
            GUILayout.Label("Choose products by clicking shelves in Build. Par = how much you want on hand (shelves + back + ordered). Orders arrive with the 06:00 truck, or within an hour by express (+25%).", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Order everything to par", W(200)))
            {
                int n = store.OrderAllToPar(0f);
                Toast(n > 0 ? "Ordered " + n + " products up to par." : "Everything on sale is at par.");
            }
            GUI.enabled = store.OnOrderTotal > 0 && !store.ExpressPending;
            if (GUILayout.Button(store.ExpressPending ? "Express on its way" : "Express delivery now (+25%)", W(220)))
                store.OrderExpress();
            GUI.enabled = true;
            GUILayout.Label("On order " + store.OnOrderTotal + " units (" + Money(store.OnOrderCost) + ").  Back room "
                + store.BackStockTotal + " / " + store.BackCapacity, small);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            for (int c = 0; c < store.Categories.Count; c++)
            {
                var cat = store.Categories[c];
                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                GUILayout.Label(cat.Name, header, W(160));
                GUILayout.Label("Prices", W(45));
                if (GUILayout.Button("-5%", W(42))) cat.PriceMult = Mathf.Max(0.8f, cat.PriceMult - 0.05f);
                GUILayout.Label(Mathf.RoundToInt(cat.PriceMult * 100f) + "%", bold, W(44));
                if (GUILayout.Button("+5%", W(42))) cat.PriceMult = Mathf.Min(1.5f, cat.PriceMult + 0.05f);
                GUILayout.Label("Sales today " + Money(cat.SalesToday) + (cat.Def.Baked ? "   (baked on site)" : ""), W(260));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("<b>Product</b>", rich, W(110));
                GUILayout.Label("<b>Shelves</b>", rich, W(60));
                GUILayout.Label("<b>On shelf</b>", rich, W(70));
                GUILayout.Label("<b>Back</b>", rich, W(50));
                GUILayout.Label("<b>Ordered</b>", rich, W(55));
                GUILayout.Label("<b>Par</b>", rich, W(110));
                GUILayout.Label("<b>Sold</b>", rich, W(40));
                GUILayout.Label("<b>Missed</b>", rich, W(50));
                GUILayout.Label("<b>Waste</b>", rich, W(45));
                GUILayout.EndHorizontal();

                for (int i = 0; i < cat.Products.Count; i++)
                {
                    var p = cat.Products[i];
                    int cs = p.Def.CaseSize;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.Carried ? p.Name : "<color=#999999>" + p.Name + "</color>", rich, W(110));
                    GUILayout.Label(p.Fixtures.Count.ToString(), W(60));
                    if (p.Carried)
                    {
                        float fill = p.ShelfFill;
                        string col = fill > 0.5f ? "#9dff9d" : fill > 0.2f ? "#ffd166" : "#ff7766";
                        GUILayout.Label("<color=" + col + ">" + p.ShelfTotal + "/" + p.ShelfCap + "</color>", rich, W(70));
                    }
                    else GUILayout.Label("-", W(70));
                    if (p.Baked)
                    {
                        GUILayout.Label("-", W(50));
                        GUILayout.Label("-", W(55));
                        GUILayout.Label("-", W(110));
                    }
                    else
                    {
                        GUILayout.Label(p.Back.Total.ToString(), W(50));
                        GUILayout.Label(p.OnOrder.ToString(), W(55));
                        if (GUILayout.Button("-", W(24))) p.Par = Mathf.Max(cs, p.Par - cs);
                        GUILayout.Label(p.Par.ToString(), W(52));
                        if (GUILayout.Button("+", W(24))) p.Par += cs;
                    }
                    GUILayout.Label(p.SoldToday.ToString(), W(40));
                    GUILayout.Label(p.MissedToday.ToString(), W(50));
                    GUILayout.Label(p.WastedLastNight.ToString(), W(45));
                    if (!p.Baked)
                    {
                        if (GUILayout.Button("+1 case", W(64))) store.Order(p, cs);
                        if (GUILayout.Button("+5", W(34))) store.Order(p, cs * 5);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
        }

        void StaffTab()
        {
            var staff = store.Staff;
            float wages = 0f;
            for (int i = 0; i < staff.All.Count; i++)
                if (staff.All[i] != null) wages += staff.All[i].Wage * 8f * (staff.All[i].Shift == Shift.Night ? 1.25f : 1f);
            GUILayout.Label(staff.All.Count + " staff. Wages about " + Money(wages) + " a day.", rich);
            GUILayout.Label("Click Role, Shift or Restocks to change them.", small);
            GUILayout.Space(4f);

            var list = staff.All.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == null) continue;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(e.DisplayName, W(150))) Selected = e;
                if (GUILayout.Button(Catalog.RoleName(e.Role), W(110))) staff.SetRole(e, NextRole(e));
                if (GUILayout.Button(ShiftNames[(int)e.Shift], W(80))) staff.SetShift(e, (Shift)(((int)e.Shift + 1) % 3));
                bool stocker = e.Role == Role.Stocker;
                GUI.enabled = stocker;
                if (GUILayout.Button(stocker ? (e.AssignedCategory != null ? e.AssignedCategory.Name : "Everything") : "-", W(120)))
                    staff.SetCategory(e, NextCategory(e));
                GUI.enabled = true;
                if (GUILayout.Button("Raise", W(54))) { e.GiveRaise(); Toast(e.DisplayName + " now earns $" + e.Wage.ToString("0") + "/h."); }
                bool confirming = confirmFire == e && Time.unscaledTime - confirmTime < 3f;
                if (GUILayout.Button(confirming ? "Sure?" : "Fire", W(54)))
                {
                    if (confirming) { staff.Fire(e); confirmFire = null; if (Selected == e) Selected = null; }
                    else { confirmFire = e; confirmTime = Time.unscaledTime; }
                }
                GUILayout.EndHorizontal();
                string status = e.OnShift ? e.Status : e.Absent ? "Off sick" : "Off duty";
                GUILayout.Label("   Morale " + MoraleText(e.Morale) + (e.MoraleNote.Length > 0 ? " (" + e.MoraleNote + ")" : "")
                    + "   $" + e.Wage.ToString("0") + "/h   " + SkillText(e) + "   <i>" + status + "</i>", small);
                GUILayout.Space(2f);
            }
        }

        Role NextRole(Employee e)
        {
            var am = store.Staff.Assistant;
            Role r = e.Role;
            for (int i = 0; i < 6; i++)
            {
                r = (Role)(((int)r + 1) % 6);
                if (r == Role.AssistantManager && am != null && am != e) continue;
                return r;
            }
            return e.Role;
        }

        Category NextCategory(Employee e)
        {
            var options = new List<Category> { null };
            for (int i = 0; i < store.Categories.Count; i++)
            {
                var c = store.Categories[i];
                if (c.Def.Baked || c.Def.Fixture == "deli_counter") continue;
                options.Add(c);
            }
            int idx = options.IndexOf(e.AssignedCategory);
            return options[(idx + 1) % options.Count];
        }

        void HireTab()
        {
            var staff = store.Staff;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hire as", W(60));
            hireRole = GUILayout.Toolbar(hireRole, RoleNames);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shift", W(60));
            hireShift = GUILayout.Toolbar(hireShift, ShiftNames, W(330));
            GUILayout.EndHorizontal();
            bool amTaken = (Role)hireRole == Role.AssistantManager && staff.Assistant != null;
            if (amTaken) GUILayout.Label("<color=#ffcc55>You already have an assistant manager.</color>", rich);
            GUILayout.Label("Leadership only matters for an assistant manager. Bakers work fast with Baking, deli clerks with Butchery. Night shift pays 25% more.", small);
            GUILayout.Space(6f);

            var list = staff.Applicants.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var a = list[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(a.Name, bold, W(170));
                GUILayout.Label("Speed " + a.Speed + "  Service " + a.Service + "  Reliability " + a.Reliability + "  Leadership " + a.Leadership, small, W(330));
                GUILayout.Label(a.Specialty != Specialty.None ? "[" + a.Specialty + "]" : "", small, W(80));
                GUILayout.Label("$" + a.AskingWage.ToString("0") + "/h", W(60));
                GUI.enabled = !amTaken;
                if (GUILayout.Button("Hire", W(70)))
                {
                    var e = staff.Hire(a, (Role)hireRole, (Shift)hireShift, null);
                    Toast("Hired " + e.DisplayName + " as " + Catalog.RoleName(e.Role) + " (" + ShiftNames[(int)e.Shift] + ").");
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("New applicants come in every day.", small);
        }

        void DelegationTab()
        {
            var am = store.Staff.Assistant;
            var del = store.Delegation;
            if (am == null)
            {
                GUILayout.Label("You have no assistant manager.", header);
                GUILayout.Label("Hire one in the Hire tab (pick Asst. Mgr), or change someone's role in the Staff tab. Leadership decides how well they do.", rich);
            }
            else
            {
                GUILayout.Label("Assistant manager: " + am.DisplayName, header);
                GUILayout.Label(StaffManager.ShiftName(am.Shift) + "   Leadership " + am.Leadership.ToString("0.#")
                    + "   " + (am.OnShift ? "At work: " + am.Status : "Not at work"), rich);
                GUILayout.Label("They only do these jobs while on shift.", small);
            }
            GUILayout.Space(8f);
            del.Ordering = GUILayout.Toggle(del.Ordering, " Ordering: order everything on sale to par an hour before their shift ends.");
            del.Complaints = GUILayout.Toggle(del.Complaints, " Complaints: go and calm down customers who want the manager.");
            del.Checkouts = GUILayout.Toggle(del.Checkouts, " Checkouts: open a till when queues get long.");
            del.Morale = GUILayout.Toggle(del.Morale, " Staff morale: chat with staff while walking the floor.");
            GUILayout.Space(8f);
            GUILayout.Label("Lower leadership means less accurate orders and less effective complaint handling. Staff near the assistant work a little faster.", small);
        }

        void ExpansionTab()
        {
            GUILayout.Label("The shop floor is " + store.Grid.W + " m wide (" + store.SlotCount + " of " + Layout.MaxSlots + " sections).", header);
            GUILayout.Label("Back room holds " + store.BackCapacity + " units. Rent is " + Money(store.Rent) + " a day.", rich);
            GUILayout.Space(8f);
            if (store.ExpansionPending)
                GUILayout.Label("<color=#ffd24d>Expansion under construction. Ready at 06:00.</color>", rich);
            else if (store.SlotCount >= Layout.MaxSlots)
                GUILayout.Label("The store can't get any bigger.", rich);
            else
            {
                GUILayout.Label("Next expansion: +12 m of shop floor, +2,000 back-room units, rent +$300/day.", rich);
                GUI.enabled = store.CanExpand;
                if (GUILayout.Button("Expand for " + Money(store.ExpansionCost), W(240), GUILayout.Height(34f)))
                    store.Expand();
                GUI.enabled = true;
            }
        }

        void FinancesTab()
        {
            GUILayout.BeginHorizontal();
            string[] heads = { "Day", "Sales", "Wholesale", "Wages", "Rent", "Building", "Profit", "Customers", "Satisf.", "Waste" };
            for (int i = 0; i < heads.Length; i++) GUILayout.Label("<b>" + heads[i] + "</b>", rich, W(i == 0 ? 50f : 80f));
            GUILayout.EndHorizontal();

            var rows = new List<DayLedger>();
            var hist = store.Economy.History;
            for (int i = Mathf.Max(0, hist.Count - 7); i < hist.Count; i++) rows.Add(hist[i]);
            rows.Add(store.Economy.Today);
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var d = rows[i];
                bool today = d == store.Economy.Today;
                GUILayout.BeginHorizontal();
                GUILayout.Label(today ? "Today" : d.Day.ToString(), W(50));
                GUILayout.Label(Money(d.Sales), W(80));
                GUILayout.Label(Money(d.Wholesale), W(80));
                GUILayout.Label(Money(d.Wages), W(80));
                GUILayout.Label(Money(d.Rent), W(80));
                GUILayout.Label(Money(d.Construction), W(80));
                string col = d.Profit >= 0f ? "#9dff9d" : "#ff7766";
                GUILayout.Label("<color=" + col + ">" + Money(d.Profit) + "</color>", rich, W(80));
                GUILayout.Label(d.Customers.ToString(), W(80));
                GUILayout.Label(d.Customers > 0 ? d.AvgSatisfaction.ToString("0") : "-", W(80));
                GUILayout.Label(Money(d.WasteValue), W(80));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8f);
            GUILayout.Label("Cash " + Money(store.Economy.Cash) + ".  Building includes fixtures bought (and refunds when sold). Waste is stock that expired.", small);
        }
    }
}
