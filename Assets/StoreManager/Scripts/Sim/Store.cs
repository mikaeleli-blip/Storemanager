using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>The store and everything in it. Created by Bootstrap at startup.</summary>
    public class Store : MonoBehaviour
    {
        public static Store I;

        public GameClock Clock;
        public CameraRig Cam;
        public StoreGrid Grid;
        public StoreBuilder Builder;
        public StaffManager Staff;
        public DirtManager Dirt;
        public ManagerAvatar Avatar;
        public readonly Economy Economy = new Economy();
        public readonly Delegation Delegation = new Delegation();

        public int SlotCount = Layout.StartSlots;   // 12 m sections of floor
        public bool ExpansionPending;
        public float Reputation = 50f;

        public readonly List<Category> Categories = new List<Category>();
        public readonly List<Product> Products = new List<Product>();
        readonly Dictionary<ProductDef, Product> productOf = new Dictionary<ProductDef, Product>();
        readonly Dictionary<CategoryDef, Category> categoryOf = new Dictionary<CategoryDef, Category>();

        public readonly List<Fixture> Fixtures = new List<Fixture>();
        readonly Dictionary<Vector2Int, Fixture> fixtureAt = new Dictionary<Vector2Int, Fixture>();
        int nextFixtureId = 1;

        public readonly List<CheckoutLane> Lanes = new List<CheckoutLane>();
        public readonly List<Customer> Customers = new List<Customer>();
        public readonly List<Customer> Complaints = new List<Customer>();
        public readonly List<string> Log = new List<string>();

        float spawnAcc, visualsTimer, truckTimer, expressTimer = -1f;
        public bool ExpressPending { get { return expressTimer >= 0f; } }

        public const float BaseTraffic = 0.28f;
        public const float ParDays = 2f;
        public const float ExpressSurcharge = 0.25f;
        public const float SellRefund = 0.5f;

        static readonly float[] HourWeight =
        {
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0.5f, 0.6f, 0.7f, 0.9f, 1.2f, 1.0f, 0.8f, 0.9f, 1.1f, 1.4f, 1.4f, 1.1f, 0.8f, 0.5f,
            0f, 0f
        };
        static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.84f, 0.72f), new Color(0.9f, 0.74f, 0.6f), new Color(0.78f, 0.6f, 0.45f),
            new Color(0.6f, 0.43f, 0.3f), new Color(0.42f, 0.29f, 0.2f), new Color(0.98f, 0.88f, 0.8f)
        };

        public int Expansions { get { return SlotCount - Layout.StartSlots; } }
        public int BackCapacity { get { return 9000 + 2000 * Expansions; } }
        public float Rent { get { return 700f + 300f * Expansions; } }
        public float ExpansionCost { get { return 15000f * (Expansions + 1); } }
        public Vector2Int StaffEntrance { get { return new Vector2Int(Grid.W - 4, Layout.DockRow); } }
        public Vector2Int DeskSeat { get { return new Vector2Int(3, 30); } }

        /// <summary>Share of everything customers ask for that the store carries (by demand).</summary>
        public float Variety
        {
            get
            {
                float carried = 0f;
                for (int i = 0; i < Products.Count; i++) if (Products[i].Carried) carried += Products[i].Def.Demand;
                return Catalog.TotalDemand > 0f ? carried / Catalog.TotalDemand : 0f;
            }
        }

        public float TrafficFactor
        {
            get { return (0.4f + Reputation / 100f * 1.2f) * (0.55f + 0.75f * Variety); }
        }

        public float ExpectedCustomersPerDay
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < HourWeight.Length; i++) sum += HourWeight[i];
                return sum * (60f / GameClock.GameMinutesPerSecond) * BaseTraffic * TrafficFactor;
            }
        }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            I = this;
            Clock = gameObject.AddComponent<GameClock>();
            Clock.SetSpeed(0);   // start paused: build first
            Dirt = new DirtManager(transform);
            Staff = new StaffManager(this);
            Builder = new StoreBuilder(this);

            for (int c = 0; c < Catalog.Categories.Count; c++)
            {
                var cat = new Category(Catalog.Categories[c]);
                Categories.Add(cat);
                categoryOf[cat.Def] = cat;
                for (int p = 0; p < cat.Def.Products.Count; p++)
                {
                    var prod = new Product(cat.Def.Products[p], cat) { Par = DefaultPar(cat.Def.Products[p]) };
                    cat.Products.Add(prod);
                    Products.Add(prod);
                    productOf[prod.Def] = prod;
                }
            }

            Builder.BuildAll();
            Staff.RefreshApplicants(true);
            Staff.HireStartingStaff();
            SpawnAvatar();
            SetupCameraAndLight();
            gameObject.AddComponent<Hud>();
            gameObject.AddComponent<BuildMode>();

            Clock.HourChanged += OnHour;
            Clock.NewDay += OnNewDay;
            Economy.Today.Day = Clock.Day;
            Economy.PayRent(Rent);
            Message("Your store is empty. Place checkouts and shelves, pick what goes on each shelf, then order stock.");
            Message("Time is paused. Press 1-4 or the speed buttons when you're ready to open.");
        }

        void Start()
        {
            Avatar.SitDown();
            BuildMode.I.Open();
        }

        void SpawnAvatar()
        {
            var go = new GameObject("Manager (You)");
            Avatar = go.AddComponent<ManagerAvatar>();
            Avatar.DisplayName = "You";
            Avatar.BuildBody(new Color(0.12f, 0.12f, 0.16f), SkinTones[1], 1.75f);
            var tag = Mats.Prim(PrimitiveType.Cube, "Manager Marker", go.transform, Vector3.zero,
                new Vector3(0.3f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.1f), false);
            tag.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            tag.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Avatar.Teleport(new Vector2Int(5, 29));
        }

        void SetupCameraAndLight()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 600f;
            Cam = cam.GetComponent<CameraRig>();
            if (Cam == null) Cam = cam.gameObject.AddComponent<CameraRig>();
            Cam.FollowTarget = Avatar.transform;
            Cam.Focus = new Vector3(Grid.W * 0.5f, 0f, 16f);
            UpdateCameraBounds();

            var atmosphere = new GameObject("Atmosphere").AddComponent<Atmosphere>();
            atmosphere.transform.SetParent(transform, false);
            atmosphere.Init(cam);
            atmosphere.Apply(Clock.Minute / 60f);
        }

        void UpdateCameraBounds()
        {
            if (Cam == null) return;
            Cam.BoundsMin = new Vector2(-4f, -4f);
            Cam.BoundsMax = new Vector2(Grid.W + 4f, Layout.Depth + 4f);
        }

        public static float ExpectedDaily(ProductDef p)
        {
            return 470f * 6.5f * 1.3f * p.Demand / Catalog.TotalDemand;
        }

        public static int DefaultPar(ProductDef p)
        {
            int par = Mathf.CeilToInt(ExpectedDaily(p) * ParDays / p.CaseSize) * p.CaseSize;
            return Mathf.Max(par, p.CaseSize * 2);
        }

        public Product ProductOf(ProductDef def)
        {
            Product p;
            return productOf.TryGetValue(def, out p) ? p : null;
        }

        public Category CategoryOf(CategoryDef def)
        {
            Category c;
            return categoryOf.TryGetValue(def, out c) ? c : null;
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            SpawnCustomers();

            visualsTimer += Time.unscaledDeltaTime;
            if (visualsTimer > 0.4f)
            {
                visualsTimer = 0f;
                UpdateVisuals();
            }

            if (truckTimer > 0f)
            {
                truckTimer -= Time.deltaTime;
                if (truckTimer <= 0f && Builder.Truck != null) Builder.Truck.SetActive(false);
            }
            if (expressTimer >= 0f)
            {
                expressTimer -= Time.deltaTime * GameClock.GameMinutesPerSecond;
                if (expressTimer < 0f) Deliver(true);
            }
        }

        void SpawnCustomers()
        {
            if (!Clock.IsOpen) return;
            if (Clock.Hour == GameClock.CloseHour - 1 && Clock.Minute % 60f > 40f) return;
            float perSecond = HourWeight[Clock.Hour] * BaseTraffic * TrafficFactor;
            spawnAcc += perSecond * Time.deltaTime;
            while (spawnAcc >= 1f)
            {
                spawnAcc -= 1f;
                if (Customers.Count < 110) SpawnCustomer();
            }
        }

        void SpawnCustomer()
        {
            var go = new GameObject("Customer");
            var c = go.AddComponent<Customer>();
            Color col = Color.HSVToRGB(Random.value, Random.Range(0.25f, 0.55f), Random.Range(0.45f, 0.85f));
            c.BuildBody(col, SkinTones[Random.Range(0, SkinTones.Length)], Random.Range(1.5f, 1.8f));
            c.Teleport(RandomStreetTile());
            Customers.Add(c);
            c.Begin();
        }

        void UpdateVisuals()
        {
            for (int i = 0; i < Fixtures.Count; i++) Fixtures[i].UpdateVisuals();
            for (int i = 0; i < Lanes.Count; i++)
            {
                var lane = Lanes[i];
                if (lane.Light == null) continue;
                Color c = lane.Accepting && !lane.Closing ? new Color(0.2f, 0.85f, 0.3f)
                    : lane.Staffed ? new Color(0.95f, 0.8f, 0.2f)
                    : new Color(0.85f, 0.15f, 0.12f);
                lane.Light.sharedMaterial = Mats.Glow(c);
            }
        }

        void OnHour(int h)
        {
            if (h == 6)
            {
                CompleteConstruction();
                Deliver(false);
            }
            if (h == GameClock.OpenHour) Message("The store is open.");
            if (h == GameClock.CloseHour)
            {
                for (int i = 0; i < Customers.Count; i++) if (Customers[i] != null) Customers[i].HurryUp = true;
                Message("Closing time. Sales today: " + Economy.Money(Economy.Today.Sales) + ".");
            }

            var am = Staff.Assistant;
            if (am != null && am.OnShift && Delegation.Ordering)
            {
                int orderHour = (StaffManager.ShiftEnd(am.Shift) + 23) % 24;
                if (h == orderHour)
                {
                    float err = Mathf.Clamp((10f - am.Leadership) * 0.04f, 0f, 0.4f);
                    int lines = OrderAllToPar(err);
                    Message(am.DisplayName + " placed the stock order (" + lines + " products). Arrives 06:00.");
                }
            }

            Staff.OnHour(h);
        }

        void OnNewDay()
        {
            var today = Economy.Today;
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                int w = p.Back.Age();
                for (int f = 0; f < p.Fixtures.Count; f++) w += p.Fixtures[f].Stock.Age();
                p.WastedLastNight = w;
                today.WasteValue += w * p.Def.Cost;
            }

            var done = Economy.CloseDay(Clock.Day);
            if (done.Customers > 0)
                Reputation = Mathf.Clamp(Reputation + (Mathf.Clamp(done.AvgSatisfaction, 0f, 100f) - Reputation) * 0.2f, 0f, 100f);
            Message("Day " + done.Day + " done: sales " + Economy.Money(done.Sales) + ", profit " + Economy.Money(done.Profit)
                + ", " + done.Customers + " customers, satisfaction " + done.AvgSatisfaction.ToString("0") + ".");

            for (int i = 0; i < Products.Count; i++) Products[i].ResetDaily();
            for (int i = 0; i < Categories.Count; i++) Categories[i].SalesToday = 0f;
            Economy.PayRent(Rent);
            Staff.RefreshApplicants(false);
            if (Economy.Cash < 0f) Message("Warning: the store is in the red.");
        }

        // ------------------------------------------------------------------ fixtures

        public Fixture FixtureAt(Vector2Int t)
        {
            Fixture f;
            return fixtureAt.TryGetValue(t, out f) ? f : null;
        }

        /// <summary>Tiles people stand on (cashier, clerk, baker, counter/till customer spot) can't be built over.</summary>
        public bool IsReserved(Vector2Int t, Fixture ignore)
        {
            for (int i = 0; i < Fixtures.Count; i++)
            {
                var f = Fixtures[i];
                if (f == ignore || f.Removed) continue;
                if ((f.Def.HasWork && f.Work == t) || (f.Def.HasService && f.Service == t) || (f.Def.HasCashier && f.Cashier == t))
                    return true;
            }
            return false;
        }

        public string CanPlace(FixtureDef def, Vector2Int origin, int rot)
        {
            if (Economy.Cash < def.Cost) return "Not enough cash (" + Economy.Money(def.Cost) + ").";
            return Placement.Validate(this, def, origin, rot, null);
        }

        public Fixture Place(FixtureDef def, Vector2Int origin, int rot)
        {
            if (CanPlace(def, origin, rot) != null) return null;
            Economy.PayConstruction(def.Cost);
            var f = new Fixture(def, nextFixtureId++);
            f.SetPlacement(origin, rot);
            Fixtures.Add(f);
            Occupy(f);
            if (f.IsCheckout) RebuildLanes();
            Builder.BuildFixture(f);
            RepathAll();
            return f;
        }

        public string Move(Fixture f, Vector2Int origin, int rot)
        {
            string why = Placement.Validate(this, f.Def, origin, rot, f);
            if (why != null) return why;
            Vacate(f);
            f.SetPlacement(origin, rot);
            Occupy(f);
            Builder.BuildFixture(f);
            if (f.Lane != null) f.Lane.Queue.Clear();
            f.Waiting.Clear();
            RepathAll();
            return null;
        }

        public void Sell(Fixture f)
        {
            if (f == null || f.Removed) return;
            AssignProduct(f, null);
            f.Removed = true;
            Vacate(f);
            Fixtures.Remove(f);
            if (f.Lane != null)
            {
                f.Lane.Queue.Clear();
                RebuildLanes();
            }
            f.Waiting.Clear();
            Builder.DestroyFixture(f);
            Economy.PayConstruction(-f.Def.Cost * SellRefund);
            RepathAll();
        }

        void Occupy(Fixture f)
        {
            for (int i = 0; i < f.Tiles.Count; i++)
            {
                fixtureAt[f.Tiles[i]] = f;
                Grid.SetBlocked(f.Tiles[i].x, f.Tiles[i].y, true);
            }
        }

        void Vacate(Fixture f)
        {
            for (int i = 0; i < f.Tiles.Count; i++)
            {
                Fixture at;
                if (fixtureAt.TryGetValue(f.Tiles[i], out at) && at == f)
                {
                    fixtureAt.Remove(f.Tiles[i]);
                    Grid.SetBlocked(f.Tiles[i].x, f.Tiles[i].y, false);
                }
            }
        }

        /// <summary>After a rebuild of the grid (expansion), mark every fixture's tiles again.</summary>
        public void ReapplyFixtures()
        {
            fixtureAt.Clear();
            for (int i = 0; i < Fixtures.Count; i++) Occupy(Fixtures[i]);
        }

        /// <summary>Put a product on a shelf or counter (or clear it). Stock already on it goes to the back room.</summary>
        public bool AssignProduct(Fixture f, ProductDef def)
        {
            if (!f.HoldsProducts) return false;
            if (def != null && def.Category.Fixture != f.Def.Id) return false;
            if (f.Product != null)
            {
                var old = f.Product;
                if (!old.Baked) f.Stock.MoveTo(old.Back, f.Stock.Total);
                f.Stock.Clear();
                old.Fixtures.Remove(f);
            }
            f.Product = def != null ? ProductOf(def) : null;
            if (f.Product != null) f.Product.Fixtures.Add(f);
            if (f.ClaimedBy != null) f.ClaimedBy.Restart();
            Builder.BuildStockRows(f);
            return true;
        }

        void RebuildLanes()
        {
            var keep = new List<CheckoutLane>();
            for (int i = 0; i < Fixtures.Count; i++)
            {
                var f = Fixtures[i];
                if (!f.IsCheckout || f.Removed) continue;
                if (f.Lane == null) f.Lane = new CheckoutLane(f);
                keep.Add(f.Lane);
            }
            for (int i = 0; i < Lanes.Count; i++)
            {
                var old = Lanes[i];
                if (!keep.Contains(old) && old.Cashier != null) old.Cashier.Restart();
            }
            Lanes.Clear();
            Lanes.AddRange(keep);
            for (int i = 0; i < Lanes.Count; i++) Lanes[i].Index = i;
        }

        public int CountFixtures(string id)
        {
            int n = 0;
            for (int i = 0; i < Fixtures.Count; i++) if (Fixtures[i].Def.Id == id) n++;
            return n;
        }

        public Fixture NearestFixture(string id, Vector3 from)
        {
            Fixture best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < Fixtures.Count; i++)
            {
                var f = Fixtures[i];
                if (f.Def.Id != id || f.Removed) continue;
                float d = (f.Center - from).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        public Vector2Int BackPickNear(Fixture f)
        {
            int x = f != null ? f.Origin.x : 10;
            return new Vector2Int(Mathf.Clamp(x, 10, Grid.W - 2), 28);
        }

        // ------------------------------------------------------------------ expansion

        public bool CanExpand
        {
            get { return SlotCount < Layout.MaxSlots && !ExpansionPending && Economy.Cash >= ExpansionCost; }
        }

        public void Expand()
        {
            if (!CanExpand) return;
            Economy.PayConstruction(ExpansionCost);
            ExpansionPending = true;
            Message("Expansion ordered: 12 m more floor space and a bigger back room. Ready at 06:00.");
        }

        void CompleteConstruction()
        {
            if (!ExpansionPending) return;
            ExpansionPending = false;
            SlotCount++;
            Builder.BuildAll();
            ReapplyFixtures();
            UpdateCameraBounds();
            RepathAll();
            Message("Expansion finished: the store is " + Grid.W + " m wide now.");
        }

        public void RepathAll()
        {
            for (int i = 0; i < Customers.Count; i++) if (Customers[i] != null) Customers[i].Repath();
            for (int i = 0; i < Staff.All.Count; i++) if (Staff.All[i] != null) Staff.All[i].Repath();
            if (Avatar != null) Avatar.Repath();
        }

        // ------------------------------------------------------------------ stock orders

        public int BackStockTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].Back.Total; return t; }
        }

        public int OnOrderTotal
        {
            get { int t = 0; for (int i = 0; i < Products.Count; i++) t += Products[i].OnOrder; return t; }
        }

        public float OnOrderCost
        {
            get { float t = 0f; for (int i = 0; i < Products.Count; i++) t += Products[i].OnOrder * Products[i].Def.Cost; return t; }
        }

        public void Order(Product p, int qty)
        {
            if (qty > 0 && !p.Baked) p.OnOrder += qty;
        }

        public int OrderToPar(Product p, float error)
        {
            if (p.Baked || !p.Carried) return 0;
            int need = p.Par - (p.OnHand + p.OnOrder);
            if (need <= 0) return 0;
            float q = need;
            if (error > 0f) q *= Random.Range(1f - error, 1f + error);
            int cases = Mathf.Max(1, Mathf.CeilToInt(q / p.Def.CaseSize));
            p.OnOrder += cases * p.Def.CaseSize;
            return 1;
        }

        public int OrderAllToPar(float error)
        {
            int lines = 0;
            for (int i = 0; i < Products.Count; i++) lines += OrderToPar(Products[i], error);
            return lines;
        }

        /// <summary>Send everything on order now: arrives in an hour, costs 25% more.</summary>
        public bool OrderExpress()
        {
            if (OnOrderTotal == 0 || ExpressPending) return false;
            expressTimer = 60f;
            Message("Express delivery booked: arrives in about an hour (+25%).");
            return true;
        }

        void Deliver(bool express)
        {
            if (express) expressTimer = -1f;
            int space = BackCapacity - BackStockTotal;
            float cost = 0f;
            int units = 0, refused = 0;
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                if (p.OnOrder <= 0) continue;
                int q = Mathf.Min(p.OnOrder, Mathf.Max(0, space));
                p.Back.Add(q, p.Life);
                space -= q;
                units += q;
                cost += q * p.Def.Cost;
                refused += p.OnOrder - q;
                p.OnOrder = 0;
            }
            if (express) cost *= 1f + ExpressSurcharge;
            if (units > 0)
            {
                Economy.PayWholesale(cost);
                Message((express ? "Express delivery" : "Delivery") + " arrived: " + units + " units for " + Economy.Money(cost) + "."
                    + (refused > 0 ? " " + refused + " units refused: the back room is full." : ""));
                if (Builder.Truck != null) Builder.Truck.SetActive(true);
                truckTimer = 60f;
            }
            else if (!express)
            {
                bool carriesAnything = false;
                for (int i = 0; i < Products.Count; i++) if (Products[i].Carried && !Products[i].Baked) carriesAnything = true;
                if (carriesAnything) Message("No delivery today: nothing was ordered.");
            }
        }

        // ------------------------------------------------------------------ customers, checkouts, complaints

        public Vector2Int RandomStreetTile()
        {
            return new Vector2Int(Random.Range(0, Grid.W), Random.Range(0, 2));
        }

        public Vector2Int CheckoutWaitTile()
        {
            if (Lanes.Count > 0)
            {
                var l = Lanes[Random.Range(0, Lanes.Count)];
                return Grid.NearestWalkable(l.QueueSpot(6) + l.Fixture.QueueDir, false);
            }
            return new Vector2Int(Random.Range(6, Mathf.Min(Grid.W - 1, 20)), Random.Range(6, 11));
        }

        public Vector2Int RandomFloorTile()
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var t = new Vector2Int(Random.Range(0, Grid.W), Random.Range(Layout.LobbyStart, Layout.FloorEnd + 1));
                if (Grid.Walkable(t, false)) return t;
            }
            return new Vector2Int(3, Layout.LobbyStart + 2);
        }

        public CheckoutLane BestLane()
        {
            CheckoutLane best = null;
            for (int i = 0; i < Lanes.Count; i++)
            {
                var l = Lanes[i];
                if (!l.Accepting) continue;
                if (best == null || l.Queue.Count < best.Queue.Count) best = l;
            }
            return best;
        }

        public CheckoutLane FreeLane()
        {
            for (int i = 0; i < Lanes.Count; i++)
                if (Lanes[i].Cashier == null && !Lanes[i].Removed) return Lanes[i];
            return null;
        }

        public int OpenLaneCount
        {
            get { int n = 0; for (int i = 0; i < Lanes.Count; i++) if (Lanes[i].Accepting && !Lanes[i].Closing) n++; return n; }
        }

        public int CustomersWaitingForLane
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Customers.Count; i++)
                    if (Customers[i] != null && Customers[i].WaitingForLane) n++;
                return n;
            }
        }

        /// <summary>True when checkout queues are long enough that another till should open.</summary>
        public bool QueuePressure()
        {
            int open = 0, queued = 0;
            for (int i = 0; i < Lanes.Count; i++)
            {
                if (!Lanes[i].Staffed) continue;
                open++;
                queued += Lanes[i].Queue.Count;
            }
            if (open == 0) return CustomersWaitingForLane > 0;
            return queued / (float)open > 3f || CustomersWaitingForLane > 0;
        }

        public bool AnyoneShopping()
        {
            for (int i = 0; i < Customers.Count; i++)
                if (Customers[i] != null && !Customers[i].IsLeaving) return true;
            return false;
        }

        public bool CounterStaffed(Fixture f)
        {
            return (f.Clerk != null && f.Clerk.OnShift) || Staff.OnShiftCount(Role.DeliClerk) > 0;
        }

        public void CustomerLeft(Customer c)
        {
            Customers.Remove(c);
            Economy.Today.Customers++;
            Economy.Today.SatisfactionSum += Mathf.Clamp(c.Satisfaction, 0f, 100f);
        }

        public void ForgetCustomer(Customer c)
        {
            Customers.Remove(c);
            Complaints.Remove(c);
            for (int i = 0; i < Fixtures.Count; i++) Fixtures[i].Waiting.Remove(c);
            for (int i = 0; i < Lanes.Count; i++) Lanes[i].Queue.Remove(c);
        }

        public void AddComplaint(Customer c)
        {
            if (!Complaints.Contains(c)) Complaints.Add(c);
            Economy.Today.Complaints++;
            Message(c.DisplayName + " wants to see the manager: " + c.ComplaintReason + ".");
        }

        public void RemoveComplaint(Customer c)
        {
            Complaints.Remove(c);
        }

        public Customer UnansweredComplaint()
        {
            for (int i = 0; i < Complaints.Count; i++)
            {
                var c = Complaints[i];
                if (c != null && c.Complaining && c.Responder == null) return c;
            }
            return null;
        }

        public void Message(string text)
        {
            string stamp = Clock != null ? Clock.Weekday + " " + Clock.TimeText + "  " : "";
            Log.Add(stamp + text);
            if (Log.Count > 80) Log.RemoveAt(0);
        }
    }
}
