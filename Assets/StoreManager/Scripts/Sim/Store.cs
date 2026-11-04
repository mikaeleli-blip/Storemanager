using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>The store and everything in it. Created by Bootstrap at startup.</summary>
    public partial class Store : MonoBehaviour
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

        // Progression
        public int Level = 1;
        public int XP;
        readonly HashSet<LicenceDef> owned = new HashSet<LicenceDef>();

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
        public float ExpressMinutesLeft { get { return Mathf.Max(0f, expressTimer); } }

        public const float BaseTraffic = 0.28f;
        public const float ParDays = 2f;
        public const float ExpressSurcharge = 0.25f;
        public const float SellRefund = 0.5f;

        /// <summary>Busy weekends, quiet start of the week (Mon..Sun, averages 1).</summary>
        public static readonly float[] DayFactor = { 0.9f, 0.9f, 0.95f, 1f, 1.1f, 1.25f, 0.9f };

        static readonly float[] HourWeight =
        {
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0.5f, 0.6f, 0.7f, 0.9f, 1.2f, 1.0f, 0.8f, 0.9f, 1.1f, 1.4f, 1.4f, 1.1f, 0.8f, 0.5f,
            0f, 0f
        };
        public int Expansions { get { return SlotCount - Layout.StartSlots; } }
        public int BackCapacity { get { return 5000 + 2000 * Expansions; } }
        public float Rent { get { return 350f + 175f * Expansions; } }
        public float ExpansionCost { get { return 8000f + 6000f * Expansions; } }
        public int ExpansionLevel
        {
            get { return Expansions < Catalog.ExpansionLevels.Length ? Catalog.ExpansionLevels[Expansions] : 99; }
        }
        public Vector2Int StaffEntrance { get { return new Vector2Int(Grid.W - 4, Layout.DockRow); } }
        public Vector2Int DeskSeat { get { return new Vector2Int(3, 30); } }

        /// <summary>Where staff sit on their break: by the sofa in the office.</summary>
        public Vector2Int BreakSpot(Employee e)
        {
            int k = Outfit.Seed(e.DisplayName) % 4;
            return new Vector2Int(5 + k % 2, 28 + k / 2);
        }
        /// <summary>Where the camera sits to look at the office computer screen (desk at 3.5, 31.5, monitor facing -Z).</summary>
        public Vector3 MonitorView { get { return new Vector3(3.5f, 1.18f, 30.72f); } }

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
            get { return (0.4f + Reputation / 100f * 1.2f) * (0.35f + 1.0f * Variety); }
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
            if (Suppliers.Count == 0) InitSuppliers();

            for (int i = 0; i < Catalog.Licences.Count; i++)
                if (Catalog.Licences[i].Level <= 1 && Catalog.Licences[i].Cost <= 0f) owned.Add(Catalog.Licences[i]);

            Builder.BuildAll();
            Staff.RefreshApplicants(true);
            SpawnAvatar();
            SetupCameraAndLight();
            gameObject.AddComponent<Hud>();
            gameObject.AddComponent<BuildMode>();

            Clock.HourChanged += OnHour;
            Clock.NewDay += OnNewDay;
            Economy.Today.Day = Clock.Day;
        }

        // ------------------------------------------------------------------ new game and opening

        /// <summary>True once you've picked how to start (on the office computer).</summary>
        public bool StartChosen;
        /// <summary>The doors are open for business (customers come during opening hours).</summary>
        public bool Opened;
        public int OpenedDay;
        public bool IsOpenNow { get { return Opened && Clock.IsOpen; } }

        /// <summary>freshTeam: no staff, the store opens when you say so. Otherwise a small crew and open from day one.</summary>
        public void ChooseStart(bool freshTeam)
        {
            if (StartChosen) return;
            StartChosen = true;
            Message("Your corner shop is empty. You have a Basic Groceries licence: pasta, canned soup, two colas and snacks.");
            if (freshTeam)
            {
                Staff.RefreshApplicants(true);
                Message("You're hiring everyone yourself. Interview applicants in the Hiring app, set their days in Roster, build and stock the store, then press Open the store.");
                Message("Time is paused. The clock can run before opening (no customers, no rent yet): interviews, references and deliveries take days.");
            }
            else
            {
                Staff.HireStartingStaff();
                OpenStore();
                Message("You have a small crew. Open the Build app to place a checkout and shelves, pick what goes on each shelf, then buy stock in the Shop app (express gets it here today).");
                Message("Time is paused. Press 1-4 or the speed buttons when you're ready. Sales earn XP; levels unlock licences.");
            }
        }

        /// <summary>What's missing before the doors can open, or null.</summary>
        public string OpenProblem()
        {
            if (Opened) return "Already open.";
            if (Lanes.Count == 0) return "Place at least one checkout (Build).";
            bool any = false, stock = false;
            for (int i = 0; i < Products.Count; i++)
            {
                if (Products[i].Carried) any = true;
                if (Products[i].OnHand > 0 || Products[i].Incoming > 0) stock = true;
            }
            if (!any) return "Put at least one product on a shelf (Build, click a shelf).";
            if (!stock) return "Order some stock (Shop).";
            if (!Staff.AnyScheduled(Role.Cashier)) return "Hire at least one cashier (Hiring).";
            return null;
        }

        public void OpenStore()
        {
            if (Opened) return;
            Opened = true;
            // Opening after closing time means the first real day is tomorrow.
            OpenedDay = Clock.Hour >= GameClock.CloseHour ? Clock.Day + 1 : Clock.Day;
            Economy.PayRent(Rent);
            PutUpOpeningDecor();
            if (Clock.IsOpen)
                Message("You flip the sign to OPEN. It's not a big shop, but it's yours. A few curious neighbours are already at the door.");
            else
                Message("The balloons are up and the banner says NOW OPEN. The doors open at " + GameClock.OpenHour.ToString("00") + ":00"
                    + (OpenedDay > Clock.Day ? " tomorrow" : "") + ". Expect a few curious neighbours.");
        }

        void Start()
        {
            Avatar.SitDown();
        }

        void SpawnAvatar()
        {
            var go = new GameObject("Manager (You)");
            Avatar = go.AddComponent<ManagerAvatar>();
            Avatar.DisplayName = "You";
            Avatar.Dress(Outfit.Manager(), Hold.None);
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

        /// <summary>Par that covers about two days of this store's demand, given what it currently sells.</summary>
        public int SuggestedPar(Product p)
        {
            float carried = 0f;
            for (int i = 0; i < Products.Count; i++) if (Products[i].Carried || Products[i] == p) carried += Products[i].Def.Demand;
            float share = 0.8f * p.Def.Demand / Mathf.Max(0.01f, carried) + 0.2f * p.Def.Demand / Catalog.TotalDemand;
            float daily = ExpectedCustomersPerDay * 6.5f * 1.3f * share;
            int cs = p.Def.CaseSize;
            // Enough to last until the delivery after next (direct suppliers don't come every day),
            // counting busy weekends from the day it arrives.
            int days = Mathf.CeilToInt(p.Def.Supplier != null ? Mathf.Max(ParDays, p.Def.Supplier.MaxGap + 1f) : ParDays);
            int from = p.Def.Supplier != null ? NextDelivery(p.Def.Supplier) : Clock.Day + 1;
            float weight = 0f;
            for (int k = 0; k < days; k++) weight += DayFactor[(from - 1 + k) % 7];
            int par = Mathf.Max(cs * 2, Mathf.CeilToInt(daily * weight / cs) * cs);
            // Things that keep for a month or more: enough to keep the shelves at least half full.
            if (p.Life < 0 || p.Life >= 30) par = Mathf.Max(par, Mathf.CeilToInt(p.ShelfCap * 0.5f / cs) * cs);
            return par;
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
            Staff.Tick();

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
            if (!IsOpenNow) return;
            if (Clock.Hour == GameClock.CloseHour - 1 && Clock.Minute % 60f > 40f) return;
            float perSecond = HourWeight[Clock.Hour] * BaseTraffic * TrafficFactor * DayFactor[(Clock.Day - 1) % 7] * OpeningBoost;
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
                var t = Economy.Today;
                if (t.WrittenOff > 0)
                    Message("Scanned out " + t.WrittenOff + " expired items today (" + Economy.Money(t.WasteValue) + " at cost)"
                        + (t.ReturnCredit > 0f ? ", " + Economy.Money(t.ReturnCredit) + " goes back to suppliers for credit." : "."));
                if (t.ExpiredFound > 0) Message("Customers picked up expired products " + t.ExpiredFound + " times today.");
            }

            var am = Staff.Assistant;
            if (am != null && am.OnShift && Delegation.Ordering)
            {
                int orderHour = (StaffManager.ShiftEnd(am.Shift) + 23) % 24;
                if (h == orderHour)
                {
                    float err = Mathf.Clamp((10f - am.Leadership) * 0.04f, 0f, 0.4f);
                    int lines = AutoOrder(err);
                    Message(lines > 0 ? am.DisplayName + " ordered " + lines + " products for the next deliveries."
                        : am.DisplayName + " didn't need to order anything.");
                }
            }

            if (am != null && am.OnShift && Delegation.Bills) AssistantPaysBills(am);

            // Department managers order their own department an hour before their shift ends.
            for (int i = 0; i < Staff.All.Count; i++)
            {
                var e = Staff.All[i];
                if (e == null || !e.OnShift || e.Rank != Rank.DeptManager || e.Department == null || !e.OrdersForDepartment) continue;
                if (h != (StaffManager.ShiftEnd(e.Shift) + 23) % 24) continue;
                float derr = Mathf.Clamp((10f - e.Leadership) * 0.03f, 0f, 0.3f);
                int n = AutoOrder(derr, e.Department, false);
                if (n > 0) Message(e.DisplayName + " (" + e.Department.Name + " manager) ordered " + n + " products.");
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
                p.ExpiredLastNight = w;
            }

            var done = Economy.CloseDay(Clock.Day);
            if (done.Customers > 0)
            {
                // First impressions count double.
                float weight = Opened && done.Day == OpenedDay ? 0.4f : 0.2f;
                Reputation = Mathf.Clamp(Reputation + (Mathf.Clamp(done.AvgSatisfaction, 0f, 100f) - Reputation) * weight, 0f, 100f);
            }
            if (Opened && done.Day == OpenedDay) LocalPaper(done);
            if (Opened && Clock.Day == OpenedDay + OpeningDays) TakeDownOpeningDecor();
            Message("Day " + done.Day + " done: sales " + Economy.Money(done.Sales) + ", profit " + Economy.Money(done.Profit)
                + ", " + done.Customers + " customers, satisfaction " + done.AvgSatisfaction.ToString("0") + ".");

            for (int i = 0; i < Products.Count; i++) Products[i].ResetDaily();
            for (int i = 0; i < Categories.Count; i++) Categories[i].SalesToday = 0f;
            if (Opened) Economy.PayRent(Rent);
            BillsNightly();
            Staff.RefreshApplicants(false);
            if (Economy.Cash < 0f) Message("Warning: the store is in the red.");
        }

        // ------------------------------------------------------------------ licences and levels

        public bool Owns(LicenceDef l) { return l == null || owned.Contains(l); }
        public bool IsLicensed(ProductDef p) { return Owns(p.Licence); }
        public int XpThisLevel { get { return Catalog.LevelXp[Level - 1]; } }
        public int XpNextLevel { get { return Level < Catalog.MaxLevel ? Catalog.LevelXp[Level] : XP; } }

        /// <summary>Why a licence can't be bought right now, or null if it can.</summary>
        public string LicenceLock(LicenceDef l)
        {
            if (Owns(l)) return "Owned";
            if (Level < l.Level) return "Needs level " + l.Level;
            if (l.Requires != null && !Owns(l.Requires)) return "Needs " + l.Requires.Name;
            if (Economy.Cash < l.Cost) return "Costs " + Economy.Money(l.Cost);
            return null;
        }

        public bool BuyLicence(LicenceDef l)
        {
            if (LicenceLock(l) != null) return false;
            Economy.PayConstruction(l.Cost);
            owned.Add(l);
            string what = "";
            for (int i = 0; i < l.Products.Count; i++) what += (i > 0 ? ", " : "") + l.Products[i].Name;
            Message("Licence bought: " + l.Name + ". You can now sell " + what + ".");
            return true;
        }

        public string FixtureLock(FixtureDef f)
        {
            var l = Catalog.LicenceForFixture(f.Id);
            return Owns(l) ? null : "Needs the " + l.Name + " licence";
        }

        public string RoleLock(Role r)
        {
            int lvl = Catalog.RoleLevel(r);
            if (Level < lvl) return "Needs level " + lvl;
            var l = Catalog.LicenceForRole(r);
            if (l != null && !Owns(l)) return "Needs the " + l.Name + " licence";
            return null;
        }

        public void AddXP(int amount)
        {
            if (amount <= 0) return;
            XP += amount;
            while (Level < Catalog.MaxLevel && XP >= Catalog.LevelXp[Level])
            {
                Level++;
                var news = new List<string>();
                for (int i = 0; i < Catalog.Licences.Count; i++)
                    if (Catalog.Licences[i].Level == Level) news.Add(Catalog.Licences[i].Name);
                for (int r = 0; r < 6; r++)
                    if (Catalog.RoleLevel((Role)r) == Level) news.Add("hiring " + Catalog.RoleName((Role)r) + "s");
                if (ExpansionLevel == Level) news.Add("store expansion");
                string text = "Store level " + Level + "!" + (news.Count > 0 ? " Unlocked: " + string.Join(", ", news.ToArray()) + "." : "");
                if (Hud.I != null) Hud.Toast(text); else Message(text);
            }
        }

        /// <summary>What a customer wants: mostly things the store sells, sometimes anything at all.</summary>
        public ProductDef PickWantedProduct()
        {
            if (Random.value < 0.8f)
            {
                float total = 0f;
                for (int i = 0; i < Products.Count; i++) if (Products[i].Carried) total += Products[i].Def.Demand;
                if (total > 0f)
                {
                    float r = Random.value * total;
                    for (int i = 0; i < Products.Count; i++)
                    {
                        if (!Products[i].Carried) continue;
                        r -= Products[i].Def.Demand;
                        if (r <= 0f) return Products[i].Def;
                    }
                }
            }
            return Catalog.RandomProduct();
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
            string locked = FixtureLock(def);
            if (locked != null) return locked + ".";
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
            if (def != null && !IsLicensed(def)) return false;
            if (f.Product != null)
            {
                var old = f.Product;
                if (!old.Baked) f.Stock.MoveTo(old.Back, f.Stock.Total);
                f.Stock.Clear();
                old.Fixtures.Remove(f);
            }
            f.Product = def != null ? ProductOf(def) : null;
            if (f.Product != null)
            {
                bool first = f.Product.Fixtures.Count == 0;
                f.Product.Fixtures.Add(f);
                if (first && !f.Product.Baked) f.Product.Par = SuggestedPar(f.Product);
            }
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
            get { return SlotCount < Layout.MaxSlots && !ExpansionPending && Level >= ExpansionLevel && Economy.Cash >= ExpansionCost; }
        }

        public void Expand()
        {
            if (!CanExpand) return;
            Economy.PayConstruction(ExpansionCost);
            ExpansionPending = true;
            Message("Expansion ordered: 12 m more shop floor and a bigger back room. Ready at 06:00.");
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
