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
                    cart.MoveTo(f.Stock, Mathf.Min(fit, cart.Total));
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
                        cart.MoveTo(shelf.Stock, Mathf.Min(cart.Total, Mathf.Max(0, shelf.Def.Capacity - shelf.Stock.Total)));
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
                    if (c != null) c.CounterServed = true;
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
