using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace StoreManager
{
    public class Employee : Agent
    {
        public Role Role;
        public Shift Shift;
        public Category AssignedCategory;   // stockers only (null = anywhere)

        public float Speed, Service, Reliability, Leadership;
        public float WorkEthic = 5f, Ambition = 5f, Charm = 5f;   // hidden
        public Specialty Specialty;
        public float Wage, AskingWage;
        public float Morale = 65f;
        public int HiredDay;

        // Contract and roster
        public Contract Contract;
        public readonly ShiftSlot[] Week = new ShiftSlot[7];   // Mon..Sun
        public bool NoNights, NoMornings, NoWeekends;
        public int ShiftsThisWeek, ShiftsWorked;
        public bool Overtime, ShortRest;

        // Career and record
        public Rank Rank;
        public Category Department;            // department managers
        public bool OrdersForDepartment = true;
        public int ProbationEnds, NextReviewDay;
        public int WarnedDay = -99, WrittenDay = -99;
        public int PromotionWaitShifts;
        public readonly List<StaffIncident> Record = new List<StaffIncident>();
        public readonly List<ShiftReport> Reports = new List<ShiftReport>();
        // What you know about their hidden traits (narrowed by the interview; exact after a couple of weeks)
        public readonly float[] Min = new float[Applicant.TraitCount], Max = new float[Applicant.TraitCount];

        public bool OnShift, Absent, AtPost, ShiftEnding, Quit, Leaving;
        public bool OnBreak, Slacking;
        public string SlackWhat = "";
        public float ArriveAt = -1f;           // late: game minute they'll walk in
        public string Status = "Off duty";
        public string MoraleNote = "";
        public int PraisedDay = -1;
        public int LowMoraleShifts;
        public float BusyTime, ShiftTime;
        public CheckoutLane Lane;

        bool idle;
        float shiftStartAbs, plannedStartAbs;
        bool paused, tookBreak, pendingBreak, pendingSlack;
        float breakAtMin, slackTimer, slackUntil;
        int curOutput, curLate, curBreakMin;
        float curSlackMin;
        Fixture claimed;
        Spill claimedSpill;
        Customer complaintTarget;
        Fixture station;
        readonly StockPile cart = new StockPile();
        Product cartProduct;

        protected override bool StaffAccess { get { return true; } }

        public override float MoveSpeed
        {
            get { return 2.4f * (0.85f + (Speed - 1f) / 9f * 0.3f) * (ManagerNearby ? 1.1f : 1f); }
        }

        public bool ManagerNearby
        {
            get
            {
                var m = Store.I.Avatar;
                return OnShift && m != null && m.DistanceTo(transform.position) < 6f;
            }
        }

        public bool AssistantNearby
        {
            get
            {
                var am = Store.I.Staff.Assistant;
                return am != null && am != this && am.OnShift && am.DistanceTo(transform.position) < 6f;
            }
        }

        public float WorkSpeed
        {
            get
            {
                float f = 0.6f + Speed * 0.08f;
                f *= 0.7f + Morale / 200f;
                if (ShortRest) f *= 0.9f;          // tired
                if (ManagerNearby) f *= 1.15f;
                else if (LeadNearby) f *= 1.05f;
                return f;
            }
        }

        public int Tenure { get { return Store.I.Clock.Day - HiredDay; } }
        public bool OnProbation { get { return Store.I.Clock.Day < ProbationEnds; } }
        public int ContractShifts { get { return Contract == Contract.FullTime ? 5 : 3; } }
        public bool IsLead { get { return Rank != Rank.Staff || Role == Role.AssistantManager; } }
        public int ActiveWrittenWarnings
        {
            get
            {
                int n = 0, day = Store.I.Clock.Day;
                for (int i = 0; i < Record.Count; i++)
                    if (Record[i].Kind == IncidentKind.WrittenWarning && day - Record[i].Day <= 60) n++;
                return n;
            }
        }

        /// <summary>A shift lead, department manager or the assistant manager on shift within 8 m.</summary>
        public bool LeadNearby
        {
            get
            {
                var all = Store.I.Staff.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var o = all[i];
                    if (o == null || o == this || !o.OnShift || !o.IsLead) continue;
                    if (o.Rank == Rank.DeptManager && o.Department != null && o.Department == AssignedCategory) return true;
                    if (o.DistanceTo(transform.position) < 8f) return true;
                }
                return false;
            }
        }

        public bool Supervised
        {
            get
            {
                var m = Store.I.Avatar;
                bool boss = m != null && !m.AtDesk && m.DistanceTo(transform.position) < 8f;
                return boss || LeadNearby;
            }
        }

        /// <summary>Chance of slacking off in the next 20 game minutes.</summary>
        public float SlackChance
        {
            get
            {
                int day = Store.I.Clock.Day;
                float c = (10f - WorkEthic) / 10f * 0.12f;
                c *= Morale < 35f ? 1.6f : Morale > 70f ? 0.7f : 1f;
                if (day - WrittenDay <= 7) c *= 0.3f;
                else if (day - WarnedDay <= 3) c *= 0.5f;
                if (OnProbation) c *= 0.6f;
                if (Supervised) c *= 0.15f;
                return c;
            }
        }

        public string RankName
        {
            get
            {
                if (Role == Role.AssistantManager) return "Assistant Manager";
                if (Rank == Rank.DeptManager) return (Department != null ? Department.Name + " " : "") + "Dept. Manager";
                if (Rank == Rank.ShiftLead) return "Shift Lead";
                return "";
            }
        }

        public void AddRecord(IncidentKind kind, string note)
        {
            Record.Add(new StaffIncident { Day = Store.I.Clock.Day, Time = Store.I.Clock.TimeText, Kind = kind, Note = note });
            if (Record.Count > 80) Record.RemoveAt(0);
        }

        /// <summary>What you know about trait t: exact after 10 shifts, otherwise the interview range.</summary>
        public string Known(int t)
        {
            if (ShiftsWorked >= 10) return Mathf.RoundToInt(TrueTrait(t)).ToString();
            if (Min[t] <= 1f && Max[t] >= 10f) return "?";
            return Min[t] == Max[t] ? Min[t].ToString("0") : Min[t].ToString("0") + "-" + Max[t].ToString("0");
        }

        public float TrueTrait(int t)
        {
            switch (t)
            {
                case 0: return Speed;
                case 1: return Service;
                case 2: return Reliability;
                case 3: return Leadership;
                case 4: return WorkEthic;
                default: return Ambition;
            }
        }

        public bool CanWork(int dow, ShiftSlot s)
        {
            if (s == ShiftSlot.Off) return true;
            if (s == ShiftSlot.Night && NoNights) return false;
            if (s == ShiftSlot.Morning && NoMornings && dow < 5) return false;
            if (NoWeekends && dow >= 5) return false;
            return true;
        }

        public int ScheduledShifts
        {
            get { int n = 0; for (int i = 0; i < 7; i++) if (Week[i] != ShiftSlot.Off) n++; return n; }
        }

        /// <summary>Less than 11 hours off between yesterday's shift and today's.</summary>
        public bool ShortRestOn(int dow)
        {
            var prev = Week[(dow + 6) % 7];
            var cur = Week[dow];
            if (prev == ShiftSlot.Off || cur == ShiftSlot.Off) return false;
            int prevEnd = prev == ShiftSlot.Night ? 30 : StaffManager.ShiftEnd(StaffManager.ToShift(prev));
            int curStart = 24 + StaffManager.ShiftStart(StaffManager.ToShift(cur));
            return curStart - prevEnd < 11;
        }

        public void RecordOutput(int n) { curOutput += n; }

        public float Utilisation { get { return ShiftTime > 1f ? BusyTime / ShiftTime : 0f; } }

        public static Color RoleColor(Role r)
        {
            switch (r)
            {
                case Role.Cashier: return new Color(0.2f, 0.45f, 0.9f);
                case Role.Stocker: return new Color(0.95f, 0.55f, 0.15f);
                case Role.Cleaner: return new Color(0.15f, 0.7f, 0.65f);
                case Role.Baker: return new Color(0.97f, 0.95f, 0.88f);
                case Role.DeliClerk: return new Color(0.8f, 0.2f, 0.25f);
                default: return new Color(0.55f, 0.3f, 0.75f);
            }
        }

        /// <summary>Puts them in their role's uniform (after hiring, a role change or a promotion).</summary>
        public void RefreshColor()
        {
            Dress(Outfit.Staff(Role, Names.IsFemale(DisplayName), Outfit.Seed(DisplayName), Rank != Rank.Staff), Hold.None);
        }

        public override Pose CurrentPose
        {
            get
            {
                if (!OnShift) return Pose.Idle;
                if (Slacking) return SlackWhat == SlackKinds[0] ? Pose.Phone : Pose.Idle;
                if (OnBreak || idle) return Pose.Idle;
                if (Role == Role.Cashier) return AtPost ? Pose.Scan : Pose.Idle;
                if (Role == Role.AssistantManager) return Pose.Idle;
                return Pose.Work;
            }
        }

        // ------------------------------------------------------------------ shifts

        /// <summary>Clocks in for a shift (or calls in sick, or turns up late).</summary>
        public void StartShift(Shift shift, bool rollAbsence)
        {
            if (OnShift || Leaving || ArriveAt >= 0f) return;
            var store = Store.I;
            Absent = false;
            Shift = shift;
            int dow = (store.Clock.Day - 1) % 7;
            ShortRest = rollAbsence && ShortRestOn(dow);
            plannedStartAbs = store.Clock.AbsMinutes;
            if (rollAbsence)
            {
                float sick = (10f - Reliability) * 0.007f + (Morale < 35f ? 0.06f : 0f) + (ShortRest ? 0.03f : 0f);
                if (Random.value < sick)
                {
                    Absent = true;
                    bool noShow = Random.value < (10f - Reliability) * 0.04f;
                    Status = noShow ? "Didn't show up" : "Called in sick";
                    AddRecord(noShow ? IncidentKind.NoShow : IncidentKind.Sick, noShow ? "Didn't show up or call" : "Called in sick");
                    store.Message(DisplayName + " (" + Catalog.RoleName(Role) + ") " + (noShow ? "didn't show up and didn't call." : "called in sick."));
                    return;
                }
                float late = (10f - Reliability) * 0.012f + (10f - WorkEthic) * 0.006f + (Morale < 35f ? 0.05f : 0f) + (ShortRest ? 0.05f : 0f);
                if (Random.value < late)
                {
                    int min = Random.Range(5, 46);
                    ArriveAt = store.Clock.AbsMinutes + min;
                    Status = "Late";
                    return;
                }
            }
            ClockIn();
        }

        /// <summary>Called by the staff manager when a late employee finally walks in.</summary>
        public void ArriveLate()
        {
            var store = Store.I;
            int late = Mathf.RoundToInt(store.Clock.AbsMinutes - plannedStartAbs);
            ArriveAt = -1f;
            AddRecord(IncidentKind.Late, "Clocked in " + late + " min late");
            ClockIn();
            curLate = late;
        }

        public void CancelLate() { ArriveAt = -1f; Status = "Off duty"; }

        void ClockIn()
        {
            var store = Store.I;
            OnShift = true;
            ShiftEnding = false;
            paused = OnBreak = Slacking = pendingBreak = pendingSlack = false;
            tookBreak = false;
            BusyTime = 0f;
            ShiftTime = 0f;
            curOutput = 0;
            curLate = 0;
            curBreakMin = 0;
            curSlackMin = 0f;
            breakAtMin = Random.Range(180f, 300f);
            slackTimer = Random.Range(20f, 40f);
            ShiftsThisWeek++;
            Overtime = ShiftsThisWeek > ContractShifts;
            shiftStartAbs = store.Clock.AbsMinutes;
            gameObject.SetActive(true);
            Teleport(store.StaffEntrance);
            Status = "Arriving";
            StartCoroutine(WorkLoop());
        }

        public void EndShift()
        {
            if (!OnShift)
            {
                if (ArriveAt >= 0f) CancelLate();
                Absent = false;
                Status = "Off duty";
                return;
            }
            PayShift();
            AfterShiftMorale();
            FileReport();

            if (Lane != null && Lane.Cashier == this && Lane.Queue.Count > 0)
            {
                ShiftEnding = true;
                Lane.Closing = true;
                Status = "Closing checkout " + (Lane.Index + 1);
                return;
            }
            GoHomeNow();
        }

        public float HourlyRate
        {
            get { return Wage * (Shift == Shift.Night ? 1.25f : 1f) * (Overtime ? 1.5f : 1f); }
        }

        void PayShift()
        {
            float hours = Mathf.Clamp((Store.I.Clock.AbsMinutes - shiftStartAbs) / 60f, 0f, 9f);
            Store.I.Economy.PayWages(hours * HourlyRate);
            shiftStartAbs = Store.I.Clock.AbsMinutes;
        }

        void FileReport()
        {
            var store = Store.I;
            Reports.Add(new ShiftReport
            {
                Day = store.Clock.Day, Slot = StaffManager.ToSlot(Shift),
                HoursPaid = Mathf.Clamp((store.Clock.AbsMinutes - plannedStartAbs - curLate) / 60f, 0f, 9f),
                BusyShare = ShiftTime > 1f ? BusyTime / ShiftTime : 0f,
                LateMinutes = curLate, BreakMinutes = curBreakMin, SlackMinutes = Mathf.RoundToInt(curSlackMin), Output = curOutput,
            });
            if (Reports.Count > 30) Reports.RemoveAt(0);
            ShiftsWorked++;
        }

        void AfterShiftMorale()
        {
            float before = Morale;
            float payRatio = Wage / Mathf.Max(1f, AskingWage);
            Morale += (payRatio - 1f) * 30f;
            float busy = ShiftTime > 1f ? BusyTime / ShiftTime : 0.5f;
            if (busy > 0.9f) Morale -= 4f;
            else if (busy < 0.25f) Morale -= 1f;
            else Morale += 2f;
            var notes = new List<string>();
            if (payRatio < 0.97f) notes.Add("underpaid");
            else if (payRatio > 1.03f) notes.Add("happy with pay");
            if (busy > 0.9f) notes.Add("overworked");
            else if (busy < 0.25f) notes.Add("bored");
            else notes.Add("good workload");
            if (ShortRest) { Morale -= 5f; notes.Add("short rest between shifts"); }
            if (ScheduledShifts >= 7) { Morale -= 3f; notes.Add("no day off this week"); }
            else if (ScheduledShifts < ContractShifts) { Morale -= 1.5f; notes.Add("wants more hours"); }
            if (Overtime) { Morale -= 1f; notes.Add("overtime"); }
            if (Store.I.Clock.Day > NextReviewDay + 5) { Morale -= 1.5f; notes.Add("review overdue"); }
            bool wantsPromotion = Ambition >= 7f && Rank == Rank.Staff && Role != Role.AssistantManager && Tenure > 25 && ActiveWrittenWarnings == 0;
            if (wantsPromotion)
            {
                Morale -= 1.5f;
                notes.Add("wants a promotion");
                PromotionWaitShifts++;
            }
            Morale += (60f - Morale) * 0.05f;
            Morale = Mathf.Clamp(Morale, 0f, 100f);
            MoraleNote = (Morale >= before ? "up" : "down") + " last shift: " + string.Join(", ", notes.ToArray());

            Speed = Mathf.Min(10f, Speed + 0.03f);
            Service = Mathf.Min(10f, Service + 0.02f);

            if (Morale < 20f) LowMoraleShifts++;
            else LowMoraleShifts = 0;
            if (LowMoraleShifts >= 2 && Random.value < 0.5f)
            {
                Quit = true;
                Store.I.Message(DisplayName + " (" + Catalog.RoleName(Role) + ") quit. Morale was too low.");
            }
            else if (wantsPromotion && PromotionWaitShifts > 10 && Random.value < 0.06f)
            {
                Quit = true;
                Store.I.Message(DisplayName + " quit for a job with more responsibility elsewhere. They wanted a promotion.");
            }
        }

        public void GoHomeNow()
        {
            paused = OnBreak = Slacking = false;
            StopAllCoroutines();
            ReleaseAll();
            if (gameObject.activeInHierarchy) StartCoroutine(GoHome());
            else FinishGoingHome();
        }

        IEnumerator GoHome()
        {
            Status = "Going home";
            AtPost = false;
            idle = false;
            yield return StartCoroutine(MoveTo(Store.I.StaffEntrance));
            FinishGoingHome();
        }

        void FinishGoingHome()
        {
            OnShift = false;
            ShiftEnding = false;
            AtPost = false;
            Status = Quit ? "Quit" : "Off duty";
            if (Quit || Leaving)
            {
                Store.I.Staff.Remove(this);
                Destroy(gameObject);
                return;
            }
            gameObject.SetActive(false);
        }

        /// <summary>Fired: pay for hours worked and walk out for good.</summary>
        public void Dismiss()
        {
            Leaving = true;
            if (OnShift)
            {
                PayShift();
                GoHomeNow();
            }
            else
            {
                Store.I.Staff.Remove(this);
                Destroy(gameObject);
            }
        }

        /// <summary>Restart work (after a role or department change).</summary>
        public void Restart()
        {
            if (!OnShift || Leaving) return;
            StopAllCoroutines();
            ReleaseAll();
            ShiftEnding = false;
            paused = OnBreak = Slacking = false;
            StartCoroutine(WorkLoop());
        }

        void ReleaseAll()
        {
            AtPost = false;
            idle = false;
            Stop();
            if (Lane != null)
            {
                if (Lane.Cashier == this) { Lane.Cashier = null; Lane.Closing = false; }
                Lane = null;
            }
            if (cartProduct != null && cart.Total > 0)
            {
                if (cartProduct.Baked)
                {
                    if (claimed != null && !claimed.Removed && claimed.Product == cartProduct) cart.MoveTo(claimed.Stock, cart.Total);
                }
                else cart.MoveTo(cartProduct.Back, cart.Total);
            }
            cartProduct = null;
            cart.Clear();
            if (claimed != null)
            {
                if (claimed.ClaimedBy == this) claimed.ClaimedBy = null;
                claimed = null;
            }
            if (claimedSpill != null)
            {
                if (claimedSpill.ClaimedBy == this) claimedSpill.ClaimedBy = null;
                claimedSpill = null;
            }
            if (complaintTarget != null)
            {
                if (complaintTarget.Responder == this) complaintTarget.Responder = null;
                complaintTarget = null;
            }
            if (station != null && station.Clerk == this) station.Clerk = null;
            station = null;
        }

        protected override void Update()
        {
            base.Update();
            if (!OnShift) return;
            float dt = Time.deltaTime;
            ShiftTime += dt;
            if (!idle && !paused) BusyTime += dt;
            if (ManagerNearby) Morale = Mathf.Min(100f, Morale + dt * 0.03f);
            else if (Store.I.Delegation.Morale && AssistantNearby) Morale = Mathf.Min(100f, Morale + dt * 0.015f);
            if (ShiftEnding || Leaving) return;

            var clock = Store.I.Clock;
            float minutesIn = clock.AbsMinutes - shiftStartAbs;

            // Caught in the act: the manager walks up while they're slacking.
            if (Slacking)
            {
                var m = Store.I.Avatar;
                if (m != null && !m.AtDesk && m.DistanceTo(transform.position) < 4f) Caught();
                return;
            }
            if (paused) return;

            if (!tookBreak && !pendingBreak && minutesIn >= breakAtMin && BreakAllowed(minutesIn)) pendingBreak = true;
            slackTimer -= dt;
            if (slackTimer <= 0f)
            {
                slackTimer = 40f;   // 20 game minutes
                if (!pendingBreak && !pendingSlack && Random.value < SlackChance) pendingSlack = true;
            }

            if (pendingBreak || pendingSlack)
            {
                // A cashier closes the till and serves the queue first.
                if (Role == Role.Cashier && Lane != null && Lane.Cashier == this)
                {
                    Lane.Closing = true;
                    if (Lane.Queue.Count > 0) return;
                }
                bool isBreak = pendingBreak;
                pendingBreak = pendingSlack = false;
                StopAllCoroutines();
                ReleaseAll();
                paused = true;
                StartCoroutine(isBreak ? BreakRoutine() : SlackRoutine());
            }
        }

        /// <summary>
        /// Breaks are staggered: one person per role at a time, and a cashier on the last open till waits for cover
        /// (up to two hours; then they go anyway, the law says so).
        /// </summary>
        bool BreakAllowed(float minutesIn)
        {
            if (minutesIn > breakAtMin + 120f) return true;
            var all = Store.I.Staff.All;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o != null && o != this && o.OnShift && o.Role == Role && o.OnBreak) return false;
            }
            if (Role == Role.Cashier && Store.I.Clock.IsOpen && Lane != null && Store.I.OpenLaneCount <= 1) return false;
            return true;
        }

        IEnumerator BreakRoutine()
        {
            var store = Store.I;
            tookBreak = true;
            OnBreak = true;   // counts as on break from now, so nobody else of this role goes at the same time
            Status = "Going on break";
            yield return StartCoroutine(MoveTo(store.BreakSpot(this)));
            Status = "On break";
            float minutes = 30f;
            // People who don't care much stretch their break.
            if (Random.value < Mathf.Max(0f, 7f - WorkEthic) * 0.045f * (Supervised ? 0.3f : 1f)) minutes += Random.Range(8f, 26f);
            float start = store.Clock.AbsMinutes;
            while (store.Clock.AbsMinutes - start < minutes) yield return null;
            OnBreak = false;
            int took = Mathf.RoundToInt(store.Clock.AbsMinutes - start);
            curBreakMin += took;
            if (took > 35) AddRecord(IncidentKind.LongBreak, "Clocked a " + took + " min break (30 allowed)");
            paused = false;
            StartCoroutine(WorkLoop());
        }

        static readonly string[] SlackKinds = { "on their phone", "chatting instead of working", "hiding in the back room", "taking a smoke break" };

        IEnumerator SlackRoutine()
        {
            var store = Store.I;
            int k = Random.Range(0, SlackKinds.Length);
            SlackWhat = SlackKinds[k];
            if (k == 2 || k == 3) yield return StartCoroutine(MoveTo(k == 2 ? store.BackPickNear(null) + new Vector2Int(Random.Range(0, 8), 0) : store.StaffEntrance));
            Slacking = true;
            idle = true;
            Status = char.ToUpper(SlackWhat[0]) + SlackWhat.Substring(1);
            float minutes = Random.Range(5f, 16f) + (k >= 2 ? 6f : 0f);
            slackUntil = store.Clock.AbsMinutes + minutes;
            while (store.Clock.AbsMinutes < slackUntil) yield return null;
            curSlackMin += minutes;
            Slacking = false;
            idle = false;
            paused = false;
            StartCoroutine(WorkLoop());
        }

        void Caught()
        {
            var store = Store.I;
            curSlackMin += Mathf.Max(0f, store.Clock.AbsMinutes - (slackUntil - 10f));
            Slacking = false;
            idle = false;
            AddRecord(IncidentKind.CaughtSlacking, "You caught them " + SlackWhat);
            Morale = Mathf.Max(0f, Morale - 2f);
            Hud.Toast("You caught " + DisplayName + " " + SlackWhat + ". Click them to have a word.");
            if (Hud.I != null) Hud.I.Selected = this;
            StopAllCoroutines();
            paused = false;
            StartCoroutine(WorkLoop());
        }

        // ------------------------------------------------------------------ manager actions

        public string Praise()
        {
            int day = Store.I.Clock.Day;
            if (PraisedDay == day) return DisplayName + " was already praised today.";
            PraisedDay = day;
            Morale = Mathf.Min(100f, Morale + 12f);
            AddRecord(IncidentKind.Praise, "You praised their work");
            return "You praised " + DisplayName + ". Morale up.";
        }

        /// <summary>A quiet word: behaves better for a few days, a small morale hit.</summary>
        public string VerbalWarning()
        {
            int day = Store.I.Clock.Day;
            if (WarnedDay == day) return "You already had a word with " + DisplayName + " today.";
            WarnedDay = day;
            Morale = Mathf.Max(0f, Morale - 4f);
            AddRecord(IncidentKind.VerbalWarning, "Verbal warning");
            return "You had a word with " + DisplayName + ". They'll pull themselves together for a while.";
        }

        /// <summary>The incident a written warning would be about (the latest one in the last 7 days not dealt with yet).</summary>
        public StaffIncident WarnableIncident
        {
            get
            {
                int day = Store.I.Clock.Day;
                for (int i = Record.Count - 1; i >= 0; i--)
                {
                    var r = Record[i];
                    if (day - r.Day > 7) break;
                    if (r.Addressed) continue;
                    if (r.Kind == IncidentKind.CaughtSlacking || r.Kind == IncidentKind.Late || r.Kind == IncidentKind.LongBreak || r.Kind == IncidentKind.NoShow)
                        return r;
                }
                return null;
            }
        }

        /// <summary>A formal written warning (áminning). Needs an incident to refer to. Two active ones allow dismissal.</summary>
        public string WrittenWarning()
        {
            var inc = WarnableIncident;
            if (inc == null) return "A written warning needs something recent to refer to (late, long break, caught slacking, no-show).";
            inc.Addressed = true;
            WrittenDay = Store.I.Clock.Day;
            Morale = Mathf.Max(0f, Morale - 10f);
            AddRecord(IncidentKind.WrittenWarning, "Written warning for: " + inc.Note.ToLower() + " (day " + inc.Day + ")");
            int n = ActiveWrittenWarnings;
            return "Written warning given to " + DisplayName + " (" + n + " active)." + (n >= 2 ? " You can now dismiss them if it happens again." : "");
        }

        public void GiveRaise(float amount)
        {
            Wage += amount;
            Morale = Mathf.Min(100f, Morale + 4f + amount * 3f);
            AddRecord(IncidentKind.Raise, "Raise of $" + amount.ToString("0") + "/h (now $" + Wage.ToString("0") + ")");
        }

        public void GiveRaise() { GiveRaise(1f); }

        // ------------------------------------------------------------------ work

        IEnumerator WorkLoop()
        {
            switch (Role)
            {
                case Role.Cashier: yield return StartCoroutine(CashierLoop(false)); break;
                case Role.Stocker: yield return StartCoroutine(StockerLoop()); break;
                case Role.Cleaner: yield return StartCoroutine(CleanerLoop()); break;
                case Role.Baker: yield return StartCoroutine(BakerLoop()); break;
                case Role.DeliClerk: yield return StartCoroutine(DeliLoop()); break;
                default: yield return StartCoroutine(AssistantLoop()); break;
            }
            // Only a cashier finishing their last queue gets here.
            ReleaseAll();
            yield return StartCoroutine(GoHome());
        }

        IEnumerator DoWork(float seconds)
        {
            idle = false;
            float p = 0f;
            while (p < seconds)
            {
                p += Time.deltaTime * WorkSpeed;
                yield return null;
            }
        }

        IEnumerator Idle(float seconds)
        {
            idle = true;
            yield return StartCoroutine(Wait(seconds));
            idle = false;
        }

        IEnumerator IdleNear()
        {
            idle = true;
            if (Random.value < 0.3f)
            {
                yield return StartCoroutine(MoveTo(Store.I.RandomFloorTile()));
            }
            yield return StartCoroutine(Wait(3f));
            idle = false;
        }

        IEnumerator CashierLoop(bool cover)
        {
            float calm = 0f, overtime = 0f;
            while (true)
            {
                if (Lane != null && Lane.Removed)
                {
                    if (Lane.Cashier == this) Lane.Cashier = null;
                    Lane = null;
                    AtPost = false;
                }
                if (Lane == null)
                {
                    Lane = Store.I.FreeLane();
                    if (Lane == null)
                    {
                        Status = "No free checkout";
                        if (cover) yield break;
                        yield return StartCoroutine(Idle(3f));
                        continue;
                    }
                    Lane.Cashier = this;
                    Lane.Closing = false;
                }

                if (!AtPost)
                {
                    idle = false;
                    Status = "Walking to checkout " + (Lane.Index + 1);
                    yield return StartCoroutine(MoveTo(Lane.CashierTile));
                    if (Tile != Lane.CashierTile) continue;
                    AtPost = true;
                    Face(Vector3.right);
                }

                var c = Lane.Front;
                if (c != null && c.AtServiceSpot)
                {
                    Status = "Serving at checkout " + (Lane.Index + 1);
                    yield return StartCoroutine(DoWork(2.5f + c.Items * 0.4f));
                    if (c != null && !c.Paid && Lane != null && Lane.Queue.Contains(c)) { curOutput += c.Items; c.Pay(this); }
                    continue;
                }

                idle = Lane.Queue.Count == 0;
                Status = "Checkout " + (Lane.Index + 1) + (Lane.Closing ? " (closing)" : "");

                if (ShiftEnding)
                {
                    overtime += Time.deltaTime;
                    if (Lane.Queue.Count == 0 && (Store.I.Clock.IsOpen || !Store.I.AnyoneShopping())) break;
                    if (overtime > 90f && Lane.Queue.Count == 0) break;
                }
                if (cover)
                {
                    if (Store.I.QueuePressure()) calm = 0f;
                    else calm += Time.deltaTime;
                    if (calm > 20f)
                    {
                        Lane.Closing = true;
                        if (Lane.Queue.Count == 0) break;
                    }
                }
                yield return null;
            }
            if (Lane != null)
            {
                if (Lane.Cashier == this) { Lane.Cashier = null; Lane.Closing = false; }
                Lane = null;
            }
            AtPost = false;
        }

        /// <summary>The emptiest shelf or counter (not bakery) that has stock waiting in the back room.</summary>
        Fixture FindRestock()
        {
            Fixture best = null;
            float bestScore = float.MaxValue;
            var all = Store.I.Fixtures;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (!f.HoldsProducts || f.IsBakeryShelf || f.Product == null || f.Removed) continue;
                if (f.ClaimedBy != null || f.Product.Back.FreshTotal <= 0 || f.FreshFill >= 0.6f) continue;
                if (AssignedCategory != null && f.Product.Category != AssignedCategory) continue;
                float score = f.FreshFill * 40f + (f.Center - transform.position).magnitude * 0.1f;
                if (score < bestScore) { bestScore = score; best = f; }
            }
            return best;
        }

        IEnumerator StockerLoop()
        {
            while (true)
            {
                if (Store.I.ClaimDateCheck(this))
                {
                    yield return StartCoroutine(DateCheckRound());
                    continue;
                }
                var f = FindRestock();
                if (f == null)
                {
                    Status = AssignedCategory != null ? AssignedCategory.Name + ": shelves full" : "Shelves full";
                    yield return StartCoroutine(IdleNear());
                    continue;
                }
                yield return StartCoroutine(RestockRun(f, 30));
            }
        }

        /// <summary>Fetch from the back room and fill one fixture.</summary>
        IEnumerator RestockRun(Fixture f, int capacity)
        {
            var p = f.Product;
            idle = false;
            claimed = f;
            f.ClaimedBy = this;
            Status = "Fetching " + p.Name;
            yield return StartCoroutine(MoveTo(Store.I.BackPickNear(f)));
            yield return StartCoroutine(DoWork(2f));
            // Expired cases in the back get scanned out instead of going on the shelf.
            Store.I.WriteOff(p, p.Back.RemoveExpired());

            int n = Mathf.Min(capacity, Mathf.Min(f.Def.Capacity - f.Stock.FreshTotal, p.Back.Total));
            if (!f.Removed && f.Product == p && n > 0)
            {
                p.Back.MoveTo(cart, n);
                cartProduct = p;
                Status = "Stocking " + p.Name;
                yield return StartCoroutine(MoveTo(f.IsCounter ? f.Work : f.AccessTile(Store.I.Grid)));
                yield return StartCoroutine(DoWork(1f + n * 0.2f));
                if (!f.Removed && f.Product == p)
                {
                    // Facing the shelf: anything out of date comes off.
                    Store.I.WriteOff(p, f.Stock.RemoveExpired());
                    int fit = Mathf.Max(0, f.Def.Capacity - f.Stock.Total);
                    curOutput += cart.MoveTo(f.Stock, Mathf.Min(fit, cart.Total));
                }
                if (cart.Total > 0) cart.MoveTo(p.Back, cart.Total);
                cart.Clear();
                cartProduct = null;
            }
            if (f.ClaimedBy == this) f.ClaimedBy = null;
            claimed = null;
        }

        /// <summary>
        /// Walks the back room and every shelf holding something perishable, pulls out expired stock and
        /// scans it out at the back. Suppliers with a returns policy credit part of it on their next delivery.
        /// </summary>
        IEnumerator DateCheckRound()
        {
            var store = Store.I;
            idle = false;
            int total = 0;

            Status = "Date check: back room";
            yield return StartCoroutine(MoveTo(store.BackPickNear(null)));
            yield return StartCoroutine(DoWork(3f));
            for (int i = 0; i < store.Products.Count; i++)
            {
                var p = store.Products[i];
                if (p.Life <= 0) continue;
                int n = p.Back.RemoveExpired();
                store.WriteOff(p, n);
                total += n;
            }

            var todo = new List<Fixture>();
            for (int i = 0; i < store.Fixtures.Count; i++)
            {
                var f = store.Fixtures[i];
                if (f.HoldsProducts && !f.Removed && f.Product != null && f.Product.Life > 0) todo.Add(f);
            }
            while (todo.Count > 0 && !ShiftEnding)
            {
                // nearest next
                int best = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < todo.Count; i++)
                {
                    float d = (todo[i].Center - transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                var f = todo[best];
                todo.RemoveAt(best);
                if (f.Removed || f.Product == null) continue;
                var p = f.Product;
                Status = "Date check: " + p.Name;
                yield return StartCoroutine(MoveTo(f.IsCounter ? f.Work : f.AccessTile(store.Grid)));
                yield return StartCoroutine(DoWork(0.6f + f.Stock.Total * 0.02f));
                if (f.Removed || f.Product != p) continue;
                int n = f.Stock.RemoveExpired();
                store.WriteOff(p, n);
                total += n;
            }

            if (total > 0)
            {
                Status = "Scanning out " + total + " expired items";
                yield return StartCoroutine(MoveTo(store.BackPickNear(null)));
                yield return StartCoroutine(DoWork(1f + total * 0.03f));
            }
        }

        Fixture FindBakeryShelf()
        {
            Fixture best = null;
            float bestFill = 0.7f;
            var all = Store.I.Fixtures;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (!f.IsBakeryShelf || f.Product == null || f.Removed || f.ClaimedBy != null) continue;
                if (f.FreshFill < bestFill) { bestFill = f.FreshFill; best = f; }
            }
            return best;
        }

        IEnumerator BakerLoop()
        {
            while (true)
            {
                var oven = Store.I.NearestFixture("oven", transform.position);
                if (oven == null)
                {
                    Status = "No oven to bake in";
                    yield return StartCoroutine(Idle(5f));
                    continue;
                }
                var shelf = FindBakeryShelf();
                if (shelf == null)
                {
                    Status = Store.I.CountFixtures("bakery_shelf") == 0 ? "No bakery shelves" : "Bakery shelves full";
                    yield return StartCoroutine(Idle(4f));
                    continue;
                }

                var p = shelf.Product;
                idle = false;
                claimed = shelf;
                shelf.ClaimedBy = this;
                Status = "Baking " + p.Name;
                yield return StartCoroutine(MoveTo(oven.Work));
                int batch = p.Def.CaseSize;
                yield return StartCoroutine(DoWork(16f * (Specialty == Specialty.Baking ? 1f : 2.2f)));
                if (!shelf.Removed && shelf.Product == p)
                {
                    Store.I.Economy.PayWholesale(p.Def.Cost * batch);
                    cart.Add(batch, p.Life);
                    cartProduct = p;
                    Status = "Putting out " + p.Name;
                    yield return StartCoroutine(MoveTo(shelf.AccessTile(Store.I.Grid)));
                    yield return StartCoroutine(DoWork(1.5f + batch * 0.15f));
                    if (!shelf.Removed && shelf.Product == p)
                    {
                        Store.I.WriteOff(p, shelf.Stock.RemoveExpired());
                        curOutput += cart.MoveTo(shelf.Stock, Mathf.Min(cart.Total, Mathf.Max(0, shelf.Def.Capacity - shelf.Stock.Total)));
                    }
                    cart.Clear();
                    cartProduct = null;
                }
                if (shelf.ClaimedBy == this) shelf.ClaimedBy = null;
                claimed = null;
            }
        }

        Fixture FreeCounter()
        {
            Fixture best = null;
            int bestWaiting = -1;
            var all = Store.I.Fixtures;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (!f.IsCounter || f.Removed || f.Clerk != null || f.Product == null) continue;
                if (f.Waiting.Count > bestWaiting) { bestWaiting = f.Waiting.Count; best = f; }
            }
            return best;
        }

        IEnumerator DeliLoop()
        {
            while (true)
            {
                if (station != null && (station.Removed || station.Product == null || station.Clerk != this)) ReleaseStation();
                if (station == null)
                {
                    station = FreeCounter();
                    if (station == null)
                    {
                        Status = Store.I.CountFixtures("deli_counter") == 0 ? "No deli counter to work at" : "Helping at the counters";
                        yield return StartCoroutine(Idle(4f));
                        continue;
                    }
                    station.Clerk = this;
                }

                if (!AtPost)
                {
                    idle = false;
                    Status = "Going to the " + station.Product.Name + " counter";
                    yield return StartCoroutine(MoveTo(station.Work));
                    if (station == null) continue;
                    AtPost = true;
                    Face(new Vector3(station.QueueDir.x, 0f, station.QueueDir.y));
                }

                var c = station.Waiting.Count > 0 ? station.Waiting[0] : null;
                if (c != null && c.AtCounter && !c.CounterServed)
                {
                    Status = "Serving a customer";
                    yield return StartCoroutine(DoWork(3f * (Specialty == Specialty.Butchery ? 1f : 1.5f)));
                    if (c != null) { c.CounterServed = true; curOutput += 1; }
                    continue;
                }
                if (station.Waiting.Count == 0)
                {
                    // Nobody here: go and serve at a counter where people are waiting with no clerk.
                    var busier = BusiestUnstaffedCounter();
                    if (busier != null)
                    {
                        ReleaseStation();
                        station = busier;
                        station.Clerk = this;
                        continue;
                    }
                }
                bool empty = station.Stock.FreshTotal == 0;
                if ((empty || (station.Waiting.Count == 0 && station.FreshFill < 0.5f)) && station.Product.Back.FreshTotal > 0
                    && (station.ClaimedBy == null || station.ClaimedBy == this))
                {
                    var f = station;
                    AtPost = false;
                    yield return StartCoroutine(RestockRun(f, 20));
                    continue;
                }
                idle = station.Waiting.Count == 0;
                Status = "Behind the " + station.Product.Name + " counter";
                yield return null;
            }
        }

        Fixture BusiestUnstaffedCounter()
        {
            Fixture best = null;
            var all = Store.I.Fixtures;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (!f.IsCounter || f.Removed || f.Product == null || f.Waiting.Count == 0) continue;
                if (f.Clerk != null && f.Clerk != this) continue;
                if (best == null || f.Waiting.Count > best.Waiting.Count) best = f;
            }
            return best;
        }

        void ReleaseStation()
        {
            if (station != null && station.Clerk == this) station.Clerk = null;
            station = null;
            AtPost = false;
        }

        IEnumerator CleanerLoop()
        {
            while (true)
            {
                var s = Store.I.Dirt.ClaimNearest(transform.position, this);
                if (s == null)
                {
                    Status = "Looking for mess";
                    yield return StartCoroutine(IdleNear());
                    continue;
                }
                claimedSpill = s;
                idle = false;
                Status = "Cleaning a spill";
                yield return StartCoroutine(MoveTo(s.Tile));
                yield return StartCoroutine(DoWork(4f));
                Store.I.Dirt.Remove(s);
                curOutput += 10;
                claimedSpill = null;
            }
        }

        IEnumerator AssistantLoop()
        {
            var store = Store.I;
            var del = store.Delegation;
            while (true)
            {
                if (del.Complaints)
                {
                    var c = store.UnansweredComplaint();
                    if (c != null)
                    {
                        c.Responder = this;
                        complaintTarget = c;
                        idle = false;
                        Status = "Handling a complaint";
                        yield return StartCoroutine(MoveTo(c.Tile));
                        if (c != null && c.Complaining && DistanceTo(c.transform.position) < 2.5f)
                        {
                            yield return StartCoroutine(DoWork(3f));
                            if (c != null) c.Resolve(this, 12f + Leadership * 1.5f);
                        }
                        if (c != null && c.Responder == this) c.Responder = null;
                        complaintTarget = null;
                        continue;
                    }
                }

                if (del.Checkouts && store.QueuePressure() && store.FreeLane() != null)
                {
                    Status = "Opening a checkout";
                    yield return StartCoroutine(CashierLoop(true));
                    AtPost = false;
                    if (ShiftEnding) yield break;
                    continue;
                }

                idle = false;
                Status = "Walking the floor";
                GoTo(store.RandomFloorTile());
                while (IsMoving)
                {
                    if (del.Complaints && store.UnansweredComplaint() != null) { Stop(); break; }
                    if (del.Checkouts && store.QueuePressure() && store.FreeLane() != null) { Stop(); break; }
                    yield return null;
                }
                yield return StartCoroutine(Wait(1.5f));
            }
        }
    }
}
