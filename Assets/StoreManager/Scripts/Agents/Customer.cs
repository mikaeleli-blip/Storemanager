using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public class Customer : Agent
    {
        class Want { public ProductDef Def; public int Qty; }
        class Picked { public Fixture Fixture; public Product Product; public int Qty; }

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
        public Fixture AtFixture;

        bool gaveUp;
        float patience;
        float dirtTimer;
        GameObject marker;

        readonly List<Want> wants = new List<Want>();
        readonly List<Picked> picked = new List<Picked>();
        readonly Dictionary<Category, float> categoryValue = new Dictionary<Category, float>();
        readonly List<Spill> spillsSeen = new List<Spill>();

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
                var p = Store.I.PickWantedProduct();
                int qty = Random.value < 0.3f ? 2 : 1;
                Want existing = null;
                for (int k = 0; k < wants.Count; k++) if (wants[k].Def == p) existing = wants[k];
                if (existing != null) existing.Qty += qty;
                else wants.Add(new Want { Def = p, Qty = qty });
            }
            StartCoroutine(Run());
        }

        void Lose(string reason, float points)
        {
            Satisfaction -= points;
            Store.I.Economy.Today.AddLoss(reason, points);
        }

        /// <summary>Best place to get a product: a stocked fixture close by, else the nearest one.</summary>
        static Fixture BestFixture(Product p, Vector3 from)
        {
            Fixture best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < p.Fixtures.Count; i++)
            {
                var f = p.Fixtures[i];
                if (f.Removed) continue;
                float d = (f.Center - from).magnitude + (f.Stock.Total > 0 ? 0f : 1000f);
                if (d < bestScore) { bestScore = d; best = f; }
            }
            return best;
        }

        IEnumerator Run()
        {
            var store = Store.I;
            var todo = new List<Want>();
            for (int i = 0; i < wants.Count; i++)
            {
                var p = store.ProductOf(wants[i].Def);
                if (p == null || !p.Carried)
                {
                    Lose("Things you don't sell", 2f);
                    store.Economy.Today.AddNotCarried(wants[i].Def.Category.Name);
                    continue;
                }
                todo.Add(wants[i]);
            }

            Status = "Shopping";
            while (todo.Count > 0 && !HurryUp && !gaveUp)
            {
                // Nearest next stop from where we are.
                Want next = null;
                Fixture target = null;
                float bd = float.MaxValue;
                for (int i = 0; i < todo.Count; i++)
                {
                    var p = store.ProductOf(todo[i].Def);
                    var f = BestFixture(p, transform.position);
                    if (f == null) continue;
                    float d = (f.Center - transform.position).sqrMagnitude;
                    if (d < bd) { bd = d; next = todo[i]; target = f; }
                }
                if (next == null)
                {
                    for (int i = 0; i < todo.Count; i++) Lose("Things you don't sell", 2f);
                    break;
                }
                todo.Remove(next);
                var product = store.ProductOf(next.Def);

                if (target.IsCounter)
                {
                    yield return StartCoroutine(VisitCounter(target, product, next.Qty));
                    continue;
                }

                Status = "Looking for " + product.Name;
                yield return StartCoroutine(MoveTo(target.AccessTile(store.Grid)));
                yield return StartCoroutine(Wait(Random.Range(1.5f, 3.5f)));
                if (target.Removed || target.Product != product)
                {
                    var other = BestFixture(product, transform.position);
                    if (other == null || other.IsCounter) { product.MissedToday++; Lose("Empty shelves", 4f); continue; }
                    target = other;
                    yield return StartCoroutine(MoveTo(target.AccessTile(store.Grid)));
                }
                TryTake(target, product, next.Qty);
                if (wantsToComplain) yield return StartCoroutine(Complain());
            }

            if (!gaveUp && Items == 0 && wants.Count > 0) Lose("Found nothing to buy", 15f);
            if (!gaveUp && Items > 0) yield return StartCoroutine(Checkout());
            yield return StartCoroutine(Leave());
        }

        void TryTake(Fixture f, Product p, int qty)
        {
            if (f.Removed || f.Product != p || f.Stock.Total <= 0)
            {
                p.MissedToday++;
                Lose("Empty shelves", 7f);
                MaybeComplain("couldn't find " + p.Name);
                return;
            }

            // Expired items still on the shelf: the more there are, the likelier this customer picks one up.
            // Counter staff never hand over expired stock.
            int expired = f.Stock.ExpiredTotal;
            if (expired > 0 && !f.IsCounter && Random.value < expired / (float)f.Stock.Total)
            {
                p.ExpiredFoundToday++;
                Store.I.Economy.Today.ExpiredFound++;
                Lose("Expired products", 9f);
                MaybeComplain("found expired " + p.Name);
            }
            if (f.Stock.FreshTotal <= 0)
            {
                p.MissedToday++;
                Lose("Empty shelves", 4f);
                return;
            }

            float mult = p.Category.PriceMult;
            if (mult > 1.001f)
            {
                float chance = Mathf.Clamp01(1f - (mult - 1f) * 1.3f);
                if (Random.value > chance)
                {
                    p.PriceSkipsToday++;
                    Lose("Prices", 3f);
                    MaybeComplain(p.Name + " is too expensive");
                    return;
                }
                Lose("Prices", (mult - 1f) * 4f);
            }
            else
            {
                Satisfaction += (1f - mult) * 10f;
            }

            // A quarter of shoppers reach to the back for the freshest date; that leaves older stock to expire.
            int got = p.Life > 0 && !f.IsCounter && Random.value < 0.25f ? f.Stock.TakeNewest(qty) : f.Stock.TakeFresh(qty);
            if (got < qty) Lose("Empty shelves", 2f);
            Items += got;
            float value = got * p.Price;
            CartValue += value;
            float cur;
            categoryValue.TryGetValue(p.Category, out cur);
            categoryValue[p.Category] = cur + value;
            picked.Add(new Picked { Fixture = f, Product = p, Qty = got });
            p.SoldToday += got;
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

        IEnumerator VisitCounter(Fixture f, Product p, int qty)
        {
            var store = Store.I;
            Status = "Queueing at the " + p.Name + " counter";
            f.Waiting.Add(this);
            AtFixture = f;
            CounterServed = false;
            float waited = 0f;
            var spot = new Vector2Int(-99, -99);
            bool served = false;

            while (true)
            {
                if (f.Removed || f.Product != p) break;
                if (CounterServed) { served = true; break; }
                int idx = f.Waiting.IndexOf(this);
                Vector2Int want = f.QueueSpot(Mathf.Max(0, idx), store.Grid);
                if (want != spot) { spot = want; GoTo(spot); }
                AtCounter = idx == 0 && !IsMoving;
                waited += Time.deltaTime;
                if (waited > 10f) Lose("Waiting at the counter", Time.deltaTime * 0.3f);
                bool staffed = store.CounterStaffed(f);
                if ((!staffed && waited > 15f) || waited > patience * 2f || (HurryUp && !staffed))
                {
                    Lose("Waiting at the counter", 5f);
                    MaybeComplain("nobody served at the counter");
                    break;
                }
                yield return null;
            }

            f.Waiting.Remove(this);
            AtCounter = false;
            AtFixture = null;
            if (served) TryTake(f, p, qty);
            else p.MissedToday++;
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
                        if (!IsMoving && noLaneTime < 0.01f) GoTo(store.CheckoutWaitTile());
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
            Store.I.AddXP(2 + Mathf.FloorToInt(CartValue / 5f));
            foreach (var kv in categoryValue) kv.Key.SalesToday += kv.Value;
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
                var pk = picked[i];
                pk.Product.SoldToday -= pk.Qty;
                int life = pk.Product.Life > 0 ? Mathf.Max(1, pk.Product.Life / 2) : -1;
                if (!pk.Fixture.Removed && pk.Fixture.Product == pk.Product) pk.Fixture.Stock.Add(pk.Qty, life);
                else if (!pk.Product.Baked) pk.Product.Back.Add(pk.Qty, life);
            }
            picked.Clear();
            categoryValue.Clear();
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
