using System.Collections;
using UnityEngine;

namespace StoreManager
{
    public class Employee : Agent
    {
        public Role Role;
        public Shift Shift;
        public Department AssignedDept;   // stockers, bakers, deli clerks (null = anywhere)

        public float Speed, Service, Reliability, Leadership;
        public Specialty Specialty;
        public float Wage, AskingWage;
        public float Morale = 65f;
        public int HiredDay;

        public bool OnShift, Absent, AtPost, ShiftEnding, Quit, Leaving;
        public string Status = "Off duty";
        public string MoraleNote = "";
        public int PraisedDay = -1;
        public bool WarnedThisShift;
        public int LowMoraleShifts;
        public float BusyTime, ShiftTime;
        public CheckoutLane Lane;

        bool idle;
        float shiftStartAbs;
        ProductStock claimed;
        Spill claimedSpill;
        Customer complaintTarget;
        CounterStation station;
        readonly StockPile cart = new StockPile();
        ProductStock cartProduct;

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
                if (WarnedThisShift) f *= 1.2f;
                if (ManagerNearby) f *= 1.15f;
                else if (AssistantNearby) f *= 1.05f;
                return f;
            }
        }

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

        public void RefreshColor()
        {
            if (Body != null) Body.sharedMaterial = Mats.Get(RoleColor(Role));
        }

        // ------------------------------------------------------------------ shifts

        public void StartShift(bool rollAbsence)
        {
            if (OnShift || Leaving) return;
            Absent = false;
            if (rollAbsence)
            {
                float chance = (10f - Reliability) * 0.007f + (Morale < 35f ? 0.06f : 0f);
                if (Random.value < chance)
                {
                    Absent = true;
                    Status = "Called in sick";
                    Store.I.Message(DisplayName + " (" + Catalog.RoleName(Role) + ") called in sick.");
                    return;
                }
            }
            OnShift = true;
            ShiftEnding = false;
            WarnedThisShift = false;
            BusyTime = 0f;
            ShiftTime = 0f;
            shiftStartAbs = Store.I.Clock.AbsMinutes;
            gameObject.SetActive(true);
            Teleport(Store.I.StaffEntrance);
            Status = "Arriving";
            StartCoroutine(WorkLoop());
        }

        public void EndShift()
        {
            if (!OnShift)
            {
                Absent = false;
                Status = "Off duty";
                return;
            }
            PayShift();
            AfterShiftMorale();

            if (Lane != null && Lane.Cashier == this && Lane.Queue.Count > 0)
            {
                ShiftEnding = true;
                Lane.Closing = true;
                Status = "Closing checkout " + (Lane.Index + 1);
                return;
            }
            GoHomeNow();
        }

        void PayShift()
        {
            float hours = Mathf.Clamp((Store.I.Clock.AbsMinutes - shiftStartAbs) / 60f, 0f, 9f);
            float rate = Wage * (Shift == Shift.Night ? 1.25f : 1f);
            Store.I.Economy.PayWages(hours * rate);
            shiftStartAbs = Store.I.Clock.AbsMinutes;
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
            var notes = new System.Collections.Generic.List<string>();
            if (payRatio < 0.97f) notes.Add("underpaid");
            else if (payRatio > 1.03f) notes.Add("happy with pay");
            if (busy > 0.9f) notes.Add("overworked");
            else if (busy < 0.25f) notes.Add("bored");
            else notes.Add("good workload");
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
        }

        public void GoHomeNow()
        {
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
            if (claimed != null)
            {
                if (claimed.ClaimedBy == this) claimed.ClaimedBy = null;
                claimed = null;
            }
            if (cartProduct != null && cart.Total > 0)
            {
                if (!cartProduct.Dept.Active) cart.Clear();
                else if (cartProduct.Dept.IsBakery) cart.MoveTo(cartProduct.Shelf, cart.Total);
                else cart.MoveTo(cartProduct.Back, cart.Total);
            }
            cartProduct = null;
            cart.Clear();
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
            if (!idle) BusyTime += dt;
            if (ManagerNearby) Morale = Mathf.Min(100f, Morale + dt * 0.03f);
            else if (Store.I.Delegation.Morale && AssistantNearby) Morale = Mathf.Min(100f, Morale + dt * 0.015f);
        }

        // ------------------------------------------------------------------ manager actions

        public string Praise()
        {
            int day = Store.I.Clock.Day;
            if (PraisedDay == day) return DisplayName + " was already praised today.";
            PraisedDay = day;
            Morale = Mathf.Min(100f, Morale + 12f);
            return "You praised " + DisplayName + ". Morale up.";
        }

        public string Warn()
        {
            if (!OnShift) return DisplayName + " isn't on shift.";
            if (WarnedThisShift) return DisplayName + " was already warned this shift.";
            WarnedThisShift = true;
            Morale = Mathf.Max(0f, Morale - 10f);
            return "You warned " + DisplayName + ". Working faster, but morale down.";
        }

        public void GiveRaise()
        {
            Wage += 1f;
            Morale = Mathf.Min(100f, Morale + 6f);
        }

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
                Vector2Int t = AssignedDept != null && AssignedDept.FloorTiles.Count > 0
                    ? AssignedDept.FloorTiles[Random.Range(0, AssignedDept.FloorTiles.Count)]
                    : Store.I.RandomFloorTile();
                yield return StartCoroutine(MoveTo(t));
            }
            yield return StartCoroutine(Wait(3f));
            idle = false;
        }

        IEnumerator CashierLoop(bool cover)
        {
            float calm = 0f, overtime = 0f;
            while (true)
            {
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
                    if (c != null && !c.Paid && Lane != null && Lane.Queue.Contains(c)) c.Pay(this);
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

        ProductStock FindRestock(out Department dept)
        {
            dept = null;
            ProductStock best = null;
            float bestFill = 0.7f;
            var slots = Store.I.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                var d = slots[i];
                if (d == null || !d.Active || d.IsBakery) continue;
                if (AssignedDept != null && d != AssignedDept) continue;
                var p = d.NeedsRestock(0.7f);
                if (p != null && p.ShelfFill < bestFill)
                {
                    best = p;
                    bestFill = p.ShelfFill;
                    dept = d;
                }
            }
            return best;
        }

        IEnumerator StockerLoop()
        {
            while (true)
            {
                Department d;
                var ps = FindRestock(out d);
                if (ps == null)
                {
                    Status = AssignedDept != null ? AssignedDept.Name + ": shelves full" : "Shelves full";
                    yield return StartCoroutine(IdleNear());
                    continue;
                }
                yield return StartCoroutine(RestockRun(d, ps, 30));
            }
        }

        IEnumerator RestockRun(Department d, ProductStock ps, int capacity)
        {
            idle = false;
            claimed = ps;
            ps.ClaimedBy = this;
            Status = "Fetching " + ps.Def.Name;
            yield return StartCoroutine(MoveTo(d.BackPick));
            yield return StartCoroutine(DoWork(2f));

            int space = ps.ShelfCap - ps.Shelf.Total;
            int n = Mathf.Min(capacity, Mathf.Min(space, ps.Back.Total));
            if (d.Active && n > 0)
            {
                ps.Back.MoveTo(cart, n);
                cartProduct = ps;
                Status = "Stocking " + ps.Def.Name;
                yield return StartCoroutine(MoveTo(d.RestockTile(ps)));
                yield return StartCoroutine(DoWork(1f + n * 0.2f));
                if (d.Active)
                {
                    int fit = Mathf.Max(0, ps.ShelfCap - ps.Shelf.Total);
                    cart.MoveTo(ps.Shelf, Mathf.Min(fit, cart.Total));
                    if (cart.Total > 0) cart.MoveTo(ps.Back, cart.Total);
                }
                cart.Clear();
                cartProduct = null;
            }
            if (ps.ClaimedBy == this) ps.ClaimedBy = null;
            claimed = null;
        }

        IEnumerator BakerLoop()
        {
            while (true)
            {
                var d = AssignedDept != null && AssignedDept.IsBakery && AssignedDept.Active
                    ? AssignedDept : Store.I.FirstActive(DeptKind.Bakery);
                if (d == null)
                {
                    Status = "No bakery to work in";
                    yield return StartCoroutine(Idle(5f));
                    continue;
                }
                var ps = d.NeedsBaking(0.75f);
                if (ps == null)
                {
                    Status = "Bakery shelves full";
                    yield return StartCoroutine(Idle(4f));
                    continue;
                }

                idle = false;
                claimed = ps;
                ps.ClaimedBy = this;
                Status = "Baking " + ps.Def.Name;
                yield return StartCoroutine(MoveTo(d.WorkTile));
                int batch = ps.Def.CaseSize;
                yield return StartCoroutine(DoWork(16f * (Specialty == Specialty.Baking ? 1f : 2.2f)));
                if (d.Active)
                {
                    Store.I.Economy.PayWholesale(ps.Def.Cost * batch);
                    cart.Add(batch, ps.Life);
                    cartProduct = ps;
                    Status = "Putting out " + ps.Def.Name;
                    yield return StartCoroutine(MoveTo(d.RestockTile(ps)));
                    yield return StartCoroutine(DoWork(1.5f + batch * 0.15f));
                    if (d.Active) cart.MoveTo(ps.Shelf, cart.Total);
                    cart.Clear();
                    cartProduct = null;
                }
                if (ps.ClaimedBy == this) ps.ClaimedBy = null;
                claimed = null;
            }
        }

        IEnumerator DeliLoop()
        {
            while (true)
            {
                var d = AssignedDept != null && AssignedDept.IsCounter && AssignedDept.Active
                    ? AssignedDept : Store.I.FirstActive(DeptKind.Counter);
                if (d == null)
                {
                    ReleaseStation();
                    Status = "No deli counter to work at";
                    yield return StartCoroutine(Idle(5f));
                    continue;
                }

                if (station == null || !d.Stations.Contains(station))
                {
                    ReleaseStation();
                    station = d.FreeStationForClerk();
                    if (station == null)
                    {
                        // Every station is taken: keep the display full instead.
                        var p = d.NeedsRestock(0.8f);
                        if (p != null) yield return StartCoroutine(RestockRun(d, p, 20));
                        else
                        {
                            Status = "Helping at the counter";
                            yield return StartCoroutine(Idle(3f));
                        }
                        continue;
                    }
                    station.Clerk = this;
                }

                if (!AtPost)
                {
                    idle = false;
                    Status = "Going to the counter";
                    yield return StartCoroutine(MoveTo(station.WorkTile));
                    AtPost = true;
                    Face(Vector3.back);
                }

                var c = station.Customer;
                if (c != null && c.AtCounter && !c.CounterServed)
                {
                    Status = "Serving a customer";
                    yield return StartCoroutine(DoWork(3f * (Specialty == Specialty.Butchery ? 1f : 1.5f)));
                    if (c != null) c.CounterServed = true;
                    continue;
                }
                if (c == null && d.CounterQueue.Count == 0)
                {
                    var low = d.NeedsRestock(0.5f);
                    if (low != null)
                    {
                        ReleaseStation();
                        yield return StartCoroutine(RestockRun(d, low, 20));
                        continue;
                    }
                }
                idle = c == null;
                Status = "Behind the counter";
                yield return null;
            }
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
