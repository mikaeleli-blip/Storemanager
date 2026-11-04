using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoreManager
{
    /// <summary>
    /// Tags floating over the world: checkout numbers (a little till display), complaints, slacking staff,
    /// whoever you selected, the NOW OPEN banner, and fixture names in the build view.
    /// </summary>
    public class WorldLabels
    {
        readonly VisualElement layer;
        readonly List<Label> pool = new List<Label>();
        int used;
        Camera cam;

        static readonly string[] Kinds = { "wlabel--name", "wlabel--alarm", "wlabel--warn", "wlabel--lane", "wlabel--banner" };

        public WorldLabels(VisualElement root)
        {
            layer = Pos.Box("layer", root);
            layer.pickingMode = PickingMode.Ignore;
        }

        void Add(Vector3 world, string text, string kind)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            var panel = layer.panel;
            if (panel == null) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);

            if (used == pool.Count)
            {
                var nl = Pos.L("", "wlabel");
                nl.pickingMode = PickingMode.Ignore;
                pool.Add(nl);
                layer.Add(nl);
            }
            var l = pool[used++];
            Pos.Show(l, true);
            Pos.Set(l, text);
            for (int i = 0; i < Kinds.Length; i++) Pos.Toggle(l, Kinds[i], Kinds[i] == kind);
            l.style.left = p.x;
            l.style.top = p.y;
        }

        public void Refresh(Store store, Hud hud, bool visible)
        {
            used = 0;
            cam = store.Cam != null ? store.Cam.GetComponent<Camera>() : null;
            if (visible && cam != null) Collect(store, hud);
            for (int i = used; i < pool.Count; i++) Pos.Show(pool[i], false);
        }

        void Collect(Store store, Hud hud)
        {
            bool desk = store.Avatar.AtDesk;
            if (desk)
            {
                Add(new Vector3(4f, 2f, 30f), "OFFICE", "wlabel--warn");
                for (int i = 0; i < store.Fixtures.Count; i++)
                {
                    var f = store.Fixtures[i];
                    var pos = f.Center + new Vector3(0f, 2.3f, 0f);
                    string kind = null, text;
                    if (f.HoldsProducts)
                    {
                        if (f.Product == null) { text = "(empty)"; kind = "wlabel--warn"; }
                        else if (f.Stock.Total == 0) { text = f.Product.Name + "  0"; kind = "wlabel--alarm"; }
                        else text = f.Product.Name + "  " + f.Stock.Total;
                    }
                    else text = f.Label;
                    if (f == BuildMode.I.Selected) { text = "[ " + text + " ]"; kind = "wlabel--name"; }
                    Add(pos, text, kind);
                }
            }
            else
            {
                if (store.InOpeningWeek) Add(store.BannerPoint, "NOW OPEN", "wlabel--banner");
                for (int i = 0; i < store.Lanes.Count; i++)
                    Add(store.Lanes[i].Fixture.Center + new Vector3(0f, 2.6f, 0f), (i + 1).ToString(), "wlabel--lane");
            }
            for (int i = 0; i < store.Complaints.Count; i++)
            {
                var c = store.Complaints[i];
                if (c != null && c.Complaining) Add(c.transform.position + new Vector3(0f, 3.2f, 0f), "! " + c.DisplayName, "wlabel--alarm");
            }
            // You can see someone slacking if you're on the floor and they're not too far away.
            if (!desk)
                for (int i = 0; i < store.Staff.All.Count; i++)
                {
                    var e = store.Staff.All[i];
                    if (e == null || !e.OnShift || !e.Slacking || store.Avatar.DistanceTo(e.transform.position) > 16f) continue;
                    Add(e.transform.position + new Vector3(0f, 2.4f, 0f), e.SlackWhat, "wlabel--warn");
                }
            if (hud.Selected != null && hud.Selected.gameObject.activeInHierarchy)
                Add(hud.Selected.transform.position + new Vector3(0f, 2.5f, 0f), hud.Selected.DisplayName, "wlabel--name");
            if (hud.SelectedCustomer != null)
                Add(hud.SelectedCustomer.transform.position + new Vector3(0f, 2.5f, 0f), hud.SelectedCustomer.DisplayName, "wlabel--name");
        }
    }
}
