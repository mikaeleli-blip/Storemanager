using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoreManager
{
    /// <summary>
    /// The HUD on the shop floor and in the build view: the till's customer display along the top (time, cash,
    /// speed keys), the alerts slip, the journal tape, notices on a shelf label, the controls legend and the
    /// name badge / receipt for whoever you clicked.
    /// </summary>
    public class FloorHud
    {
        readonly VisualElement layer;

        // customer display
        Label day, time, status, cash, rep, level, sales, inStore, paused;
        VisualElement xpFill, pausedField;
        readonly List<Button> speedKeys = new List<Button>();
        Button fastNights;

        // alerts slip
        VisualElement alertsWrap, alertsBody;
        readonly List<Label> alertLines = new List<Label>();
        public float AlertsBottom { get; private set; }

        // journal tape
        readonly List<Label> journal = new List<Label>();
        const int JournalLines = 7;

        // notice, legend, desk key
        VisualElement tag, legendFloor, legendBuild;
        Label tagText;
        Button sitKey;

        // selection
        VisualElement badge, badgeStripe, slip;
        Label badgeName, badgeRole, badgeStatus, badgeMorale, badgeMoney, badgeIncident, badgeNote;
        VisualElement moraleFill;
        readonly Label[] statVals = new Label[5];
        Button praise, word, written;
        Label slipName, slipStatus, slipSat, slipCart, slipComplaint;

        Store store;
        Hud hud;

        public FloorHud(VisualElement root)
        {
            layer = Pos.Box("layer", root);
            layer.pickingMode = PickingMode.Ignore;
            BuildDisplay();
            BuildAlerts();
            BuildJournal();
            BuildNotice();
            BuildLegend();
            BuildBadge();
            BuildSlip();
        }

        // ------------------------------------------------------------------ building

        VisualElement Field(VisualElement parent, string cap, out Label val)
        {
            var f = Pos.Box("vfd__field", parent);
            f.Add(Pos.L(cap, "vfd__cap"));
            val = Pos.L("", "vfd__val");
            f.Add(val);
            return f;
        }

        void BuildDisplay()
        {
            var bar = Pos.Box("vfd", layer);
            var glass = Pos.Box("vfd__glass", bar);
            Field(glass, "DAY", out day);
            Field(glass, "TIME", out time);
            Field(glass, "STORE", out status);
            Field(glass, "CASH", out cash);
            Field(glass, "SALES TODAY", out sales);
            Field(glass, "REPUTATION", out rep);
            var lv = Field(glass, "LEVEL", out level);
            var xp = Pos.Box("vfd__xp", lv);
            xpFill = Pos.Box("vfd__xpfill", xp);
            Field(glass, "IN STORE", out inStore);
            pausedField = Field(glass, "", out paused);
            paused.AddToClassList("vfd__val--warn");

            var keys = Pos.Box("vfd__keys", bar);
            for (int i = 0; i < GameClock.Speeds.Length; i++)
            {
                int k = i;
                var b = Pos.Key(i == 0 ? "II" : GameClock.Speeds[i] + "x", () => store.Clock.SetSpeed(k), "dark");
                keys.Add(b);
                speedKeys.Add(b);
            }
            fastNights = Pos.Key("FAST NIGHTS", () => store.Clock.ClosedBoost = !store.Clock.ClosedBoost, "dark");
            fastNights.style.marginLeft = 10f;
            keys.Add(fastNights);
        }

        void BuildAlerts()
        {
            alertsWrap = Pos.Receipt(out alertsBody, true, true);
            alertsWrap.AddToClassList("alerts");
            alertsBody.Add(Pos.L("*** ALERTS ***", "receipt__title"));
            alertsBody.Add(Pos.Rule());
            layer.Add(alertsWrap);
        }

        void BuildJournal()
        {
            VisualElement body;
            var wrap = Pos.Receipt(out body, true, false);
            wrap.AddToClassList("journal");
            wrap.pickingMode = PickingMode.Ignore;
            body.pickingMode = PickingMode.Ignore;
            var head = Pos.L("JOURNAL", "receipt__sub");
            head.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(head);
            for (int i = 0; i < JournalLines; i++)
            {
                var l = Pos.L("", "receipt__line");
                l.pickingMode = PickingMode.Ignore;
                journal.Add(l);
                body.Add(l);
            }
            layer.Add(wrap);
        }

        void BuildNotice()
        {
            var row = Pos.Box("top-center", layer);
            row.pickingMode = PickingMode.Ignore;
            tag = Pos.Box("tag", row);
            tag.pickingMode = PickingMode.Ignore;
            tagText = Pos.L("", "tag__text");
            tag.Add(tagText);
        }

        void Legend(VisualElement parent, params string[] parts)
        {
            // parts: "cap|cap|text" groups
            foreach (var part in parts)
            {
                var bits = part.Split('|');
                for (int i = 0; i < bits.Length - 1; i++) parent.Add(Pos.Cap(bits[i]));
                parent.Add(Pos.L(bits[bits.Length - 1], "cap-text"));
            }
        }

        void BuildLegend()
        {
            var row = Pos.Box("bottom-center", layer);
            row.pickingMode = PickingMode.Ignore;

            sitKey = Pos.Key("SIT AT DESK   [TAB]", () => store.Avatar.SitDown(), "green big");
            sitKey.AddToClassList("sit-key");
            row.Add(sitKey);

            legendFloor = Pos.Box("legend", row);
            Legend(legendFloor, "W|A|S|D|walk", "RMB|walk to", "Q|E|turn", "WHEEL|zoom", "SPACE|pause", "1-4|speed");
            legendBuild = Pos.Box("legend", row);
            Legend(legendBuild, "CLICK|place / pick a shelf", "R|rotate", "RMB|cancel", "WASD|Q|E|camera", "ESC|back to the computer");
        }

        void BuildBadge()
        {
            badge = Pos.Box("badge", layer);
            badgeStripe = Pos.Box("badge__stripe", badge);
            badgeName = Pos.L("", "badge__name");
            badgeRole = Pos.L("", "badge__role");
            badgeStripe.Add(badgeName);
            badgeStripe.Add(badgeRole);

            var body = Pos.Box("badge__body", badge);
            badgeStatus = Pos.L("", "receipt__line");
            body.Add(badgeStatus);
            var mrow = Pos.Box("row", body);
            mrow.Add(Pos.L("MORALE", "stat__cap"));
            badgeMorale = Pos.L("", "stat__cap");
            badgeMorale.style.marginLeft = 8f;
            mrow.Add(badgeMorale);
            var meter = Pos.Box("meter", body);
            moraleFill = Pos.Box("meter__fill", meter);
            badgeMoney = Pos.L("", "receipt__line");
            body.Add(badgeMoney);

            var stats = Pos.Box("stat-row", body);
            string[] caps = { "SPEED", "SERVICE", "RELIABLE", "LEADER", "WORK ETHIC" };
            for (int i = 0; i < 5; i++)
            {
                var s = Pos.Box("stat", stats);
                s.Add(Pos.L(caps[i], "stat__cap"));
                statVals[i] = Pos.L("", "stat__val");
                s.Add(statVals[i]);
            }
            badgeIncident = Pos.L("", "receipt__line receipt__line--red");
            body.Add(badgeIncident);

            var keys = Pos.Box("key-row", body);
            praise = Pos.Key("PRAISE", () => Hud.Toast(hud.Selected.Praise()), "green");
            word = Pos.Key("A WORD", () => Hud.Toast(hud.Selected.VerbalWarning()), "amber");
            written = Pos.Key("WRITTEN WARNING", () => Hud.Toast(hud.Selected.WrittenWarning()), "red");
            var close = Pos.Key("CLOSE", () => hud.Selected = null);
            close.style.marginLeft = new StyleLength(StyleKeyword.Auto);
            keys.Add(praise);
            keys.Add(word);
            keys.Add(written);
            keys.Add(close);
            badgeNote = Pos.L("", "note");
            body.Add(badgeNote);
        }

        void BuildSlip()
        {
            VisualElement body;
            slip = Pos.Receipt(out body, true, true);
            slip.AddToClassList("slip");
            layer.Add(slip);
            body.Add(Pos.L("CUSTOMER", "receipt__title"));
            slipName = Pos.L("", "receipt__sub");
            body.Add(slipName);
            body.Add(Pos.Rule());
            slipStatus = Pos.L("", "receipt__line");
            slipSat = Pos.L("", "receipt__line");
            slipCart = Pos.L("", "receipt__line receipt__total");
            slipComplaint = Pos.L("", "receipt__line receipt__line--red");
            body.Add(slipStatus);
            body.Add(slipSat);
            body.Add(Pos.Rule());
            body.Add(slipCart);
            body.Add(slipComplaint);
            var keys = Pos.Box("key-row", body);
            var close = Pos.Key("CLOSE", () => hud.SelectedCustomer = null);
            close.style.marginLeft = new StyleLength(StyleKeyword.Auto);
            keys.Add(close);
        }

        // ------------------------------------------------------------------ refresh

        public void Refresh(Store s, Hud h, bool visible)
        {
            store = s;
            hud = h;
            Pos.Show(layer, visible);
            if (!visible) { AlertsBottom = 48f; return; }

            RefreshDisplay();
            RefreshAlerts();
            RefreshJournal();

            Pos.Show(tag, hud.ToastLeft > 0f);
            if (hud.ToastLeft > 0f) Pos.Set(tagText, Pos.Plain(hud.ToastText));

            bool desk = store.Avatar.AtDesk;
            Pos.Show(legendFloor, !desk);
            Pos.Show(legendBuild, desk);
            Pos.Show(sitKey, !desk && store.Avatar.InOffice);

            var e = hud.Selected;
            Pos.Show(badge, e != null);
            if (e != null) RefreshBadge(e);
            var c = e == null ? hud.SelectedCustomer : null;
            Pos.Show(slip, c != null);
            if (c != null) RefreshSlip(c);
        }

        void RefreshDisplay()
        {
            var clk = store.Clock;
            Pos.Set(day, clk.Day + " " + clk.Weekday.ToUpperInvariant());
            Pos.Set(time, clk.TimeText);
            string st = !store.Opened ? "NOT OPEN" : clk.IsOpen ? "OPEN" : "CLOSED";
            Pos.Set(status, st);
            Pos.Toggle(status, "vfd__val--cool", !store.Opened);
            Pos.Toggle(status, "vfd__val--bad", store.Opened && !clk.IsOpen);
            Pos.Set(cash, Economy.Money(store.Economy.Cash));
            Pos.Toggle(cash, "vfd__val--bad", store.Economy.Cash < 0f);
            Pos.Set(sales, Economy.Money(store.Economy.Today.Sales));
            int r = Mathf.RoundToInt(store.Reputation);
            Pos.Set(rep, r + " " + new string('*', Mathf.Clamp(r / 20 + 1, 1, 5)));
            bool max = store.Level >= Catalog.MaxLevel;
            int have = store.XP - store.XpThisLevel, need = Mathf.Max(1, store.XpNextLevel - store.XpThisLevel);
            Pos.Set(level, store.Level + (max ? " MAX" : "  " + have + "/" + need));
            xpFill.style.width = Length.Percent(max ? 100f : Mathf.Clamp01(have / (float)need) * 100f);
            Pos.Set(inStore, store.Customers.Count.ToString());
            Pos.Show(pausedField, clk.Paused);
            if (clk.Paused) Pos.Set(paused, Time.unscaledTime % 1.2f < 0.8f ? "PAUSED" : "");
            for (int i = 0; i < speedKeys.Count; i++) Pos.Toggle(speedKeys[i], "key--on", clk.SpeedIndex == i);
            Pos.Toggle(fastNights, "key--on", clk.ClosedBoost);
        }

        void RefreshAlerts()
        {
            var list = hud.Alerts;
            Pos.Show(alertsWrap, list.Count > 0);
            while (alertLines.Count < list.Count)
            {
                var l = Pos.L("", "receipt__line");
                alertLines.Add(l);
                alertsBody.Add(l);
            }
            for (int i = 0; i < alertLines.Count; i++)
            {
                var l = alertLines[i];
                Pos.Show(l, i < list.Count);
                if (i >= list.Count) continue;
                string ink;
                string text = Pos.Ink(list[i], out ink);
                Pos.Set(l, (ink == "red" ? "! " : "  ") + text.TrimStart('!', ' '));
                Pos.Toggle(l, "receipt__line--red", ink == "red");
                Pos.Toggle(l, "receipt__line--amber", ink == "amber");
            }
            var rect = alertsWrap.worldBound;
            AlertsBottom = list.Count > 0 && !float.IsNaN(rect.yMax) ? rect.yMax : 48f;
        }

        void RefreshJournal()
        {
            var log = store.Log;
            int start = Mathf.Max(0, log.Count - JournalLines);
            for (int i = 0; i < JournalLines; i++)
            {
                int k = start + i;
                Pos.Set(journal[i], k < log.Count ? Pos.Plain(log[k]) : "");
                Pos.Toggle(journal[i], "receipt__line--faint", k < log.Count - 2);
            }
        }

        void RefreshBadge(Employee e)
        {
            Color rc = Employee.RoleColor(e.Role);
            badgeStripe.style.backgroundColor = rc;
            Color ink = Pos.Light(rc) ? new Color(0.12f, 0.12f, 0.11f) : Color.white;
            badgeName.style.color = ink;
            badgeRole.style.color = ink;
            Pos.Set(badgeName, e.DisplayName.ToUpperInvariant());
            Pos.Set(badgeRole, (Catalog.RoleName(e.Role) + (e.RankName.Length > 0 ? " · " + e.RankName : "")).ToUpperInvariant());

            string st = e.OnShift ? e.Status : e.ArriveAt >= 0f ? "Late, not here yet" : e.Absent ? e.Status : "Off duty";
            Pos.Set(badgeStatus, "STATUS  " + st);
            Pos.Toggle(badgeStatus, "receipt__line--red", e.Slacking);
            int m = Mathf.RoundToInt(e.Morale);
            Pos.Set(badgeMorale, m + (e.MoraleNote.Length > 0 ? "  " + e.MoraleNote : ""));
            moraleFill.style.width = Length.Percent(Mathf.Clamp(e.Morale, 0f, 100f));
            moraleFill.style.backgroundColor = e.Morale >= 60f ? new Color(0.18f, 0.62f, 0.37f) : e.Morale >= 35f ? new Color(0.95f, 0.71f, 0.2f) : new Color(0.78f, 0.26f, 0.23f);
            Pos.Set(badgeMoney, "WAGE $" + e.Wage.ToString("0") + "/h" + (e.OnProbation ? "   PROBATION" : "")
                + (e.ActiveWrittenWarnings > 0 ? "   " + e.ActiveWrittenWarnings + " WRITTEN WARNING" + (e.ActiveWrittenWarnings > 1 ? "S" : "") : ""));
            for (int i = 0; i < 5; i++) Pos.Set(statVals[i], e.Known(i));

            var inc = e.WarnableIncident;
            Pos.Show(badgeIncident, inc != null);
            if (inc != null) Pos.Set(badgeIncident, "RECENT: " + inc.Note + " (day " + inc.Day + " " + inc.Time + ")");

            var av = store.Avatar;
            bool near = e.OnShift && !av.AtDesk && av.DistanceTo(e.transform.position) <= ManagerAvatar.TalkRange;
            praise.SetEnabled(near);
            word.SetEnabled(near);
            written.SetEnabled(near && inc != null);
            Pos.Set(badgeNote, !near
                ? (av.AtDesk ? "Get up from the desk and walk over to talk. Written warnings can also be given in the Staff app."
                    : e.OnShift ? "Walk within 3 m to talk." : "Not at work right now.")
                : "\"A word\" is a verbal warning: they behave for a few days. A written warning (áminning) goes on their record; two allow dismissal.");
        }

        void RefreshSlip(Customer c)
        {
            Pos.Set(slipName, c.DisplayName.ToUpperInvariant());
            Pos.Set(slipStatus, "STATUS        " + c.Status);
            Pos.Set(slipSat, "SATISFACTION  " + Mathf.RoundToInt(c.Satisfaction));
            Pos.Toggle(slipSat, "receipt__line--red", c.Satisfaction < 35f);
            Pos.Set(slipCart, ("CART  " + c.Items + " ITEMS").PadRight(22) + Economy.Money(c.CartValue));
            Pos.Show(slipComplaint, c.Complaining);
            if (c.Complaining) Pos.Set(slipComplaint, "COMPLAINT: " + c.ComplaintReason);
        }
    }
}
