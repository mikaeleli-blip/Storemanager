using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Placing, moving and selling fixtures, and choosing what each shelf holds. Only at the office desk.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public static BuildMode I;

        public bool Active;
        public FixtureDef Placing;
        public Fixture Moving;
        public Fixture Selected;
        public int Rot;

        Store store;
        GameObject ghost;
        Vector2Int ghostOrigin = new Vector2Int(-999, -999);
        int ghostRot = -1;
        bool ghostValid;
        string reason;
        Material okMat, badMat, spotMat;
        Vector2 paletteScroll, panelScroll;
        int confirmSell = -1;
        float confirmTime;

        public bool Busy { get { return Placing != null || Moving != null; } }

        void Awake() { I = this; }

        void Start() { store = Store.I; }

        public void Open()
        {
            Active = true;
        }

        public void Close()
        {
            CancelTool();
            Active = false;
        }

        public void StartPlacing(FixtureDef d)
        {
            CancelTool();
            Placing = d;
        }

        public void StartMoving(Fixture f)
        {
            CancelTool();
            Moving = f;
            Rot = f.Rot;
            if (f.Root != null) f.Root.gameObject.SetActive(false);
        }

        public void CancelTool()
        {
            if (Moving != null && Moving.Root != null) Moving.Root.gameObject.SetActive(true);
            Placing = null;
            Moving = null;
            DestroyGhost();
        }

        void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost);
            ghost = null;
            ghostOrigin = new Vector2Int(-999, -999);
            ghostRot = -1;
        }

        void EnsureMaterials()
        {
            if (okMat != null) return;
            okMat = ModelLibrary.Transparent(new Color(0.3f, 1f, 0.45f, 0.45f));
            badMat = ModelLibrary.Transparent(new Color(1f, 0.25f, 0.2f, 0.45f));
            spotMat = ModelLibrary.Transparent(new Color(0.3f, 0.6f, 1f, 0.7f));
        }

        bool MouseTile(out Vector2Int tile)
        {
            tile = new Vector2Int();
            if (store == null || store.Cam == null) return false;
            var cam = store.Cam.GetComponent<Camera>();
            if (cam == null) return false;
            Ray ray = cam.ScreenPointToRay(GameInput.MousePosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (!ground.Raycast(ray, out enter)) return false;
            tile = StoreGrid.TileAt(ray.GetPoint(enter));
            return true;
        }

        void Update()
        {
            if (store == null) store = Store.I;
            if (store == null || !Active) return;
            if (!store.Avatar.AtDesk) { Close(); return; }

            if (GameInput.Down(GameKey.R)) Rot = (Rot + 1) % 4;
            if (GameInput.Down(GameKey.Escape) || (GameInput.MouseDown(1) && !Hud.PointerOverUI))
            {
                if (Busy) CancelTool();
                else Selected = null;
            }

            Vector2Int tile;
            bool hasTile = MouseTile(out tile);
            var def = Placing != null ? Placing : Moving != null ? Moving.Def : null;

            if (def != null)
            {
                if (!hasTile) return;
                var origin = Placement.OriginFor(def, Rot, tile);
                if (origin != ghostOrigin || Rot != ghostRot)
                {
                    reason = Placing != null ? store.CanPlace(def, origin, Rot) : Placement.Validate(store, def, origin, Rot, Moving);
                    ghostValid = reason == null;
                    EnsureMaterials();
                    if (ghost != null) Destroy(ghost);
                    ghost = store.Builder.BuildGhost(def, origin, Rot, ghostValid ? okMat : badMat, spotMat);
                    ghostOrigin = origin;
                    ghostRot = Rot;
                }
                if (GameInput.MouseDown(0) && !Hud.PointerOverUI)
                {
                    if (!ghostValid)
                    {
                        Hud.Toast(reason);
                        return;
                    }
                    if (Placing != null)
                    {
                        var f = store.Place(def, origin, Rot);
                        if (f != null)
                        {
                            Selected = f;
                            DestroyGhost();
                        }
                    }
                    else
                    {
                        var f = Moving;
                        string why = store.Move(f, origin, Rot);
                        if (why != null) Hud.Toast(why);
                        else
                        {
                            Moving = null;
                            Selected = f;
                            DestroyGhost();
                        }
                    }
                }
            }
            else if (hasTile && GameInput.MouseDown(0) && !Hud.PointerOverUI)
            {
                Selected = store.FixtureAt(tile);
            }
        }

        // ------------------------------------------------------------------ GUI (called by Hud)

        public void DrawPalette(Hud hud, float sw, float sh)
        {
            var r = new Rect(8f, 40f, 300f, Mathf.Max(260f, sh - 230f));
            hud.Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));
            GUILayout.Label("Build", hud.Header);
            GUILayout.Label("Cash " + Economy.Money(store.Economy.Cash), hud.Bold);
            paletteScroll = GUILayout.BeginScrollView(paletteScroll);
            var list = Catalog.Fixtures;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                bool on = Placing == d;
                var lic = Catalog.LicenceForFixture(d.Id);
                bool locked = !store.Owns(lic);
                string label = locked
                    ? d.Name + "   (needs " + lic.Name + ")"
                    : d.Name + "   " + Economy.Money(d.Cost) + "   (" + d.W + "x" + d.D + ")";
                GUI.enabled = !locked && (store.Economy.Cash >= d.Cost || on);
                if (GUILayout.Toggle(on, label, "Button", GUILayout.Height(30f)) != on)
                {
                    if (on) CancelTool();
                    else StartPlacing(d);
                }
                GUI.enabled = true;
                if (on) GUILayout.Label(d.Description, hud.Small);
            }
            GUILayout.EndScrollView();

            if (Busy)
            {
                string what = Placing != null ? "Placing " + Placing.Name : "Moving " + Moving.Label;
                GUILayout.Label(what + ".  R: rotate   Right-click: cancel", hud.Small);
                if (reason != null && ghost != null && !ghostValid)
                    GUILayout.Label("<color=#ff8877>" + reason + "</color>", hud.Small);
                GUILayout.Label("<color=#88b8ff>Blue squares</color> = where staff or customers stand.", hud.Small);
            }
            else
            {
                GUILayout.Label("Pick a fixture, then click on the floor. Click a shelf to choose its product.", hud.Small);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate (R)")) Rot = (Rot + 1) % 4;
            if (GUILayout.Button("Done")) { Close(); hud.ShowTab(0); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Info for the selected fixture; product choice, move and sell when at the desk.</summary>
        public void DrawFixturePanel(Hud hud, float sw, float sh, float top)
        {
            var f = Selected;
            if (f == null) return;
            if (f.Removed) { Selected = null; return; }
            bool edit = store.Avatar.AtDesk;
            var choices = new List<ProductDef>();
            if (f.HoldsProducts)
            {
                var all = Catalog.ProductsFor(f.Def);
                for (int i = 0; i < all.Count; i++) if (store.IsLicensed(all[i])) choices.Add(all[i]);
            }
            float h = 150f + (f.HoldsProducts ? 30f + ((choices.Count + 1) / 2) * 30f + 50f : 30f);
            var r = new Rect(sw - 408f, top, 400f, Mathf.Min(h, sh - top - 270f));
            hud.Panel(r);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 10f));
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label(f.Def.Name + "  <size=12>#" + f.Id + "</size>", hud.Header);

            if (f.HoldsProducts)
            {
                if (f.Product == null) GUILayout.Label("<color=#ffcc55>Empty: choose what goes here.</color>", hud.Rich);
                else
                {
                    var p = f.Product;
                    GUILayout.Label("<b>" + p.Name + "</b>   on the shelf " + f.Stock.Total + " / " + f.Def.Capacity, hud.Rich);
                    if (p.Baked) GUILayout.Label("Baked by bakers at an oven.", hud.Small);
                    else GUILayout.Label("In the back room " + p.Back.Total + ", on order " + p.OnOrder + ".  Sold today " + p.SoldToday + ".", hud.Small);
                }
                if (f.IsCounter) GUILayout.Label("Clerk: " + (f.Clerk != null ? f.Clerk.DisplayName : "none") + "   waiting: " + f.Waiting.Count, hud.Small);

                if (edit)
                {
                    GUILayout.Label("What goes here:", hud.Bold);
                    if (choices.Count < Catalog.ProductsFor(f.Def).Count)
                        GUILayout.Label("More products unlock with licences (Licences tab).", hud.Small);
                    for (int i = 0; i < choices.Count; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        for (int k = i; k < i + 2 && k < choices.Count; k++)
                        {
                            var pd = choices[k];
                            bool on = f.Product != null && f.Product.Def == pd;
                            if (GUILayout.Toggle(on, pd.Name, "Button", GUILayout.Width(180f), GUILayout.Height(26f)) && !on)
                                store.AssignProduct(f, pd);
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (f.Product != null && GUILayout.Button("Clear shelf", GUILayout.Width(180f))) store.AssignProduct(f, null);
                    if (f.Product != null && !f.Product.Baked)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Order 1 case", GUILayout.Width(120f))) store.Order(f.Product, f.Product.Def.CaseSize);
                        if (GUILayout.Button("Order to par", GUILayout.Width(120f)))
                        {
                            int n = store.OrderToPar(f.Product, 0f);
                            Hud.Toast(n > 0 ? "Ordered " + f.Product.Name + " up to par." : f.Product.Name + " is already at par.");
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }
            else if (f.IsCheckout && f.Lane != null)
            {
                var l = f.Lane;
                GUILayout.Label("Checkout " + (l.Index + 1) + ". Cashier: " + (l.Cashier != null ? l.Cashier.DisplayName : "none")
                    + ".  In the queue: " + l.Queue.Count, hud.Rich);
            }
            else if (f.IsOven)
            {
                GUILayout.Label("Bakers on shift: " + store.Staff.OnShiftCount(Role.Baker) + ". They bake for the bakery shelves.", hud.Rich);
            }

            if (edit)
            {
                GUILayout.Space(6f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Move", GUILayout.Width(110f))) StartMoving(f);
                bool confirming = confirmSell == f.Id && Time.unscaledTime - confirmTime < 3f;
                string sellLabel = confirming ? "Sure?" : "Sell (+" + Economy.Money(f.Def.Cost * Store.SellRefund) + ")";
                if (GUILayout.Button(sellLabel, GUILayout.Width(150f)))
                {
                    if (confirming) { store.Sell(f); Selected = null; confirmSell = -1; }
                    else { confirmSell = f.Id; confirmTime = Time.unscaledTime; }
                }
                if (GUILayout.Button("Close", GUILayout.Width(80f))) Selected = null;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Changes are made from your office desk.", hud.Small);
                if (GUILayout.Button("Close", GUILayout.Width(80f))) Selected = null;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
