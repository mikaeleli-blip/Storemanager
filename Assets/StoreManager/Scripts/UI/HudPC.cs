using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// The office computer: sitting at the desk zooms onto the monitor and opens a full-screen desktop with
    /// apps (shop, staff, hiring, stats...). The Build app leaves the screen for the overhead camera.
    /// </summary>
    public partial class Hud
    {
        public enum App { Shop, Bills, Products, Licences, Staff, Roster, Hiring, Assistant, Stats, Expansion, Inbox }
        public const int AppCount = 11;
        static readonly string[] AppNames = { "Shop", "Bills", "Products", "Licences", "Staff", "Roster", "Hiring", "Assistant", "Stats", "Expansion", "Inbox" };
        static readonly string[] AppTitles =
        {
            "Ordering: warehouse and suppliers", "Bills (reikningar)", "Products & prices", "Licences", "Staff: profiles, reviews, discipline",
            "Roster (vaktaplan)", "Hiring and interviews", "Assistant manager", "Statistics", "Expansion", "Inbox",
        };
        static readonly Color[] AppColors =
        {
            Mats.Hex("#2f9e6a"), Mats.Hex("#a8432f"), Mats.Hex("#d08a2c"), Mats.Hex("#b8913a"), Mats.Hex("#3f7fd0"), Mats.Hex("#3a9a8c"), Mats.Hex("#6a5acd"),
            Mats.Hex("#c0566b"), Mats.Hex("#2a9db0"), Mats.Hex("#7a8a3a"), Mats.Hex("#5d6b7d"),
        };
        static readonly Color BuildColor = Mats.Hex("#d9573b");
        static readonly Vector2[] AppSizes =
        {
            new Vector2(1100f, 740f), new Vector2(1000f, 680f), new Vector2(1000f, 680f), new Vector2(1020f, 660f), new Vector2(1140f, 720f),
            new Vector2(1020f, 720f), new Vector2(1140f, 740f), new Vector2(720f, 460f), new Vector2(980f, 660f), new Vector2(640f, 340f),
            new Vector2(720f, 560f),
        };

        class PcWindow
        {
            public App App;
            public Rect Rect;
            public Vector2 Scroll, Scroll2;
            public bool Minimized, Closing;
        }

        /// <summary>At the desk: true while looking at the computer, false in the Build view.</summary>
        public bool OnPC;
        readonly List<PcWindow> windows = new List<PcWindow>();
        // Cart changes wait until the window has finished drawing, so the layout doesn't change mid-frame.
        readonly List<KeyValuePair<Product, int>> cartOps = new List<KeyValuePair<Product, int>>();
        bool clearCart;
        int statsPage;
        float pcAlpha;
        Texture2D wallpaper, tile;
        Texture2D[] appIcons;
        Texture2D buildIcon;
        GUIStyle winStyle, iconLabel, iconShadow, winTitle, watermark, cardTitle, taskButton;

        // ------------------------------------------------------------------ desk views

        public void OpenPC()
        {
            var s = Store.I;
            OnPC = true;
            if (BuildMode.I != null) BuildMode.I.Close();
            Selected = null;
            SelectedCustomer = null;
            if (s != null && s.Cam != null)
            {
                s.Cam.Locked = true;
                s.Cam.LockPosition = s.MonitorView;
                s.Cam.LockRotation = Quaternion.identity;
            }
        }

        public void OpenBuildView()
        {
            var s = Store.I;
            OnPC = false;
            if (s != null && s.Cam != null)
            {
                s.Cam.Locked = false;
                s.Cam.FreeMode = true;
                s.Cam.Distance = Mathf.Max(s.Cam.Distance, 60f);
                s.Cam.Focus = new Vector3(s.Grid.W * 0.5f, 0f, 16f);
            }
            if (BuildMode.I != null) BuildMode.I.Open();
        }

        public void LeaveDesk()
        {
            var s = Store.I;
            OnPC = false;
            if (BuildMode.I != null) BuildMode.I.Close();
            if (s != null && s.Cam != null)
            {
                s.Cam.Locked = false;
                s.Cam.FreeMode = false;
                s.Cam.Distance = Mathf.Min(s.Cam.Distance, 34f);
            }
        }

        public void OpenApp(App app)
        {
            PcWindow w = null;
            for (int i = 0; i < windows.Count; i++) if (windows[i].App == app && !windows[i].Closing) w = windows[i];
            if (w == null)
            {
                Vector2 size = AppSizes[(int)app];
                int k = windows.Count % 6;
                w = new PcWindow { App = app, Rect = new Rect(250f + 34f * k, 30f + 30f * k, size.x, size.y) };
            }
            else windows.Remove(w);
            w.Minimized = false;
            windows.Add(w);   // last = front
            GUI.FocusWindow(1000 + (int)app);
        }

        void CloseTopWindow()
        {
            for (int i = windows.Count - 1; i >= 0; i--)
                if (!windows[i].Minimized && !windows[i].Closing) { windows[i].Closing = true; return; }
        }

        bool PcVisible { get { return store.Cam == null || store.Cam.LockBlend > 0.75f; } }

        // ------------------------------------------------------------------ textures and styles

        static readonly string[][] IconArt =
        {
            new[] { "............", "##..........", ".#..........", ".##########.", ".#++++++++#.", ".#++++++++#.",
                    "..#++++++#..", "..########..", "..#.........", "..#########.", "...##...##..", "...##...##.." },
            new[] { "..########..", "..#++++++#..", "..#+####+#..", "..#++++++#..", "..#+###++#..", "..#++++++#..",
                    "..#+####+#..", "..#++++++#..", "..#++oooo#..", "..#++++++#..", "..##+##+##..", "..#..#..#..." },
            new[] { "............", "............", "...#########", "..#++++++++#", ".#++#+#+##+#", "#+o+#+#+##+#",
                    ".#++#+#+##+#", "..#++++++++#", "...#########", "............", "............", "............" },
            new[] { "#########...", "#+++++++#...", "#+#####+#...", "#+++++++#...", "#+####++#...", "#+++++++#...",
                    "#+###+####..", "#++++#oooo#.", "######oooo#.", ".....#oooo#.", "......####..", "......#..#.." },
            new[] { "............", "..##....##..", ".####..####.", ".####..####.", "..##....##..", "............",
                    ".####..####.", "######.#####", "######.#####", "######.#####", "............", "............" },
            new[] { "..#....#....", ".##########.", ".##########.", ".#++#++#++#.", ".#++#++#++#.", ".##########.",
                    ".#++#++#++#.", ".#++#++#++#.", ".##########.", ".#++#++#oo#.", ".#++#++#oo#.", ".##########." },
            new[] { "............", "...###......", "..#####.....", "..#####..#..", "...###...#..", "........###.",
                    ".######..#..", "########.#..", "########....", "########....", "............", "............" },
            new[] { "............", "....####....", "...######...", "...######...", "....####....", "............",
                    "..###oo###..", ".####oo####.", ".#####o####.", ".####oo####.", "............", "............" },
            new[] { "............", "#...........", "#.......##..", "#.......##..", "#....##.##..", "#....##.##..",
                    "#.##.##.##..", "#.##.##.##..", "#.##.##.##..", "#.##.##.##..", "###########.", "............" },
            new[] { "............", "#####..#....", "#+++#..##...", "#+++#######.", "#+++#######.", "#+++#..##...",
                    "#####..#....", "............", "############", "#++++++++++#", "#++#++#++#+#", "############" },
            new[] { "............", "............", "############", "##++++++++##", "#+#++++++#+#", "#++#++++#++#",
                    "#+++####+++#", "#++++++++++#", "#++++++++++#", "############", "............", "............" },
        };
        static readonly string[] BuildArt =
        {
            "............", ".########...", ".########...", ".########...", "....##......", "....##......",
            "....##......", "....##......", "....##......", "....##......", "....##......", "............",
        };

        static Texture2D PixelIcon(string[] art)
        {
            var t = new Texture2D(12, 12);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 12; y++)
                for (int x = 0; x < 12; x++)
                {
                    char c = art[11 - y][x];
                    Color col = c == '#' ? Color.white
                        : c == '+' ? new Color(1f, 1f, 1f, 0.45f)
                        : c == 'o' ? new Color(1f, 0.85f, 0.35f, 1f)
                        : new Color(0f, 0f, 0f, 0f);
                    t.SetPixel(x, y, col);
                }
            t.Apply();
            return t;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsurePcAssets()
        {
            if (wallpaper != null) return;

            wallpaper = new Texture2D(2, 64);
            wallpaper.wrapMode = TextureWrapMode.Clamp;
            Color top = Mats.Hex("#14324a"), bottom = Mats.Hex("#2a6b68");
            for (int y = 0; y < 64; y++)
            {
                Color c = Color.Lerp(bottom, top, y / 63f);
                wallpaper.SetPixel(0, y, c);
                wallpaper.SetPixel(1, y, c);
            }
            wallpaper.Apply();

            // Rounded tile, white, tinted per app with GUI.color.
            tile = new Texture2D(16, 16);
            tile.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(3.5f - x, x - 11.5f)), dy = Mathf.Max(0f, Mathf.Max(3.5f - y, y - 11.5f));
                    float a = Mathf.Clamp01(4.2f - Mathf.Sqrt(dx * dx + dy * dy));
                    float shade = 1f - 0.18f * (1f - y / 15f);
                    tile.SetPixel(x, y, new Color(shade, shade, shade, a));
                }
            tile.Apply();

            appIcons = new Texture2D[AppCount];
            for (int i = 0; i < AppCount; i++) appIcons[i] = PixelIcon(IconArt[i]);
            buildIcon = PixelIcon(BuildArt);

            winStyle = new GUIStyle();
            winStyle.normal.background = Solid(Mats.Hex("#1d232b"));
            winStyle.onNormal.background = winStyle.normal.background;
            winStyle.padding = new RectOffset(0, 0, 0, 0);

            iconLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13, fontStyle = FontStyle.Bold };
            iconLabel.normal.textColor = Color.white;
            iconShadow = new GUIStyle(iconLabel);
            iconShadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            winTitle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            winTitle.normal.textColor = Color.white;
            watermark = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            watermark.normal.textColor = new Color(1f, 1f, 1f, 0.08f);
            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            cardTitle.normal.textColor = new Color(0.85f, 0.95f, 1f);
            taskButton = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
        }

        // ------------------------------------------------------------------ desktop

        void DrawPC(float sw, float sh)
        {
            EnsurePcAssets();
            for (int i = windows.Count - 1; i >= 0; i--) if (windows[i].Closing) windows.RemoveAt(i);

            var full = new Rect(0f, 0f, sw, sh);
            if (Event.current.type == EventType.Layout) rects.Add(full);
            pcAlpha = store.Cam != null ? Mathf.InverseLerp(0.75f, 1f, store.Cam.LockBlend) : 1f;
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);

            GUI.DrawTexture(full, wallpaper);
            GUI.Label(new Rect(sw - 760f, sh - 150f, 400f, 90f), "StoreOS", watermark);

            DrawDesktopIcons();
            DrawWidgets(sw, sh);
            DrawTaskbar(sw, sh);

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w.Minimized || w.Closing) continue;
                w.Rect = GUI.Window(1000 + (int)w.App, w.Rect, DrawWindow, GUIContent.none, winStyle);
                w.Rect.x = Mathf.Clamp(w.Rect.x, -w.Rect.width + 120f, sw - 120f);
                w.Rect.y = Mathf.Clamp(w.Rect.y, 0f, sh - 44f - 30f);
            }
            GUI.color = Color.white;
        }

        bool DesktopIcon(Rect cell, Texture2D icon, Color col, string label)
        {
            bool hover = cell.Contains(Event.current.mousePosition);
            if (hover) { GUI.color = new Color(1f, 1f, 1f, 0.12f * pcAlpha); GUI.DrawTexture(cell, tile); }
            var t = new Rect(cell.x + (cell.width - 64f) * 0.5f, cell.y + 6f, 64f, 64f);
            GUI.color = new Color(col.r, col.g, col.b, pcAlpha);
            GUI.DrawTexture(t, tile);
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);
            GUI.DrawTexture(new Rect(t.x + 8f, t.y + 8f, 48f, 48f), icon);
            var lr = new Rect(cell.x - 6f, cell.y + 74f, cell.width + 12f, 22f);
            GUI.Label(new Rect(lr.x + 1f, lr.y + 1f, lr.width, lr.height), label, iconShadow);
            GUI.Label(lr, label, iconLabel);
            return GUI.Button(cell, GUIContent.none, GUIStyle.none);
        }

        void DrawDesktopIcons()
        {
            const float cw = 96f, ch = 104f;
            int n = 0;
            for (int i = 0; i <= AppCount; i++)
            {
                int col = n / 5, row = n % 5;
                var cell = new Rect(22f + col * (cw + 10f), 22f + row * ch, cw, ch - 4f);
                n++;
                if (i == AppCount)
                {
                    if (DesktopIcon(cell, buildIcon, BuildColor, "Build")) OpenBuildView();
                }
                else if (DesktopIcon(cell, appIcons[i], AppColors[i], AppNames[i])) OpenApp((App)i);
            }
        }

        void Card(Rect r)
        {
            GUI.color = new Color(0.07f, 0.1f, 0.13f, 0.72f * pcAlpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);
        }

        void DrawWidgets(float sw, float sh)
        {
            var clk = store.Clock;
            var t = store.Economy.Today;
            float x = sw - 352f, w = 336f;

            var a = new Rect(x, 16f, w, 228f);
            Card(a);
            GUILayout.BeginArea(new Rect(a.x + 12f, a.y + 8f, a.width - 24f, a.height - 12f));
            GUILayout.Label("TODAY  ·  Day " + clk.Day + ", " + clk.Weekday + " " + clk.TimeText + "  "
                + (clk.IsOpen ? "<color=#7dff8a>open</color>" : "<color=#ff8a7d>closed</color>"), cardTitle);
            GUILayout.Label("Sales " + Money(t.Sales) + "   Profit " + (t.Profit >= 0f ? "<color=#9dff9d>" : "<color=#ff7766>") + Money(t.Profit) + "</color>", rich);
            GUILayout.Label("Customers " + t.Customers + "   In store now " + store.Customers.Count, rich);
            GUILayout.Label("Satisfaction " + (t.Customers > 0 ? MoraleText(t.AvgSatisfaction) : "-") + "   Reputation " + MoraleText(store.Reputation), rich);
            float dueSoon = store.DueWithin(2);
            GUILayout.Label("Cash <b>" + Money(store.Economy.Cash) + "</b>" + (dueSoon > 0f ? "   Bills due soon " + (store.OverdueCount > 0 ? "<color=#ff6655>" : "<color=#ffcc55>") + Money(dueSoon) + "</color>" : ""), rich);
            GUILayout.Space(4f);
            GUILayout.Label(LevelText(), bold);
            GUILayout.EndArea();
            float frac = store.Level >= Catalog.MaxLevel ? 1f
                : Mathf.Clamp01((float)(store.XP - store.XpThisLevel) / Mathf.Max(1, store.XpNextLevel - store.XpThisLevel));
            var bar = new Rect(a.x + 12f, a.yMax - 22f, a.width - 24f, 10f);
            GUI.color = new Color(1f, 1f, 1f, 0.15f * pcAlpha);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.82f, 0.3f, pcAlpha);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);

            float ah = Mathf.Clamp(36f + alerts.Count * 22f, 64f, 300f);
            var b = new Rect(x, a.yMax + 10f, w, ah);
            Card(b);
            GUILayout.BeginArea(new Rect(b.x + 12f, b.y + 8f, b.width - 24f, b.height - 12f));
            GUILayout.Label("ALERTS", cardTitle);
            if (alerts.Count == 0) GUILayout.Label("<color=#9dff9d>All good.</color>", rich);
            for (int i = 0; i < alerts.Count; i++) GUILayout.Label(alerts[i], small);
            GUILayout.EndArea();

            var c = new Rect(x, b.yMax + 10f, w, 200f);
            Card(c);
            GUILayout.BeginArea(new Rect(c.x + 12f, c.y + 8f, c.width - 24f, c.height - 12f));
            GUILayout.Label("LATEST", cardTitle);
            for (int i = store.Log.Count - 1, k = 0; i >= 0 && k < 6; i--, k++) GUILayout.Label(store.Log[i], small);
            GUILayout.EndArea();
        }

        void DrawTaskbar(float sw, float sh)
        {
            var bar = new Rect(0f, sh - 44f, sw, 44f);
            GUI.color = new Color(0.05f, 0.07f, 0.09f, 0.94f * pcAlpha);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);

            var clk = store.Clock;
            GUILayout.BeginArea(new Rect(6f, bar.y + 6f, sw - 12f, 32f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Get up (Tab)", W(110), GUILayout.Height(32f))) store.Avatar.StandUp();
            if (GUILayout.Button("Build", W(70), GUILayout.Height(32f))) OpenBuildView();
            GUILayout.Space(10f);
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w.Closing) continue;
                string label = (w.Minimized ? "<color=#9aa4ae>" : "") + AppNames[(int)w.App] + (w.Minimized ? "</color>" : "");
                taskButton.richText = true;
                if (GUILayout.Button(label, taskButton, W(110), GUILayout.Height(32f)))
                {
                    if (!w.Minimized && i == windows.Count - 1) w.Minimized = true;
                    else OpenApp(w.App);
                }
            }
            GUILayout.FlexibleSpace();
            for (int i = 0; i < GameClock.Speeds.Length; i++)
            {
                string label = i == 0 ? "||" : GameClock.Speeds[i] + "x";
                bool on = clk.SpeedIndex == i;
                if (GUILayout.Toggle(on, label, "Button", W(38), GUILayout.Height(32f)) && !on) clk.SetSpeed(i);
            }
            clk.ClosedBoost = GUILayout.Toggle(clk.ClosedBoost, " Fast nights", W(100), GUILayout.Height(32f));
            GUILayout.Label("<b>" + clk.TimeText + "</b>  " + clk.Weekday + ", day " + clk.Day
                + (clk.Paused ? "  <color=#ffcc55>PAUSED</color>" : ""), rich, W(210), GUILayout.Height(32f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ windows

        PcWindow WindowById(int id)
        {
            for (int i = 0; i < windows.Count; i++) if (1000 + (int)windows[i].App == id && !windows[i].Closing) return windows[i];
            return null;
        }

        void DrawWindow(int id)
        {
            var w = WindowById(id);
            if (w == null) return;
            GUI.enabled = true;
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);
            float ww = w.Rect.width, wh = w.Rect.height;
            Color col = AppColors[(int)w.App];
            GUI.color = new Color(col.r, col.g, col.b, pcAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, ww, 30f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, pcAlpha);
            GUI.DrawTexture(new Rect(8f, 5f, 20f, 20f), appIcons[(int)w.App]);
            GUI.Label(new Rect(34f, 0f, ww - 120f, 30f), AppTitles[(int)w.App], winTitle);
            if (GUI.Button(new Rect(ww - 62f, 4f, 26f, 22f), "_")) w.Minimized = true;
            if (GUI.Button(new Rect(ww - 32f, 4f, 26f, 22f), "x")) w.Closing = true;

            GUILayout.BeginArea(new Rect(12f, 38f, ww - 24f, wh - 48f));
            if (w.App == App.Shop) ShopApp(w, ww - 24f, wh - 48f);
            else if (w.App == App.Staff) StaffApp(w, ww - 24f, wh - 48f);
            else if (w.App == App.Roster) RosterApp(w, ww - 24f, wh - 48f);
            else if (w.App == App.Hiring) HiringApp(w, ww - 24f, wh - 48f);
            else
            {
                if (w.App == App.Stats)
                {
                    statsPage = GUILayout.Toolbar(statsPage, new[] { "Today", "Last 7 days" }, W(260));
                    GUILayout.Space(6f);
                }
                w.Scroll = GUILayout.BeginScrollView(w.Scroll);
                switch (w.App)
                {
                    case App.Bills: BillsApp(); break;
                    case App.Products: ProductsApp(); break;
                    case App.Licences: LicencesTab(); break;
                    case App.Assistant: DelegationTab(); break;
                    case App.Stats: if (statsPage == 0) OverviewTab(); else FinancesTab(); break;
                    case App.Expansion: ExpansionTab(); break;
                    default: InboxApp(); break;
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
            GUI.DragWindow(new Rect(0f, 0f, ww - 70f, 30f));

            if (clearCart) store.Cart.Clear();
            for (int i = 0; i < cartOps.Count; i++) store.Cart.Add(cartOps[i].Key, cartOps[i].Value);
            cartOps.Clear();
            clearCart = false;
        }

        // ------------------------------------------------------------------ apps

        void ShopApp(PcWindow w, float width, float height)
        {
            var cart = store.Cart;
            GUILayout.BeginHorizontal();

            // Catalogue, by supplier
            GUILayout.BeginVertical(W(width * 0.62f));
            GUILayout.Label("Catalogue", header);
            GUILayout.Label("Prices are per case; \"3d\" = shelf life in days, \"~40/day\" = average sold over the last week. Each supplier delivers on its own days at 06:00; "
                + "express comes in about an hour (+25%). Every delivery comes with a bill (Bills app). Weekends are busier than Mon-Tue.", small);
            w.Scroll = GUILayout.BeginScrollView(w.Scroll);
            bool anyLocked = false;
            for (int si = 0; si < store.Suppliers.Count; si++)
            {
                var sup = store.Suppliers[si];
                bool header0 = false;
                for (int i = 0; i < store.Products.Count; i++)
                {
                    var p = store.Products[i];
                    if (p.Def.Supplier != sup.Def) continue;
                    if (!store.IsLicensed(p.Def)) { anyLocked = true; continue; }
                    if (!header0)
                    {
                        GUILayout.Space(6f);
                        GUILayout.Label("<b>" + sup.Name + "</b>" + (sup.OnHold ? "  <color=#ff6655>ON HOLD: unpaid bills</color>" : ""), rich);
                        GUILayout.Label("Delivers " + sup.Def.DaysText + " (next " + store.DayText(store.NextDelivery(sup.Def)) + ")"
                            + "  ·  bills due in " + sup.Def.TermsDays + " days"
                            + (sup.Def.MinOrder > 0f ? "  ·  min order " + Money(sup.Def.MinOrder) : "")
                            + "  ·  credit left " + Money(store.CreditLeft(sup)) + " of " + Money(store.CreditLimit(sup))
                            + "  ·  " + (sup.Def.ReturnCredit > 0f ? Mathf.RoundToInt(sup.Def.ReturnCredit * 100f) + "% credit for expired" : "no returns"), small);
                        header0 = true;
                    }
                    int cs = p.Def.CaseSize, inCart = cart.CasesOf(p);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.Name, bold, W(118));
                    GUILayout.Label(cs + "/case " + Money(Store.CaseCost(p, false)) + (p.Life > 0 ? "  " + p.Life + "d" : ""), small, W(122));
                    string stock = p.Carried
                        ? "shelf " + p.ShelfTotal + "  back " + p.Back.Total + (p.Incoming > 0 ? "  coming " + p.Incoming : "")
                            + "  ~" + Mathf.RoundToInt(p.AvgSold) + "/day"
                        : "<color=#ffcc55>not on a shelf yet</color>" + (p.Back.Total > 0 ? "  back " + p.Back.Total : "");
                    GUILayout.Label(stock, small, W(208));
                    GUI.enabled = inCart > 0;
                    if (GUILayout.Button("-", W(26))) cartOps.Add(new KeyValuePair<Product, int>(p, -1));
                    GUI.enabled = true;
                    GUILayout.Label(inCart > 0 ? "<b>" + inCart + "</b>" : "0", rich, W(28));
                    if (GUILayout.Button("+", W(26))) cartOps.Add(new KeyValuePair<Product, int>(p, 1));
                    if (GUILayout.Button("+5", W(34))) cartOps.Add(new KeyValuePair<Product, int>(p, 5));
                    GUILayout.EndHorizontal();
                }
            }
            if (anyLocked)
            {
                GUILayout.Space(6f);
                GUILayout.Label("More products unlock with licences (Licences app).", small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12f);

            // Cart, by supplier
            GUILayout.BeginVertical("box");
            GUILayout.Label("Cart  <size=12>" + cart.Cases + " cases, " + cart.Units + " units</size>", header);
            w.Scroll2 = GUILayout.BeginScrollView(w.Scroll2, GUILayout.Height(Mathf.Max(140f, height - 330f)));
            if (cart.Lines.Count == 0) GUILayout.Label("Empty. Use + in the catalogue.", small);
            for (int si = 0; si < store.Suppliers.Count; si++)
            {
                var sup = store.Suppliers[si];
                float sub = cart.SupplierCost(sup.Def, false);
                if (sub <= 0f) continue;
                bool underMin = sub < sup.Def.MinOrder;
                GUILayout.Label("<b>" + sup.Name + "</b>  " + Money(sub) + "  ·  arrives " + store.DayText(store.NextDelivery(sup.Def))
                    + (underMin ? "  <color=#ff9977>(min " + Money(sup.Def.MinOrder) + ")</color>" : ""), rich);
                for (int i = 0; i < cart.Lines.Count; i++)
                {
                    var l = cart.Lines[i];
                    if (l.Product.Def.Supplier != sup.Def) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("   " + l.Product.Name + "  x" + l.Cases, W(150));
                    GUILayout.Label(Money(l.Cases * Store.CaseCost(l.Product, false)), W(80));
                    if (GUILayout.Button("x", W(24))) cartOps.Add(new KeyValuePair<Product, int>(l.Product, -l.Cases));
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            int free = store.BackFreeForOrders;
            string room = cart.Units > free ? "<color=#ff7766>" : "";
            GUILayout.Label("Back room: " + room + cart.Units + " in cart / " + free + " free" + (room.Length > 0 ? "</color>" : "")
                + "  <size=11>(stock on the way counts)</size>", rich);
            GUILayout.Space(4f);

            string truckProblem = store.OrderProblem(cart, false), expressProblem = store.OrderProblem(cart, true);
            GUI.enabled = truckProblem == null;
            if (GUILayout.Button("Order " + Money(cart.Cost(false)) + "  ·  on each supplier's next delivery", GUILayout.Height(32f)))
                store.PlaceOrder(cart, false);
            GUI.enabled = expressProblem == null;
            if (GUILayout.Button("Order " + Money(cart.Cost(true)) + "  ·  express, ~1 hour", GUILayout.Height(32f)))
                store.PlaceOrder(cart, true);
            GUI.enabled = cart.Lines.Count > 0;
            if (GUILayout.Button("Empty cart")) clearCart = true;
            GUI.enabled = true;
            string problem = cart.Lines.Count == 0 ? null : truckProblem ?? expressProblem;
            if (problem != null) GUILayout.Label("<color=#ff9977>" + problem + "</color>", small);
            GUILayout.Label("You pay later: each delivery brings a bill.", small);

            GUILayout.Space(6f);
            int express = 0;
            for (int i = 0; i < store.Products.Count; i++) express += store.Products[i].OnExpress;
            var coming = new List<string>();
            for (int i = 0; i < store.Suppliers.Count; i++)
                if (store.Suppliers[i].PendingDay >= 0) coming.Add(store.Suppliers[i].Name + " " + store.DayText(store.Suppliers[i].PendingDay));
            GUILayout.Label("On the way: " + (coming.Count > 0 ? string.Join(", ", coming.ToArray()) : "nothing by truck")
                + (express > 0 ? "; " + express + " units by express (about " + Mathf.CeilToInt(store.ExpressMinutesLeft) + " min)" : "") + ".", small);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        void BillsApp()
        {
            int day = store.Clock.Day;
            GUILayout.Label("Bills", header);
            GUILayout.Label("Unpaid " + Money(store.UnpaidTotal) + "   ·   due within 3 days " + Money(store.DueWithin(3))
                + "   ·   " + (store.OverdueCount > 0 ? "<color=#ff6655>" + store.OverdueCount + " overdue</color>" : "none overdue")
                + "   ·   cash " + Money(store.Economy.Cash), rich);
            GUILayout.Label("Overdue bills get a " + Mathf.RoundToInt(Store.LateFeeShare * 100f) + "% late fee. After " + Store.HoldAfterDays
                + " days overdue the supplier stops taking orders. Each supplier only lets you owe up to a credit limit (grows with store level): "
                + "pay early to order more. The assistant manager can pay bills (Assistant app).", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = store.DueWithin(3) > 0f;
            if (GUILayout.Button("Pay everything due within 3 days", W(260)))
            {
                int n = store.PayBillsDueWithin(3, 0f);
                Toast(n > 0 ? "Paid " + n + " bills." : "Not enough cash to pay them.");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.Label("Suppliers", bold);
            for (int i = 0; i < store.Suppliers.Count; i++)
            {
                var s = store.Suppliers[i];
                float owed = 0f;
                for (int k = 0; k < store.Invoices.Count; k++)
                    if (!store.Invoices[k].Paid && store.Invoices[k].Supplier == s) owed += store.Invoices[k].Amount;
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.Name, W(160));
                GUILayout.Label(s.Def.DaysText, small, W(100));
                GUILayout.Label("owed " + Money(owed) + " / limit " + Money(store.CreditLimit(s)), W(170));
                GUILayout.Label(s.Credit > 0f ? "credit " + Money(s.Credit) : "", W(100));
                GUILayout.Label(s.ReturnsUnits > 0 ? s.ReturnsUnits + " items to return (" + Money(s.ReturnsValue) + ")" : "", small, W(190));
                GUILayout.Label(s.OnHold ? "<color=#ff6655>ON HOLD</color>" : "<color=#9dff9d>ok</color>", rich, W(70));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8f);

            GUILayout.Label("Unpaid", bold);
            var unpaid = new List<Invoice>();
            for (int i = 0; i < store.Invoices.Count; i++) if (!store.Invoices[i].Paid) unpaid.Add(store.Invoices[i]);
            unpaid.Sort((a, b) => a.DueDay.CompareTo(b.DueDay));
            if (unpaid.Count == 0) GUILayout.Label("Nothing to pay.", small);
            for (int i = 0; i < unpaid.Count; i++)
            {
                var inv = unpaid[i];
                int left = inv.DueDay - day;
                string due = left < 0 ? "<color=#ff6655>" + (-left) + " days overdue</color>"
                    : left == 0 ? "<color=#ffcc55>due today</color>" : left == 1 ? "<color=#ffcc55>due tomorrow</color>" : "due in " + left + " days";
                GUILayout.BeginHorizontal();
                GUILayout.Label("#" + inv.Number, W(52));
                GUILayout.Label(inv.Supplier.Name + (inv.Express ? " (express)" : ""), W(190));
                GUILayout.Label(inv.Units + " units, day " + inv.IssuedDay, small, W(120));
                GUILayout.Label(due, rich, W(130));
                string extra = (inv.CreditUsed > 0f ? " -" + Money(inv.CreditUsed) + " credit" : "") + (inv.LateFee > 0f ? " +" + Money(inv.LateFee) + " fee" : "");
                GUILayout.Label("<b>" + Money(inv.Amount) + "</b><size=11>" + extra + "</size>", rich, W(170));
                GUI.enabled = store.Economy.Cash >= inv.Amount;
                if (GUILayout.Button("Pay", W(60))) store.PayInvoice(inv);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Paid recently", bold);
            int shown = 0;
            for (int i = store.Invoices.Count - 1; i >= 0 && shown < 12; i--)
            {
                var inv = store.Invoices[i];
                if (!inv.Paid) continue;
                shown++;
                GUILayout.Label("<color=#999999>#" + inv.Number + "  " + inv.Supplier.Name + "  " + Money(inv.Amount) + "  paid day " + inv.PaidDay
                    + (inv.PaidDay > inv.DueDay ? " (late)" : "") + "</color>", rich);
            }
            if (shown == 0) GUILayout.Label("None yet.", small);
        }

        void ProductsApp()
        {
            GUILayout.Label("Products & prices", header);
            GUILayout.Label("Click a shelf in Build to choose what it holds. Order stock in the Shop app. Higher prices earn more per item, but some customers won't buy. "
                + "Expired = out of date and still in the store (\"soon\" = expires tonight). Binned = scanned out today.", small);
            GUILayout.Space(6f);
            for (int c = 0; c < store.Categories.Count; c++)
            {
                var cat = store.Categories[c];
                bool anyLicensed = false;
                for (int i = 0; i < cat.Products.Count; i++) if (store.IsLicensed(cat.Products[i].Def)) anyLicensed = true;
                if (!anyLicensed) continue;
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
                string[] heads = { "Product", "Price", "Shelves", "On shelf", "Back", "Coming", "Sold", "Missed", "Expired", "Binned" };
                float[] widths = { 120f, 60f, 58f, 70f, 52f, 58f, 44f, 54f, 60f, 54f };
                for (int k = 0; k < heads.Length; k++) GUILayout.Label("<b>" + heads[k] + "</b>", rich, W(widths[k]));
                GUILayout.EndHorizontal();

                for (int i = 0; i < cat.Products.Count; i++)
                {
                    var p = cat.Products[i];
                    if (!store.IsLicensed(p.Def))
                    {
                        GUILayout.Label("<color=#777777>" + p.Name + "   (" + p.Def.Licence.Name + " licence, level " + p.Def.Licence.Level + ")</color>", rich);
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.Carried ? p.Name : "<color=#999999>" + p.Name + "</color>", rich, W(widths[0]));
                    GUILayout.Label("$" + p.Price.ToString("0.00"), W(widths[1]));
                    GUILayout.Label(p.Fixtures.Count.ToString(), W(widths[2]));
                    if (p.Carried)
                    {
                        float fill = p.ShelfFill;
                        string colr = fill > 0.5f ? "#9dff9d" : fill > 0.2f ? "#ffd166" : "#ff7766";
                        GUILayout.Label("<color=" + colr + ">" + p.ShelfTotal + "/" + p.ShelfCap + "</color>", rich, W(widths[3]));
                    }
                    else GUILayout.Label("-", W(widths[3]));
                    GUILayout.Label(p.Baked ? "-" : p.Back.Total.ToString(), W(widths[4]));
                    GUILayout.Label(p.Baked ? "-" : p.Incoming.ToString(), W(widths[5]));
                    GUILayout.Label(p.SoldToday.ToString(), W(widths[6]));
                    GUILayout.Label(p.MissedToday.ToString(), W(widths[7]));
                    int exp = p.ExpiredOnHand, soon = p.ExpiringSoon(1);
                    GUILayout.Label(p.Life <= 0 ? "-" : exp > 0 ? "<color=#ff7766>" + exp + "</color>" : soon > 0 ? "<color=#ffd166>" + soon + " soon</color>" : "0", rich, W(widths[8]));
                    GUILayout.Label(p.BinnedToday.ToString(), W(widths[9]));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }
        }

        void InboxApp()
        {
            GUILayout.Label("Inbox", header);
            if (store.Log.Count == 0) GUILayout.Label("No messages.", small);
            for (int i = store.Log.Count - 1; i >= 0; i--) GUILayout.Label(store.Log[i], rich);
        }
    }
}
