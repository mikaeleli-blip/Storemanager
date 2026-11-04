using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>Office computer staff apps: Staff (profiles, reviews, discipline), Roster (weekly schedule), Hiring (interviews).</summary>
    public partial class Hud
    {
        Employee staffSel;
        Applicant appSel;
        int offerRole, offerContract, offerShift = 0, deptChoice;
        float offerWage;
        GUIStyle cellStyle;

        static readonly string[] HireRoles = { "Cashier", "Stocker", "Cleaner", "Baker", "Deli Clerk", "Asst. Mgr" };

        void EnsureStaffStyles()
        {
            if (cellStyle != null) return;
            cellStyle = new GUIStyle(GUI.skin.button) { richText = true, fontStyle = FontStyle.Bold };
        }

        static string SlotColor(ShiftSlot s)
        {
            switch (s)
            {
                case ShiftSlot.Morning: return "#ffd166";
                case ShiftSlot.Evening: return "#7fc8ff";
                case ShiftSlot.Night: return "#c79bff";
                default: return "#6b7680";
            }
        }

        string StatusText(Employee e)
        {
            if (e.OnShift) return e.Slacking ? "<color=#ff9966>" + e.Status + "</color>" : e.OnBreak ? "<color=#9fd3ff>On break</color>" : e.Status;
            if (e.ArriveAt >= 0f) return "<color=#ffcc55>Late, not in yet</color>";
            if (e.Absent) return "<color=#ff7766>" + e.Status + "</color>";
            var today = e.Week[store.Staff.Today];
            return today == ShiftSlot.Off ? "<color=#888888>Day off</color>" : "Off duty (" + SlotNames[(int)today].ToLower() + " today)";
        }

        // ------------------------------------------------------------------ Staff app

        void StaffApp(PcWindow w, float width, float height)
        {
            EnsureStaffStyles();
            var staff = store.Staff;
            float wages = 0f;
            for (int i = 0; i < staff.All.Count; i++)
            {
                var e = staff.All[i];
                if (e != null) wages += e.Wage * 8f * e.ScheduledShifts * (e.Week[0] == ShiftSlot.Night ? 1.05f : 1f);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(staff.All.Count + " staff, about " + Money(wages) + " a week in wages.", rich, W(330));
            GUILayout.Label("Date checks", bold, W(90));
            store.DateChecks = (DateCheckPolicy)GUILayout.Toolbar((int)store.DateChecks, new[] { "Only when restocking", "Once a day", "Every shift" }, W(400));
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            // list
            GUILayout.BeginVertical(W(400f));
            w.Scroll = GUILayout.BeginScrollView(w.Scroll);
            var list = staff.All.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == null) continue;
                bool sel = staffSel == e;
                string flags = (staff.ReviewDue(e) ? " <color=#ffcc55>review</color>" : "") + (e.OnProbation ? " <color=#9fd3ff>prob.</color>" : "")
                    + (e.WarnableIncident != null ? " <color=#ff9966>!</color>" : "") + (e.ActiveWrittenWarnings > 0 ? " <color=#ff7766>W" + e.ActiveWrittenWarnings + "</color>" : "");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(sel, e.DisplayName, "Button", W(150)) && !sel) { staffSel = e; deptChoice = 0; }
                GUILayout.Label("<size=11>" + Catalog.RoleName(e.Role) + (e.Rank != Rank.Staff ? " · " + (e.Rank == Rank.ShiftLead ? "Lead" : "Dept") : "")
                    + " · " + (e.Contract == Contract.FullTime ? "FT" : "PT") + flags + "</size>", rich, W(140));
                GUILayout.Label("<size=11>" + StatusText(e) + "</size>", rich, W(100));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical("box");
            w.Scroll2 = GUILayout.BeginScrollView(w.Scroll2);
            if (staffSel == null || staffSel.Leaving || !staff.All.Contains(staffSel)) GUILayout.Label("Pick someone on the left.", small);
            else StaffProfile(staffSel);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void StaffProfile(Employee e)
        {
            var staff = store.Staff;
            GUILayout.Label(e.DisplayName + "  <size=12>" + Catalog.RoleName(e.Role) + (e.RankName.Length > 0 ? ", " + e.RankName : "")
                + ", " + (e.Contract == Contract.FullTime ? "full-time (5 shifts)" : "part-time (3 shifts)") + "</size>", header);
            GUILayout.Label("With you " + e.Tenure + " days" + (e.OnProbation ? " · <color=#9fd3ff>probation until day " + e.ProbationEnds + "</color>" : "")
                + " · next review day " + e.NextReviewDay + " · " + StatusText(e), rich);
            GUILayout.Label("Morale " + MoraleText(e.Morale) + (e.MoraleNote.Length > 0 ? "  <size=11>(" + e.MoraleNote + ")</size>" : ""), rich);
            GUILayout.Label("Wage $" + e.Wage.ToString("0") + "/h (asked $" + e.AskingWage.ToString("0") + ")" + "   Can work: " + Availability(e), rich);
            GUILayout.Label(SkillText(e) + "  Ambition " + e.Known(5) + (e.ShiftsWorked < 10 ? "   <i>(ranges from the interview; you'll know for sure after 10 shifts)</i>" : ""), small);
            var week = "";
            for (int d = 0; d < 7; d++) week += StaffManager.DayNames[d] + " <color=" + SlotColor(e.Week[d]) + ">" + StaffManager.SlotShort[(int)e.Week[d]] + "</color>  ";
            GUILayout.Label(week + " (" + e.ScheduledShifts + " of " + e.ContractShifts + ")  <size=11>edit in Roster</size>", rich);

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Role: " + Catalog.RoleName(e.Role), W(150)) && e.Role != Role.AssistantManager) staff.SetRole(e, NextWorkRole(e));
            GUI.enabled = e.Role == Role.Stocker && e.Rank != Rank.DeptManager;
            if (GUILayout.Button("Restocks: " + (e.AssignedCategory != null ? e.AssignedCategory.Name : "all"), W(170))) staff.SetCategory(e, NextCategory(e));
            GUI.enabled = !e.OnShift && e.ArriveAt < 0f;
            if (GUILayout.Button("Call in now", W(100))) Toast(staff.CallIn(e));
            GUI.enabled = true;
            if (GUILayout.Button("Raise $1", W(80))) { e.GiveRaise(1f); Toast(e.DisplayName + " now earns $" + e.Wage.ToString("0") + "/h."); }
            GUILayout.EndHorizontal();

            // Review
            GUILayout.Space(6f);
            string summary;
            int rating = staff.Rating(e, out summary);
            if (staff.ReviewDue(e))
            {
                GUILayout.Label("<b>Performance review due</b>  rating " + Stars(rating) + "  <size=11>" + summary + "</size>", rich);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("No raise", W(90))) Toast(staff.Review(e, 0f));
                if (GUILayout.Button("Raise $1", W(90))) Toast(staff.Review(e, 1f));
                if (GUILayout.Button("Raise $2", W(90))) Toast(staff.Review(e, 2f));
                GUILayout.Label(rating >= 4 ? "<size=11>They expect a raise.</size>" : "", rich);
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Performance so far " + Stars(rating) + "  <size=11>" + summary + "</size>", rich);

            // Career
            GUILayout.Space(6f);
            string next = staff.NextRankName(e);
            if (next != null)
            {
                Category dept = null;
                if (e.Rank == Rank.ShiftLead)
                {
                    var cats = new List<Category>();
                    var names = new List<string>();
                    for (int i = 0; i < store.Categories.Count; i++)
                    {
                        var c = store.Categories[i];
                        bool any = false;
                        for (int k = 0; k < c.Products.Count; k++) if (c.Products[k].Carried) any = true;
                        if (any) { cats.Add(c); names.Add(c.Name); }
                    }
                    if (cats.Count > 0)
                    {
                        deptChoice = Mathf.Clamp(deptChoice, 0, cats.Count - 1);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Department", W(80));
                        deptChoice = GUILayout.Toolbar(deptChoice, names.ToArray());
                        GUILayout.EndHorizontal();
                        dept = cats[deptChoice];
                    }
                }
                string why = staff.PromoteProblem(e, dept);
                GUILayout.BeginHorizontal();
                GUI.enabled = why == null;
                if (GUILayout.Button("Promote to " + next, W(260))) Toast(staff.Promote(e, dept));
                GUI.enabled = true;
                GUILayout.Label(why != null ? "<size=11><color=#ff9977>" + why + "</color></size>" : "<size=11>Pay goes up; morale up.</size>", rich);
                GUILayout.EndHorizontal();
            }
            if (e.Rank == Rank.DeptManager)
                e.OrdersForDepartment = GUILayout.Toggle(e.OrdersForDepartment, " Orders stock for " + (e.Department != null ? e.Department.Name : "their department") + " before their shift ends");
            if (e.IsLead) GUILayout.Label("<size=11>Leads keep staff around them working (less slacking) and a little faster.</size>", rich);
            if (e.IsLead && GUILayout.Button("Demote", W(90))) Toast(staff.Demote(e));

            // Discipline
            GUILayout.Space(6f);
            var inc = e.WarnableIncident;
            GUILayout.BeginHorizontal();
            GUI.enabled = inc != null;
            if (GUILayout.Button("Written warning", W(140))) Toast(e.WrittenWarning());
            GUI.enabled = true;
            GUILayout.Label(inc != null ? "<size=11>For: " + inc.Note.ToLower() + " (day " + inc.Day + ")</size>" : "<size=11>Nothing recent to give a written warning for.</size>", rich, W(260));
            string fireWhy = staff.FireProblem(e);
            bool confirming = confirmFire == e && Time.unscaledTime - confirmTime < 4f;
            if (GUILayout.Button(confirming ? "Sure?" : e.OnProbation ? "End probation" : "Dismiss", W(110)))
            {
                if (confirming) { staff.Fire(e); confirmFire = null; staffSel = null; GUILayout.EndHorizontal(); return; }
                confirmFire = e;
                confirmTime = Time.unscaledTime;
            }
            GUILayout.EndHorizontal();
            if (confirming && fireWhy != null) GUILayout.Label("<color=#ff7766>" + fireWhy + "</color>", small);

            // Record and time clock
            GUILayout.Space(6f);
            GUILayout.Label("Record", bold);
            int shown = 0;
            for (int i = e.Record.Count - 1; i >= 0 && shown < 10; i--, shown++)
            {
                var r = e.Record[i];
                string col = r.Kind == IncidentKind.WrittenWarning || r.Kind == IncidentKind.CaughtSlacking || r.Kind == IncidentKind.NoShow ? "#ff9977"
                    : r.Kind == IncidentKind.Late || r.Kind == IncidentKind.LongBreak || r.Kind == IncidentKind.VerbalWarning ? "#ffd166"
                    : r.Kind == IncidentKind.Promotion || r.Kind == IncidentKind.Praise || r.Kind == IncidentKind.Raise ? "#9dff9d" : "#cfd8e0";
                GUILayout.Label("<size=11>Day " + r.Day + " " + r.Time + "  <color=" + col + ">" + r.Note + "</color>" + (r.Addressed ? " (warned)" : "") + "</size>", rich);
            }
            GUILayout.Space(4f);
            GUILayout.Label("Time clock and till (last shifts)", bold);
            for (int i = e.Reports.Count - 1, k = 0; i >= 0 && k < 7; i--, k++)
            {
                var r = e.Reports[i];
                float perHour = r.HoursPaid > 0.1f ? r.Output / r.HoursPaid : 0f;
                GUILayout.Label("<size=11>Day " + r.Day + " " + SlotNames[(int)r.Slot] + ":  " + r.HoursPaid.ToString("0.0") + " h paid"
                    + (r.LateMinutes > 0 ? ", <color=#ffd166>" + r.LateMinutes + " min late</color>" : "")
                    + ", break " + r.BreakMinutes + " min" + (r.BreakMinutes > 35 ? " <color=#ffd166>(long)</color>" : "")
                    + ", busy " + Mathf.RoundToInt(r.BusyShare * 100f) + "%, " + OutputText(e.Role, perHour) + "</size>", rich);
            }
            if (e.Reports.Count == 0) GUILayout.Label("No shifts yet.", small);
        }

        static string Stars(int r) { return "<color=#ffd166>" + new string('*', r) + "</color><color=#555555>" + new string('*', 5 - r) + "</color>"; }

        static string OutputText(Role r, float perHour)
        {
            switch (r)
            {
                case Role.Cashier: return Mathf.RoundToInt(perHour) + " items scanned/h";
                case Role.Stocker: return Mathf.RoundToInt(perHour) + " units stocked/h";
                case Role.Baker: return Mathf.RoundToInt(perHour) + " baked/h";
                case Role.DeliClerk: return perHour.ToString("0.0") + " customers served/h";
                case Role.Cleaner: return (perHour / 10f).ToString("0.0") + " spills cleaned/h";
                default: return "";
            }
        }

        static string Availability(Employee e)
        {
            var l = new List<string>();
            if (e.NoMornings) l.Add("no weekday mornings");
            if (e.NoNights) l.Add("no nights");
            if (e.NoWeekends) l.Add("no weekends");
            return l.Count == 0 ? "any shift" : string.Join(", ", l.ToArray());
        }

        Role NextWorkRole(Employee e)
        {
            Role r = e.Role;
            for (int i = 0; i < 5; i++)
            {
                r = (Role)(((int)r + 1) % 5);
                if (store.RoleLock(r) == null) return r;
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

        // ------------------------------------------------------------------ Roster app

        void RosterApp(PcWindow w, float width, float height)
        {
            EnsureStaffStyles();
            var staff = store.Staff;
            int today = staff.Today;
            GUILayout.Label("Weekly roster. It repeats every week; changes to today take effect now. Click a cell to cycle Off / Morning 07-15 / Evening 14-22 / Night 22-06 (+25%). "
                + "Shifts over the contract are overtime (x1.5). Less than 11 hours between shifts means tired staff.", small);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Name</b>", rich, W(150));
            GUILayout.Label("<b>Role</b>", rich, W(86));
            for (int d = 0; d < 7; d++)
                GUILayout.Label(d == today ? "<b><color=#9fe3ff>[" + StaffManager.DayNames[d] + "]</color></b>" : "<b>" + StaffManager.DayNames[d] + "</b>", rich, W(52));
            GUILayout.Label("<b>Shifts</b>", rich, W(70));
            GUILayout.Label("<b>Notes</b>", rich);
            GUILayout.EndHorizontal();

            w.Scroll = GUILayout.BeginScrollView(w.Scroll, GUILayout.Height(Mathf.Max(180f, height - 260f)));
            var list = staff.All.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var e = list[i];
                if (e == null || e.Leaving) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(e.DisplayName + (e.Contract == Contract.PartTime ? " <size=10>PT</size>" : ""), rich, W(150));
                GUILayout.Label("<size=11>" + Catalog.RoleName(e.Role) + "</size>", rich, W(86));
                var notes = new List<string>();
                for (int d = 0; d < 7; d++)
                {
                    var s = e.Week[d];
                    bool shortRest = e.ShortRestOn(d);
                    string label = "<color=" + SlotColor(s) + ">" + StaffManager.SlotShort[(int)s] + "</color>" + (shortRest ? "<color=#ff7766>!</color>" : "");
                    if (GUILayout.Button(label, cellStyle, W(52), GUILayout.Height(24f))) staff.CycleDay(e, d);
                    if (shortRest) notes.Add("short rest " + StaffManager.DayNames[d]);
                }
                int n = e.ScheduledShifts;
                string col = n > e.ContractShifts ? "#ffd166" : n < e.ContractShifts ? "#9fd3ff" : "#9dff9d";
                GUILayout.Label("<color=" + col + ">" + n + "/" + e.ContractShifts + "</color>", rich, W(70));
                if (n > e.ContractShifts) notes.Add((n - e.ContractShifts) + " overtime");
                if (n >= 7) notes.Add("no day off");
                if (n < e.ContractShifts) notes.Add("wants more hours");
                string can = Availability(e);
                if (can != "any shift") notes.Add(can);
                GUILayout.Label("<size=11>" + string.Join(", ", notes.ToArray()) + "</size>", rich);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            // Coverage
            GUILayout.Space(6f);
            GUILayout.Label("Coverage (who's rostered)", bold);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", W(236));
            for (int d = 0; d < 7; d++) GUILayout.Label("<b>" + StaffManager.DayNames[d] + "</b>", rich, W(52));
            GUILayout.EndHorizontal();
            Role[] roles = { Role.Cashier, Role.Stocker, Role.Cleaner, Role.Baker, Role.DeliClerk };
            for (int ri = 0; ri < roles.Length; ri++)
            {
                var r = roles[ri];
                if (!staff.AnyScheduled(r) && r != Role.Cashier && r != Role.Stocker) continue;
                for (int si = 1; si <= 3; si++)
                {
                    var slot = (ShiftSlot)si;
                    int total = 0;
                    for (int d = 0; d < 7; d++) total += staff.Coverage(r, d, slot);
                    bool needed = (r == Role.Cashier && slot != ShiftSlot.Night) || (r == Role.Stocker && slot == ShiftSlot.Morning);
                    if (total == 0 && !needed) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<size=11>" + Catalog.RoleName(r) + "s, " + SlotNames[si].ToLower() + "</size>", rich, W(236));
                    for (int d = 0; d < 7; d++)
                    {
                        int c = staff.Coverage(r, d, slot);
                        string cc = c == 0 ? (needed ? "#ff6655" : "#6b7680") : "#e8eef2";
                        GUILayout.Label("<color=" + cc + ">" + c + "</color>", rich, W(52));
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Label("<size=11>Red = nobody rostered when the store needs them (cashiers while open, a stocker in the morning). Remember breaks: two cashiers on a shift keep a till open during breaks.</size>", rich);
        }

        // ------------------------------------------------------------------ Hiring app

        void HiringApp(PcWindow w, float width, float height)
        {
            var staff = store.Staff;
            GUILayout.Label("Applicants come and go every few days. Their real skills are hidden: interview them (" + staff.InterviewsToday + "/" + Interview.MaxPerDay
                + " interviews today, " + Interview.MaxQuestions + " questions each) and call references. Smooth talkers sound better than they are; references don't lie.", small);
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(W(360f));
            w.Scroll = GUILayout.BeginScrollView(w.Scroll);
            var list = staff.Applicants.ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                var a = list[i];
                bool sel = appSel == a;
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(sel, a.Name, "Button", W(150)) && !sel) { appSel = a; offerWage = a.AskingWage; offerContract = a.WantsPartTime ? 1 : 0; }
                GUILayout.Label("<size=11>" + a.Age + " y, asks $" + a.AskingWage.ToString("0") + (a.Interviewed ? " · <color=#9dff9d>interviewed</color>" : "")
                    + (a.ReferencesIn ? " · refs" : "") + "</size>", rich, W(190));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10f);
            GUILayout.BeginVertical("box");
            w.Scroll2 = GUILayout.BeginScrollView(w.Scroll2);
            if (appSel == null || !staff.Applicants.Contains(appSel)) GUILayout.Label("Pick an applicant on the left.", small);
            else ApplicantPanel(appSel);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        void ApplicantPanel(Applicant a)
        {
            var staff = store.Staff;
            GUILayout.Label(a.Name + "  <size=12>asks $" + a.AskingWage.ToString("0") + "/h</size>", header);
            GUILayout.Label(a.Cv, rich);
            var est = "";
            for (int t = 0; t < Applicant.TraitCount; t++) est += Applicant.TraitNames[t] + " <b>" + a.Estimate(t) + "</b>   ";
            GUILayout.Label(est, rich);

            GUILayout.Space(6f);
            GUILayout.Label("Interview  <size=11>(" + (Interview.MaxQuestions - a.Asked.Count) + " questions left)</size>", bold);
            bool can = staff.CanInterview(a) && a.Asked.Count < Interview.MaxQuestions;
            if (!staff.CanInterview(a)) GUILayout.Label("<color=#ffcc55>No time for more interviews today.</color>", small);
            for (int q = 0; q < Interview.Questions.Length; q++)
            {
                if (a.Asked.Contains(q)) continue;
                GUI.enabled = can;
                if (GUILayout.Button(Interview.Questions[q].Text)) staff.Ask(a, q);
                GUI.enabled = true;
            }
            GUI.enabled = !a.ReferencesRequested;
            if (GUILayout.Button(a.ReferencesRequested ? (a.ReferencesIn ? "References checked" : "References: waiting for a call back") : "Call their references (answer tomorrow)"))
                staff.RequestReferences(a);
            GUI.enabled = true;
            for (int i = 0; i < a.Transcript.Count; i++) GUILayout.Label(a.Transcript[i], rich);

            GUILayout.Space(8f);
            GUILayout.Label("Job offer", bold);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Role", W(60));
            offerRole = GUILayout.Toolbar(offerRole, HireRoles);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Contract", W(60));
            offerContract = GUILayout.Toolbar(offerContract, new[] { "Full-time (5 shifts)", "Part-time (3 shifts)" }, W(320));
            GUILayout.Label("Usual shift", W(70));
            offerShift = GUILayout.Toolbar(offerShift, ShiftNames, W(240));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Wage", W(60));
            if (GUILayout.Button("-", W(26))) offerWage = Mathf.Max(10f, offerWage - 1f);
            GUILayout.Label("<b>$" + offerWage.ToString("0") + "/h</b>", rich, W(60));
            if (GUILayout.Button("+", W(26))) offerWage += 1f;
            float chance = Interview.AcceptChance(a, offerWage);
            GUILayout.Label("<size=11>" + (chance >= 1f ? "they'll accept" : Mathf.RoundToInt(chance * 100f) + "% chance they accept") + "; pay above asking starts them happier</size>", rich, W(330));
            var role = (Role)offerRole;
            var contract = (Contract)offerContract;
            string why = staff.OfferProblem(a, role, contract);
            GUI.enabled = why == null;
            if (GUILayout.Button("Make offer", W(110)))
            {
                string result;
                var e = staff.Offer(a, role, contract, (ShiftSlot)(offerShift + 1), offerWage, out result);
                Toast(result);
                if (e != null) { appSel = null; staffSel = e; }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (why != null) GUILayout.Label("<color=#ff9977>" + why + "</color>", small);
            GUILayout.Label("<size=11>New staff start on " + StaffManager.ProbationDays + " days' probation: you can let them go without warnings until then. Edit their days in Roster.</size>", rich);
        }
    }
}
