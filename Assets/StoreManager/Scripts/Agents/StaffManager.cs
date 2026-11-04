using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public static class Names
    {
        static readonly string[] First =
        {
            "Anna", "Ben", "Clara", "Daniel", "Elin", "Felix", "Greta", "Hugo", "Ida", "Jonas",
            "Kara", "Leo", "Maja", "Nils", "Olivia", "Pétur", "Rosa", "Sam", "Tara", "Viktor",
            "Wendy", "Yusuf", "Zoe", "Arnar", "Birna", "Carlos", "Dina", "Emil", "Freya", "Gunnar",
            "Hanna", "Ivan", "Julia", "Kevin", "Lena", "Marco", "Nora", "Omar", "Priya", "Ragnar"
        };
        static readonly string[] Last =
        {
            "Berg", "Carter", "Dahl", "Evans", "Fischer", "Garcia", "Hansen", "Ito", "Jensen", "Kowalski",
            "Larsen", "Moreno", "Nowak", "Olsen", "Park", "Quinn", "Rossi", "Sato", "Torres", "Ullman",
            "Vidal", "Walsh", "Young", "Jónsson", "Sigurdsdóttir", "Novak", "Brennan", "Khan", "Lund", "Meyer"
        };

        public static string Random()
        {
            return First[UnityEngine.Random.Range(0, First.Length)] + " " + Last[UnityEngine.Random.Range(0, Last.Length)];
        }
    }

    /// <summary>Roster, hiring, time clock, reviews, promotions and payroll.</summary>
    public class StaffManager
    {
        public readonly List<Employee> All = new List<Employee>();
        public readonly List<Applicant> Applicants = new List<Applicant>();
        public int InterviewsToday;
        readonly Store store;

        public StaffManager(Store store) { this.store = store; }

        public const int ProbationDays = 14;
        public const int ReviewEvery = 30;
        public const float LeadRaise = 2.5f, DeptRaise = 2.5f, AssistantRaise = 3f;

        public Employee Assistant
        {
            get
            {
                for (int i = 0; i < All.Count; i++)
                    if (All[i] != null && All[i].Role == Role.AssistantManager && !All[i].Leaving) return All[i];
                return null;
            }
        }

        public int Today { get { return (store.Clock.Day - 1) % 7; } }

        public static int ShiftStart(Shift s) { return s == Shift.Morning ? 7 : s == Shift.Evening ? 14 : 22; }
        public static int ShiftEnd(Shift s) { return s == Shift.Morning ? 15 : s == Shift.Evening ? 22 : 6; }
        public static Shift ToShift(ShiftSlot s) { return s == ShiftSlot.Evening ? Shift.Evening : s == ShiftSlot.Night ? Shift.Night : Shift.Morning; }
        public static ShiftSlot ToSlot(Shift s) { return s == Shift.Evening ? ShiftSlot.Evening : s == Shift.Night ? ShiftSlot.Night : ShiftSlot.Morning; }
        public static string ShiftName(Shift s)
        {
            return s == Shift.Morning ? "Morning 07-15" : s == Shift.Evening ? "Evening 14-22" : "Night 22-06";
        }
        public static readonly string[] SlotShort = { "-", "M", "E", "N" };
        public static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public static bool ShiftActiveAt(Shift s, int hour)
        {
            int a = ShiftStart(s), b = ShiftEnd(s);
            return a < b ? hour >= a && hour < b : hour >= a || hour < b;
        }

        /// <summary>Today's slot for this employee, or yesterday's night shift if that's still running.</summary>
        public ShiftSlot SlotNow(Employee e)
        {
            int h = store.Clock.Hour;
            if (h < 6 && e.Week[(Today + 6) % 7] == ShiftSlot.Night) return ShiftSlot.Night;
            var s = e.Week[Today];
            return s != ShiftSlot.Off && ShiftActiveAt(ToShift(s), h) ? s : ShiftSlot.Off;
        }

        // ------------------------------------------------------------------ clock

        public void Tick()
        {
            float now = store.Clock.AbsMinutes;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e != null && e.ArriveAt >= 0f && now >= e.ArriveAt && !e.Leaving) e.ArriveLate();
            }
        }

        public void OnHour(int hour)
        {
            var copy = All.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                var e = copy[i];
                if (e == null) continue;
                if ((e.OnShift || e.ArriveAt >= 0f) && hour == ShiftEnd(e.Shift) && !e.ShiftEnding) e.EndShift();
            }
            for (int i = 0; i < copy.Length; i++)
            {
                var e = copy[i];
                if (e == null || e.Leaving || e.OnShift || e.ArriveAt >= 0f) continue;
                var slot = e.Week[Today];
                if (slot != ShiftSlot.Off && hour == ShiftStart(ToShift(slot))) e.StartShift(ToShift(slot), true);
            }
            if (hour == 0) OnNewDay();
        }

        void OnNewDay()
        {
            int day = store.Clock.Day;
            InterviewsToday = 0;
            if (Today == 0) for (int i = 0; i < All.Count; i++) if (All[i] != null) All[i].ShiftsThisWeek = 0;
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e == null) continue;
                if (day == e.ProbationEnds)
                {
                    e.AddRecord(IncidentKind.ProbationPassed, "Passed probation");
                    store.Message(e.DisplayName + " passed their probation. From now on, dismissal needs two written warnings.");
                }
                if (day == e.NextReviewDay) store.Message(e.DisplayName + " is due a performance review (Staff app).");
            }
            for (int i = 0; i < Applicants.Count; i++)
            {
                var a = Applicants[i];
                if (a.ReferencesRequested && !a.ReferencesIn && day > a.ReferencesDay)
                {
                    Interview.References(a);
                    store.Message("References came back for " + a.Name + " (Hiring app).");
                }
            }
        }

        // ------------------------------------------------------------------ roster

        public void SetDay(Employee e, int dow, ShiftSlot slot)
        {
            if (!e.CanWork(dow, slot)) return;
            e.Week[dow] = slot;
            if (dow != Today) return;
            // today: start or end a shift that's running now
            var now = SlotNow(e);
            if (e.OnShift && slot != ToSlot(e.Shift) && !(e.Shift == Shift.Night && store.Clock.Hour < 6)) e.EndShift();
            else if (!e.OnShift && e.ArriveAt < 0f && slot != ShiftSlot.Off && now == slot) e.StartShift(ToShift(slot), false);
        }

        /// <summary>Cycles a roster cell: off, morning, evening, night (skipping what they can't work).</summary>
        public void CycleDay(Employee e, int dow)
        {
            var s = e.Week[dow];
            for (int k = 0; k < 4; k++)
            {
                s = (ShiftSlot)(((int)s + 1) % 4);
                if (e.CanWork(dow, s)) { SetDay(e, dow, s); return; }
            }
        }

        /// <summary>Default week for a new contract: full-time on weekdays, part-time at the weekend.</summary>
        public void DefaultWeek(Employee e, ShiftSlot main)
        {
            for (int d = 0; d < 7; d++) e.Week[d] = ShiftSlot.Off;
            int[] days = e.Contract == Contract.FullTime ? new[] { 0, 1, 2, 3, 4 } : e.NoWeekends ? new[] { 0, 1, 2 } : new[] { 4, 5, 6 };
            for (int i = 0; i < days.Length; i++)
            {
                int d = days[i];
                var s = main;
                if (!e.CanWork(d, s)) s = e.CanWork(d, ShiftSlot.Evening) ? ShiftSlot.Evening : e.CanWork(d, ShiftSlot.Morning) ? ShiftSlot.Morning : ShiftSlot.Off;
                e.Week[d] = s;
            }
        }

        /// <summary>How many people of a role are rostered for a day and shift.</summary>
        public int Coverage(Role r, int dow, ShiftSlot slot)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && !All[i].Leaving && All[i].Role == r && All[i].Week[dow] == slot) n++;
            return n;
        }

        /// <summary>Calls an off-duty employee in now (overtime if over contract; they're not happy on a day off).</summary>
        public string CallIn(Employee e)
        {
            if (e.OnShift || e.ArriveAt >= 0f) return e.DisplayName + " is already working.";
            int h = store.Clock.Hour;
            Shift s = h >= 22 || h < 6 ? Shift.Night : h >= 14 ? Shift.Evening : Shift.Morning;
            if (!e.CanWork(Today, ToSlot(s))) return e.DisplayName + " can't work " + ShiftName(s).ToLower() + " shifts.";
            bool dayOff = e.Week[Today] == ShiftSlot.Off;
            e.Week[Today] = ToSlot(s);
            e.StartShift(s, false);
            if (dayOff) e.Morale = Mathf.Max(0f, e.Morale - 4f);
            return "Called " + e.DisplayName + " in for the " + s.ToString().ToLower() + " shift" + (dayOff ? " on their day off" : "") + ".";
        }

        // ------------------------------------------------------------------ queries

        public int OnShiftCount(Role r)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Role == r && All[i].OnShift) n++;
            return n;
        }

        public bool AnyOnShift(Role r)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e != null && e.Role == r && e.OnShift && !e.ShiftEnding) return true;
            }
            return false;
        }

        public bool AnyScheduled(Role r)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Role == r && !All[i].Leaving) return true;
            return false;
        }

        // ------------------------------------------------------------------ hiring

        public bool CanInterview(Applicant a)
        {
            return a.Interviewed || InterviewsToday < Interview.MaxPerDay;
        }

        public void Ask(Applicant a, int q)
        {
            if (!a.Interviewed)
            {
                if (InterviewsToday >= Interview.MaxPerDay) return;
                InterviewsToday++;
            }
            Interview.Ask(a, q);
        }

        public void RequestReferences(Applicant a)
        {
            if (a.ReferencesRequested) return;
            a.ReferencesRequested = true;
            a.ReferencesDay = store.Clock.Day;
            a.Transcript.Add("<i>You asked for references. They'll call back tomorrow morning.</i>");
        }

        /// <summary>Why this offer won't work, or null.</summary>
        public string OfferProblem(Applicant a, Role role, Contract c)
        {
            if (store.RoleLock(role) != null) return store.RoleLock(role) + ".";
            if (role == Role.AssistantManager && Assistant != null) return "You already have an assistant manager.";
            if (c == Contract.FullTime && a.WantsPartTime) return a.Name + " only wants part-time work.";
            if (c == Contract.PartTime && a.WantsFullTime) return a.Name + " only wants a full-time job.";
            if (a.DeclinedDay == store.Clock.Day) return a.Name + " turned down an offer today. Try again tomorrow.";
            return null;
        }

        /// <summary>Makes a job offer. Returns the new employee, or null if they said no.</summary>
        public Employee Offer(Applicant a, Role role, Contract c, ShiftSlot main, float wage, out string result)
        {
            result = OfferProblem(a, role, c);
            if (result != null) return null;
            if (Random.value > Interview.AcceptChance(a, wage))
            {
                a.DeclinedDay = store.Clock.Day;
                result = a.Name + " turned down $" + wage.ToString("0") + "/h (asked $" + a.AskingWage.ToString("0") + ").";
                return null;
            }
            var e = Hire(a, role, c, main, null, wage);
            result = "Hired " + e.DisplayName + " as " + Catalog.RoleName(role) + ", " + (c == Contract.FullTime ? "full-time" : "part-time")
                + ", $" + wage.ToString("0") + "/h. " + ProbationDays + " days' probation.";
            return e;
        }

        public Employee Hire(Applicant a, Role role, Contract contract, ShiftSlot main, Category category, float wage)
        {
            if (role == Role.AssistantManager && Assistant != null) role = Role.Stocker;
            var go = new GameObject("Staff " + a.Name);
            var e = go.AddComponent<Employee>();
            e.DisplayName = a.Name;
            e.Role = role;
            e.Contract = contract;
            e.AssignedCategory = role == Role.Stocker ? category : null;
            e.Speed = a.Speed;
            e.Service = a.Service;
            e.Reliability = a.Reliability;
            e.Leadership = a.Leadership;
            e.WorkEthic = a.WorkEthic;
            e.Ambition = a.Ambition;
            e.Charm = a.Charm;
            e.Specialty = a.Specialty;
            e.NoNights = a.NoNights;
            e.NoMornings = a.NoMornings;
            e.NoWeekends = a.NoWeekends;
            e.AskingWage = a.AskingWage;
            e.Wage = wage;
            e.Morale = Mathf.Clamp(62f + (wage / Mathf.Max(1f, a.AskingWage) - 1f) * 60f, 30f, 85f);
            e.HiredDay = store.Clock.Day;
            e.ProbationEnds = e.HiredDay + ProbationDays;
            e.NextReviewDay = e.HiredDay + ReviewEvery;
            for (int t = 0; t < Applicant.TraitCount; t++) { e.Min[t] = a.Min[t]; e.Max[t] = a.Max[t]; }
            DefaultWeek(e, main);
            e.Shift = ToShift(main);
            e.AddRecord(IncidentKind.Hired, "Hired as " + Catalog.RoleName(role) + " (" + (contract == Contract.FullTime ? "full-time" : "part-time") + ")");
            e.BuildBody(Employee.RoleColor(role), new Color(0.93f, 0.78f, 0.65f), 1.6f);
            All.Add(e);
            Applicants.Remove(a);

            var now = SlotNow(e);
            if (now != ShiftSlot.Off) e.StartShift(ToShift(now), false);
            else go.SetActive(false);
            return e;
        }

        public void Remove(Employee e)
        {
            All.Remove(e);
        }

        // ------------------------------------------------------------------ dismissal

        /// <summary>Null if you can let them go fairly; otherwise why it would be unfair dismissal.</summary>
        public string FireProblem(Employee e)
        {
            if (e.OnProbation) return null;
            if (e.ActiveWrittenWarnings >= 2) return null;
            return "Past probation with " + e.ActiveWrittenWarnings + " written warning" + (e.ActiveWrittenWarnings == 1 ? "" : "s")
                + " (2 needed): unfair dismissal. Costs " + Economy.Money(UnfairCost(e)) + " compensation and everyone's morale.";
        }

        public float UnfairCost(Employee e) { return Mathf.Round(e.Wage * 80f); }

        public void Fire(Employee e)
        {
            if (e == null || e.Leaving) return;
            bool unfair = FireProblem(e) != null;
            if (unfair)
            {
                store.Economy.PayWages(UnfairCost(e));
                store.Message("You dismissed " + e.DisplayName + " without proper warnings: " + Economy.Money(UnfairCost(e)) + " compensation. The staff are upset.");
            }
            else store.Message(e.OnProbation ? "You ended " + e.DisplayName + "'s employment during probation." : "You dismissed " + e.DisplayName + ".");
            All.Remove(e);
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null) All[i].Morale = Mathf.Max(0f, All[i].Morale - (unfair ? 8f : 2f));
            e.Dismiss();
        }

        // ------------------------------------------------------------------ reviews and promotions

        public bool ReviewDue(Employee e) { return store.Clock.Day >= e.NextReviewDay; }

        /// <summary>1-5, from the time clock, the till and what you've seen since the last review.</summary>
        public int Rating(Employee e, out string summary)
        {
            int from = e.NextReviewDay - ReviewEvery;
            int shifts = 0, late = 0, lng = 0, caught = 0, noShow = 0, sick = 0;
            float busy = 0f;
            for (int i = 0; i < e.Reports.Count; i++)
            {
                if (e.Reports[i].Day < from) continue;
                shifts++;
                busy += e.Reports[i].BusyShare;
            }
            for (int i = 0; i < e.Record.Count; i++)
            {
                var r = e.Record[i];
                if (r.Day < from) continue;
                if (r.Kind == IncidentKind.Late) late++;
                else if (r.Kind == IncidentKind.LongBreak) lng++;
                else if (r.Kind == IncidentKind.CaughtSlacking) caught++;
                else if (r.Kind == IncidentKind.NoShow) noShow++;
                else if (r.Kind == IncidentKind.Sick) sick++;
            }
            busy = shifts > 0 ? busy / shifts : 0.5f;
            float score = 3f + (busy - 0.6f) * 4f - late * 0.3f - lng * 0.3f - caught * 0.6f - noShow * 1f - Mathf.Max(0, sick - 2) * 0.3f
                + (e.Service - 5f) * 0.08f + (e.Speed - 5f) * 0.08f;
            summary = shifts + " shifts, busy " + Mathf.RoundToInt(busy * 100f) + "% of the time, " + late + " late, " + lng + " long breaks, "
                + caught + " caught slacking, " + noShow + " no-shows, " + sick + " sick days";
            return Mathf.Clamp(Mathf.RoundToInt(score), 1, 5);
        }

        /// <summary>Finishes a review. raise = dollars per hour (0 for none).</summary>
        public string Review(Employee e, float raise)
        {
            string summary;
            int rating = Rating(e, out summary);
            e.NextReviewDay = store.Clock.Day + ReviewEvery;
            if (raise > 0f) e.Wage += raise;
            float expected = rating >= 5 ? 2f : rating >= 4 ? 1f : 0f;
            float delta = raise >= expected ? 6f + (raise - expected) * 3f : -8f * (expected - raise);
            if (rating <= 2 && raise <= 0f) delta = -2f;
            e.Morale = Mathf.Clamp(e.Morale + delta, 0f, 100f);
            e.AddRecord(IncidentKind.Review, "Review: " + rating + "/5" + (raise > 0f ? ", raise $" + raise.ToString("0") + "/h" : ", no raise"));
            return e.DisplayName + ": rated " + rating + "/5" + (raise > 0f ? ", raise $" + raise.ToString("0") + "/h." : ", no raise.")
                + (delta < 0f ? " They expected more." : "");
        }

        /// <summary>The next step up for this employee, as text, or null at the top.</summary>
        public string NextRankName(Employee e)
        {
            if (e.Role == Role.AssistantManager) return null;
            if (e.Rank == Rank.Staff) return "Shift Lead";
            if (e.Rank == Rank.ShiftLead) return "Department Manager";
            return "Assistant Manager";
        }

        public string PromoteProblem(Employee e, Category dept)
        {
            if (e.Role == Role.AssistantManager) return "Already at the top.";
            if (e.ActiveWrittenWarnings > 0) return "Has an active written warning.";
            if (e.Rank == Rank.Staff)
            {
                if (store.Level < 2) return "Shift leads unlock at store level 2.";
                if (e.Tenure < 10) return "Needs 10 days with you (has " + e.Tenure + ").";
                return null;
            }
            if (e.Rank == Rank.ShiftLead)
            {
                if (store.Level < 5) return "Department managers unlock at store level 5.";
                if (e.Tenure < 20) return "Needs 20 days with you (has " + e.Tenure + ").";
                if (dept == null) return "Pick a department.";
                for (int i = 0; i < All.Count; i++)
                    if (All[i] != null && All[i] != e && All[i].Rank == Rank.DeptManager && All[i].Department == dept) return dept.Name + " already has a manager.";
                return null;
            }
            string l = store.RoleLock(Role.AssistantManager);
            if (l != null) return l + ".";
            if (Assistant != null) return "You already have an assistant manager.";
            return null;
        }

        public string Promote(Employee e, Category dept)
        {
            string why = PromoteProblem(e, dept);
            if (why != null) return why;
            string to;
            if (e.Rank == Rank.Staff) { e.Rank = Rank.ShiftLead; e.Wage += LeadRaise; to = "Shift Lead"; }
            else if (e.Rank == Rank.ShiftLead)
            {
                e.Rank = Rank.DeptManager;
                e.Department = dept;
                if (e.Role == Role.Stocker) e.AssignedCategory = dept;
                e.Wage += DeptRaise;
                to = dept.Name + " Department Manager";
            }
            else { e.Role = Role.AssistantManager; e.AssignedCategory = null; e.Wage += AssistantRaise; to = "Assistant Manager"; e.RefreshColor(); e.Restart(); }
            e.Morale = Mathf.Min(100f, e.Morale + 15f);
            e.PromotionWaitShifts = 0;
            e.AddRecord(IncidentKind.Promotion, "Promoted to " + to);
            return e.DisplayName + " is now " + to + " ($" + e.Wage.ToString("0") + "/h).";
        }

        public string Demote(Employee e)
        {
            if (e.Role == Role.AssistantManager) { e.Role = Role.Stocker; e.Wage -= AssistantRaise; e.RefreshColor(); e.Restart(); }
            else if (e.Rank == Rank.DeptManager) { e.Rank = Rank.ShiftLead; e.Department = null; e.Wage -= DeptRaise; }
            else if (e.Rank == Rank.ShiftLead) { e.Rank = Rank.Staff; e.Wage -= LeadRaise; }
            else return "They're already at the bottom.";
            e.Morale = Mathf.Max(0f, e.Morale - 25f);
            e.AddRecord(IncidentKind.Demotion, "Demoted");
            return "You demoted " + e.DisplayName + ". They took it badly.";
        }

        // ------------------------------------------------------------------ role changes, applicants

        public void SetRole(Employee e, Role r)
        {
            if (r == Role.AssistantManager) return;   // only by promotion
            e.Role = r;
            if (r != Role.Stocker) e.AssignedCategory = null;
            e.RefreshColor();
            e.Restart();
        }

        public void SetCategory(Employee e, Category c)
        {
            e.AssignedCategory = c;
            e.Restart();
        }

        public void RefreshApplicants(bool full)
        {
            if (full) Applicants.Clear();
            for (int i = Applicants.Count - 1; i >= 0; i--)
            {
                Applicants[i].Days++;
                if (Applicants[i].Days > 4) Applicants.RemoveAt(i);
            }
            // Before the grand opening the job ad is fresh: more people apply.
            int want = store.Opened ? 8 : 12;
            while (Applicants.Count < want) Applicants.Add(Applicant.Generate());
        }

        /// <summary>The starting crew: two full-time cashiers and a stocker on weekdays, two part-timers at the weekend.</summary>
        public void HireStartingStaff()
        {
            var a = Crew(Role.Cashier, Contract.FullTime, ShiftSlot.Morning);
            var b = Crew(Role.Cashier, Contract.FullTime, ShiftSlot.Evening);
            Crew(Role.Stocker, Contract.FullTime, ShiftSlot.Morning);
            var c = Crew(Role.Cashier, Contract.PartTime, ShiftSlot.Morning);
            var d = Crew(Role.Cashier, Contract.PartTime, ShiftSlot.Evening);
            for (int k = 0; k < 7; k++) { c.Week[k] = ShiftSlot.Off; d.Week[k] = ShiftSlot.Off; }
            c.Week[4] = ShiftSlot.Morning; c.Week[5] = ShiftSlot.Morning; c.Week[6] = ShiftSlot.Morning;
            d.Week[4] = ShiftSlot.Evening; d.Week[5] = ShiftSlot.Evening; d.Week[6] = ShiftSlot.Evening;
            // the weekday crew already knows the store
            foreach (var e in new[] { a, b, c, d }) e.ProbationEnds = e.HiredDay;
        }

        Employee Crew(Role r, Contract c, ShiftSlot main)
        {
            var a = Applicant.Generate();
            a.Specialty = Specialty.None;
            a.Leadership = Mathf.Min(a.Leadership, 5f);
            a.NoNights = true;
            a.NoMornings = a.NoWeekends = false;
            a.WantsFullTime = a.WantsPartTime = false;
            a.AskingWage = Mathf.Round(11f + (a.Speed + a.Service + a.Reliability) / 3f * 1.0f + a.Leadership * 0.3f);
            Applicants.Add(a);
            var e = Hire(a, r, c, main, null, a.AskingWage);
            e.Record.Clear();
            e.AddRecord(IncidentKind.Hired, "With the store since before you came");
            return e;
        }
    }
}
