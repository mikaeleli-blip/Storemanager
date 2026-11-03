using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class Customer : Agent
    {
        class Want { public DepartmentDef Dept; public ProductDef Product; public int Qty; }
        class Picked { public ProductStock Stock; public int Qty; }

        public const float ComplaintLimit = 50f;

        public float Satisfaction = 70f;
        public int Items;
        public float CartValue;
        public string Status = "Arriving";
        public bool HurryUp;
        public bool WaitingForLane;
        public bool IsLeaving;

        // Complaints
        public bool Complaining;
        public string ComplaintReason = "";
        public float ComplaintTime;
        public Agent Responder;
        bool complainedOnce, wantsToComplain, resolved;
        string pendingReason = "";

        // Checkout
        public CheckoutLane Lane;
        public bool Paid;
        public bool AtServiceSpot;

        // Counter
        public bool AtCounter;
        public bool CounterServed;
        public CounterStation Station;

        bool gaveUp;
        float patience;
        float dirtTimer;
        GameObject marker;

        readonly List<Want> wants = new List<Want>();
        readonly List<Spill> spillsSeen = new List<Spill>();
        readonly List<Picked> picked = new List<Picked>();
        readonly Dictionary<Department, float> deptValue = new Dictionary<Department, float>();

        public override float MoveSpeed { get { return HurryUp ? BaseSpeed * 1.3f : BaseSpeed; } }
        public int ListSize { get { return wants.Count; } }

        public void Begin()
        {
            DisplayName = Names.Random();
            BaseSpeed = Random.Range(1.7f, 2.4f);
            patience = Random.Range(35f, 80f);

            int count = Random.Range(3, 11);
            for (int i = 0; i < count; i++)
            {
                var d = Catalog.RandomDepartment();
                var p = Catalog.RandomProduct(d);
                int qty = Random.value < 0.3f ? 2 : 1;
                Want existing = null;
                for (int k = 0; k < wants.Count; k++) if (wants[k].Product == p) existing = wants[k];
                if (existing != null) existing.Qty += qty;
                else wants.Add(new Want { Dept = d, Product = p, Qty = qty });
            }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            var store = Store.I;
            var visit = new List<Department>();
            for (int i = 0; i < wants.Count; i++)
            {
                var d = store.FindDepartment(wants[i].Dept);
                if (d == null || !d.Active)
                {
                    Lose("Things you don't sell", 2f);
                    store.Economy.Today.AddNotCarried(wants[i].Dept.Name);
                    continue;
                }
                if (!visit.Contains(d)) visit.Add(d);
            }
            visit.Sort((a, b) => a.Slot.CompareTo(b.Slot));

            Status = "Shopping";
            for (int v = 0; v < visit.Count; v++)
            {
                var d = visit[v];
                if (HurryUp || gaveUp) break;
                if (!d.Active) { Lose("Things you don't sell", 3f); continue; }

                if (d.IsCounter)
                {
                    yield return StartCoroutine(VisitCounter(d));
                    continue;
                }

                for (int i = 0; i < wants.Count; i++)
                {
                    if (wants[i].Dept != d.Def) continue;
                    if (HurryUp || gaveUp || !d.Active) break;
                    var ps = d.Find(wants[i].Product);
                    if (ps == null) continue;
                    Status = "Looking for " + ps.Def.Name;
                    yield return StartCoroutine(MoveTo(d.PickTile(ps)));
                    yield return StartCoroutine(Wait(Random.Range(1.5f, 3.5f)));
                    if (!d.Active) break;
                    TryTake(d, ps, wants[i].Qty);
                    if (wantsToComplain) yield return StartCoroutine(Complain());
                }
            }

            if (!gaveUp && Items > 0) yield return StartCoroutine(Checkout());
            yield return StartCoroutine(Leave());
        }

        void Lose(string reason, float points)
        {
            Satisfaction -= points;
            Store.I.Economy.Today.AddLoss(reason, points);
        }

        void TryTake(Department d, ProductStock ps, int qty)
        {
            if (ps.Shelf.Total <= 0)
            {
                ps.MissedToday++;
                Lose("Empty shelves", 7f);
                MaybeComplain("couldn't find " + ps.Def.Name);
                return;
            }

            float mult = d.PriceMult;
            if (mult > 1.001f)
            {
                float chance = Mathf.Clamp01(1f - (mult - 1f) * 1.3f);
                if (Random.value > chance)
                {
                    ps.PriceSkipsToday++;
                    Lose("Prices", 3f);
                    MaybeComplain(ps.Def.Name + " is too expensive");
                    return;
                }
                Lose("Prices", (mult - 1f) * 4f);
            }
            else
            {
                Satisfaction += (1f - mult) * 10f;
            }

            int got = ps.Shelf.Take(qty);
            if (got < qty) Lose("Empty shelves", 2f);
            Items += got;
            float value = got * ps.Price;
            CartValue += value;
            float cur;
            deptValue.TryGetValue(d, out cur);
            deptValue[d] = cur + value;
            picked.Add(new Picked { Stock = ps, Qty = got });
            ps.SoldToday += got;
            Satisfaction += 1f;

            if (Random.value < 0.006f) Store.I.Dirt.AddSpill(transform.position);
        }

        void MaybeComplain(string reason)
        {
            if (complainedOnce || wantsToComplain || Satisfaction > 40f) return;
            if (Random.value < 0.5f)
            {
                wantsToComplain = true;
                pendingReason = reason;
            }
        }

        IEnumerator Complain()
        {
            wantsToComplain = false;
            complainedOnce = true;
            Complaining = true;
            resolved = false;
            Responder = null;
            ComplaintReason = pendingReason;
            ComplaintTime = 0f;
            Stop();
            Status = "Wants to see the manager";
            Store.I.AddComplaint(this);
            ShowMarker(true);

            while (Complaining && ComplaintTime < ComplaintLimit)
            {
                ComplaintTime += Time.deltaTime;
                yield return null;
            }

            ShowMarker(false);
            Store.I.RemoveComplaint(this);
            if (!resolved)
            {
                Complaining = false;
                Store.I.Message(DisplayName + " walked out angry: nobody came about the complaint.");
                Store.I.Reputation = Mathf.Max(0f, Store.I.Reputation - 0.1f);
                Lose("Complaint ignored", 10f);
                Abandon();
                gaveUp = true;
            }
            else
            {
                Status = "Shopping";
            }
        }

        /// <summary>The manager or assistant manager dealt with the complaint.</summary>
        public void Resolve(Agent by, float bonus)
        {
            if (!Complaining) return;
            resolved = true;
            Complaining = false;
            Satisfaction += bonus;
            Store.I.Economy.Today.ComplaintsResolved++;
            Store.I.Reputation = Mathf.Min(100f, Store.I.Reputation + 0.05f);
            Status = "Calmed down";
        }

        IEnumerator VisitCounter(Department d)
        {
            var store = Store.I;
            Status = "Queueing at the " + d.Name + " counter";
            d.CounterQueue.Add(this);
            CounterServed = false;
            Station = null;
            float waited = 0f;
            var spot = new Vector2Int(-99, -99);
            bool served = false;

            while (true)
            {
                if (!d.Active) break;
                if (CounterServed) { served = true; break; }

                // The clerk at our station left: back to the front of the line.
                if (Station != null && !Station.Open)
                {
                    if (Station.Customer == this) Station.Customer = null;
                    Station = null;
                    AtCounter = false;
                    d.CounterQueue.Insert(0, this);
                }
                if (Station == null && d.CounterQueue.IndexOf(this) == 0)
                {
                    var free = d.FreeStationForCustomer();
                    if (free != null)
                    {
                        Station = free;
                        free.Customer = this;
                        d.CounterQueue.Remove(this);
                        Status = "At the " + d.Name + " counter";
                    }
                }

                Vector2Int want;
                if (Station != null) want = Station.ServiceTile;
                else want = d.QueueTiles[Mathf.Clamp(d.CounterQueue.IndexOf(this), 0, d.QueueTiles.Count - 1)];
                if (want != spot) { spot = want; GoTo(spot); }
                AtCounter = Station != null && !IsMoving;

                waited += Time.deltaTime;
                if (waited > 10f) Lose("Waiting at the counter", Time.deltaTime * 0.3f);
                bool staffed = store.CounterStaffed(d);
                if ((!staffed && waited > 15f) || waited > patience * 2f || (HurryUp && !staffed))
                {
                    Lose("Waiting at the counter", 5f);
                    MaybeComplain("nobody served at the " + d.Name + " counter");
                    break;
                }
                yield return null;
            }

            d.CounterQueue.Remove(this);
            if (Station != null && Station.Customer == this) Station.Customer = null;
            Station = null;
            AtCounter = false;

            if (served)
            {
                for (int i = 0; i < wants.Count; i++)
                {
                    if (wants[i].Dept != d.Def) continue;
                    var ps = d.Find(wants[i].Product);
                    if (ps != null) TryTake(d, ps, wants[i].Qty);
                }
            }
            else
            {
                for (int i = 0; i < wants.Count; i++)
                {
                    if (wants[i].Dept != d.Def) continue;
                    var ps = d.Find(wants[i].Product);
                    if (ps != null) ps.MissedToday++;
                }
            }
            if (wantsToComplain) yield return StartCoroutine(Complain());
            Status = "Shopping";
        }

        IEnumerator Checkout()
        {
            var store = Store.I;
            float noLaneTime = 0f, waited = 0f;
            var spot = new Vector2Int(-99, -99);

            while (!Paid)
            {
                if (Lane == null)
                {
                    var lane = store.BestLane();
                    if (lane != null)
                    {
                        WaitingForLane = false;
                        Lane = lane;
                        lane.Queue.Add(this);
                        spot = new Vector2Int(-99, -99);
                        Status = "Queueing at checkout " + (lane.Index + 1);
                    }
                    else
                    {
                        Status = "Waiting for a checkout to open";
                        WaitingForLane = true;
                        if (!IsMoving && Tile.y > 11) GoTo(store.CheckoutWaitTile());
                        noLaneTime += Time.deltaTime;
                        Lose("No open checkout", Time.deltaTime * 0.4f);
                        if (noLaneTime > patience * 1.5f)
                        {
                            WaitingForLane = false;
                            Abandon();
                            gaveUp = true;
                            yield break;
                        }
                        yield return null;
                        continue;
                    }
                }

                if (!Lane.Staffed)
                {
                    Lane.Queue.Remove(this);
                    Lane = null;
                    AtServiceSpot = false;
                    yield return null;
                    continue;
                }

                int idx = Lane.Queue.IndexOf(this);
                Vector2Int want = Lane.QueueSpot(idx);
                if (want != spot) { spot = want; GoTo(spot); }
                AtServiceSpot = idx == 0 && !IsMoving;
                waited += Time.deltaTime;
                if (waited > 12f) Lose("Checkout queues", Time.deltaTime * 0.3f);
                if (idx > 0 && waited > patience * 2.5f)
                {
                    Lane.Queue.Remove(this);
                    Lane = null;
                    Abandon();
                    gaveUp = true;
                    yield break;
                }
                yield return null;
            }
            AtServiceSpot = false;
        }

        /// <summary>Called by the cashier when this customer has been rung up.</summary>
        public void Pay(Employee cashier)
        {
            if (Paid) return;
            Paid = true;
            Store.I.Economy.Sale(CartValue, Items);
            foreach (var kv in deptValue) kv.Key.SalesToday += kv.Value;
            if (cashier != null)
            {
                float service = (cashier.Service - 5f) * 1.5f;
                if (service < 0f) Lose("Unfriendly cashiers", -service);
                else Satisfaction += service;
            }
            if (Lane != null) Lane.Queue.Remove(this);
            Lane = null;
            AtServiceSpot = false;
            Status = "Paid";
        }

        /// <summary>Walks out without paying; the cart goes back on the shelves.</summary>
        void Abandon()
        {
            for (int i = 0; i < picked.Count; i++)
            {
                var p = picked[i];
                p.Stock.SoldToday -= p.Qty;
                if (p.Stock.Dept.Active)
                {
                    int life = p.Stock.Life > 0 ? Mathf.Max(1, p.Stock.Life / 2) : -1;
                    p.Stock.Shelf.Add(p.Qty, life);
                }
            }
            picked.Clear();
            deptValue.Clear();
            Items = 0;
            CartValue = 0f;
            Lose("Walked out", 20f);
            Store.I.Economy.Today.Walkouts++;
            Status = "Walking out";
        }

        IEnumerator Leave()
        {
            IsLeaving = true;
            WaitingForLane = false;
            if (Lane != null) { Lane.Queue.Remove(this); Lane = null; }
            if (!gaveUp) Status = Paid ? "Leaving" : "Leaving (nothing to buy)";
            yield return StartCoroutine(MoveTo(Store.I.RandomStreetTile()));
            Store.I.CustomerLeft(this);
            Destroy(gameObject);
        }

        protected override void Update()
        {
            base.Update();
            dirtTimer += Time.deltaTime;
            if (dirtTimer >= 1f)
            {
                dirtTimer = 0f;
                // Each spill a customer walks past upsets them once (up to four).
                if (spillsSeen.Count < 4)
                {
                    var s = Store.I.Dirt.NearestWithin(transform.position, 2.5f);
                    if (s != null && !spillsSeen.Contains(s))
                    {
                        spillsSeen.Add(s);
                        Lose("Dirty floors", 2.5f);
                    }
                }
            }
        }

        void ShowMarker(bool on)
        {
            if (on && marker == null)
            {
                marker = Mats.Prim(PrimitiveType.Cube, "Complaint", transform, Vector3.zero,
                    new Vector3(0.25f, 0.6f, 0.25f), new Color(0.95f, 0.15f, 0.1f), false);
                marker.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            }
            if (marker != null) marker.SetActive(on);
        }

        void OnDestroy()
        {
            if (Store.I == null) return;
            if (Lane != null) Lane.Queue.Remove(this);
            Store.I.ForgetCustomer(this);
        }
    }
}
