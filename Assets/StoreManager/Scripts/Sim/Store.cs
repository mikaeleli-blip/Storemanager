using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

        public readonly Department[] Slots = new Department[Layout.MaxSlots];
        public int SlotCount = Layout.StartSlots;
        public bool ExpansionPending;
        public float Reputation = 50f;

        public readonly List<CheckoutLane> Lanes = new List<CheckoutLane>();
        public readonly List<Customer> Customers = new List<Customer>();
        public readonly List<Customer> Complaints = new List<Customer>();
        public readonly List<string> Log = new List<string>();

        float spawnAcc, visualsTimer, truckTimer;

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

        public const float BaseTraffic = 0.28f;
        public const float ParDays = 2f;

        public int Expansions { get { return SlotCount - Layout.StartSlots; } }
        public int BackCapacity { get { return 9000 + 2000 * Expansions; } }
        public float Rent { get { return 700f + 300f * Expansions; } }
        public float ExpansionCost { get { return 15000f * (Expansions + 1); } }
        public Vector2Int StaffEntrance { get { return new Vector2Int(Grid.W - 4, Layout.DockRow); } }
        public Vector2Int DeskSeat { get { return new Vector2Int(3, 30); } }

        public float TrafficFactor
        {
            get { return (0.4f + Reputation / 100f * 1.2f) * (0.7f + 0.1f * ActiveDepartmentCount); }
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
            Dirt = new DirtManager(transform);
            Staff = new StaffManager(this);
            for (int i = 0; i < Layout.LaneCount(SlotCount); i++) Lanes.Add(new CheckoutLane(i));
            Builder = new StoreBuilder(this);

            Slots[0] = new Department(Catalog.Get("grocery"), 0);
            Slots[1] = new Department(Catalog.Get("produce"), 1);
            Slots[2] = new Department(Catalog.Get("dairy"), 2);
            Builder.BuildAll();
            for (int i = 0; i < SlotCount; i++) if (Slots[i] != null) FillStartingStock(Slots[i]);

            Staff.RefreshApplicants(true);
            Staff.HireStartingStaff();
            SpawnAvatar();
            SetupCameraAndLight();
            gameObject.AddComponent<Hud>();

            Clock.HourChanged += OnHour;
            Clock.NewDay += OnNewDay;
            Economy.Today.Day = Clock.Day;
            Economy.PayRent(Rent);
            Message("Welcome, manager. Your office is at the back left. Walk in and press Tab to sit at your desk.");
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
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            Cam = cam.GetComponent<CameraRig>();
            if (Cam == null) Cam = cam.gameObject.AddComponent<CameraRig>();
            Cam.FollowTarget = Avatar.transform;
            Cam.Focus = Avatar.transform.position;
            UpdateCameraBounds();

            Light sun = null;
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.48f, 0.52f);
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

        void FillStartingStock(Department d)
        {
            for (int i = 0; i < d.Products.Count; i++)
            {
                var p = d.Products[i];
                p.Par = DefaultPar(p.Def);
                p.Shelf.Add(p.ShelfCap, p.Life);
                if (!d.IsBakery) p.Back.Add(Mathf.Max(0, p.Par - p.ShelfCap), p.Life);
            }
            d.UpdateVisuals();
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
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] != null && Slots[i].Active) Slots[i].UpdateVisuals();
            for (int i = 0; i < Lanes.Count; i++)
            {
                var lane = Lanes[i];
                if (lane.Light == null) continue;
                Color c = lane.Accepting && !lane.Closing ? new Color(0.2f, 0.85f, 0.3f)
                    : lane.Staffed ? new Color(0.95f, 0.8f, 0.2f)
                    : new Color(0.85f, 0.15f, 0.12f);
                lane.Light.sharedMaterial = Mats.Get(c);
            }
        }

        void OnHour(int h)
        {
            if (h == 6)
            {
                CompleteConstruction();
                Deliver();
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
            for (int s = 0; s < SlotCount; s++)
            {
                var d = Slots[s];
                if (d == null) continue;
                for (int i = 0; i < d.Products.Count; i++)
                {
                    var p = d.Products[i];
                    int w = p.Shelf.Age() + p.Back.Age();
                    p.WastedLastNight = w;
                    today.WasteValue += w * p.Def.Cost;
                }
            }

            var done = Economy.CloseDay(Clock.Day);
            if (done.Customers > 0)
                Reputation = Mathf.Clamp(Reputation + (Mathf.Clamp(done.AvgSatisfaction, 0f, 100f) - Reputation) * 0.2f, 0f, 100f);
            Message("Day " + done.Day + " done: sales " + Economy.Money(done.Sales) + ", profit " + Economy.Money(done.Profit)
                + ", " + done.Customers + " customers, satisfaction " + done.AvgSatisfaction.ToString("0") + ".");

            for (int s = 0; s < SlotCount; s++) if (Slots[s] != null) Slots[s].ResetDaily();
            Economy.PayRent(Rent);
            Staff.RefreshApplicants(false);
            if (Economy.Cash < 0f) Message("Warning: the store is in the red.");
        }

        // ------------------------------------------------------------------ departments, expansion

        public Department FindDepartment(DepartmentDef def)
        {
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] != null && Slots[i].Def == def) return Slots[i];
            return null;
        }

        public Department FirstActive(DeptKind kind)
        {
            for (int i = 0; i < SlotCount; i++)
                if (Slots[i] != null && Slots[i].Active && Slots[i].Def.Kind == kind) return Slots[i];
            return null;
        }

        public int ActiveDepartmentCount
        {
            get { int n = 0; for (int i = 0; i < SlotCount; i++) if (Slots[i] != null && Slots[i].Active) n++; return n; }
        }

        public bool CanBuild(int slot, DepartmentDef def)
        {
            return slot >= 0 && slot < SlotCount && Slots[slot] == null && FindDepartment(def) == null
                && Economy.Cash >= def.BuildCost;
        }

        public void BuildDepartment(int slot, DepartmentDef def)
        {
            if (!CanBuild(slot, def)) return;
            Economy.PayConstruction(def.BuildCost);
            var d = new Department(def, slot) { UnderConstruction = true };
            Slots[slot] = d;
            for (int i = 0; i < d.Products.Count; i++)
            {
                var p = d.Products[i];
                p.Par = DefaultPar(p.Def);
                if (!d.IsBakery) p.OnOrder = p.Par;
            }
            Builder.BuildDepartment(d);
            string extra = d.IsBakery ? " You'll need a baker." : d.IsCounter ? " You'll need a deli clerk." : "";
            Message(def.Name + " is being built in slot " + (slot + 1) + ". Ready at 06:00, opening stock comes with the delivery." + extra);
        }

        public void Demolish(int slot)
        {
            var d = Slots[slot];
            if (d == null) return;
            d.Removed = true;
            Slots[slot] = null;
            d.CounterQueue.Clear();
            if (d.Root != null) Destroy(d.Root.gameObject);
            Builder.ClearSlot(slot);
            for (int i = 0; i < Staff.All.Count; i++)
            {
                var e = Staff.All[i];
                if (e != null && e.AssignedDept == d) { e.AssignedDept = null; e.Restart(); }
            }
            RepathAll();
            Message(d.Name + " was removed. Its stock was thrown out.");
        }

        public bool CanExpand
        {
            get { return SlotCount < Layout.MaxSlots && !ExpansionPending && Economy.Cash >= ExpansionCost; }
        }

        public void Expand()
        {
            if (!CanExpand) return;
            Economy.PayConstruction(ExpansionCost);
            ExpansionPending = true;
            Message("Expansion ordered: one more department slot and checkout lane. Ready at 06:00.");
        }

        void CompleteConstruction()
        {
            bool changed = false;
            if (ExpansionPending)
            {
                ExpansionPending = false;
                SlotCount++;
                while (Lanes.Count < Layout.LaneCount(SlotCount)) Lanes.Add(new CheckoutLane(Lanes.Count));
                changed = true;
                Message("Expansion finished: slot " + SlotCount + " and checkout " + Lanes.Count + " are ready.");
            }
            for (int i = 0; i < SlotCount; i++)
            {
                var d = Slots[i];
                if (d != null && d.UnderConstruction)
                {
                    d.UnderConstruction = false;
                    changed = true;
                    Message(d.Name + " is finished and open.");
                }
            }
            if (changed)
            {
                Builder.BuildAll();
                UpdateCameraBounds();
                RepathAll();
            }
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
            get
            {
                int t = 0;
                for (int s = 0; s < SlotCount; s++)
                    if (Slots[s] != null) t += Slots[s].BackTotal;
                return t;
            }
        }

        public int OnOrderTotal
        {
            get
            {
                int t = 0;
                for (int s = 0; s < SlotCount; s++)
                    if (Slots[s] != null)
                        for (int i = 0; i < Slots[s].Products.Count; i++) t += Slots[s].Products[i].OnOrder;
                return t;
            }
        }

        public void Order(ProductStock p, int qty)
        {
            if (qty > 0 && !p.Dept.Removed && !p.Dept.IsBakery) p.OnOrder += qty;
        }

        public int OrderToPar(Department d, float error)
        {
            if (d == null || d.IsBakery || d.Removed) return 0;
            int lines = 0;
            for (int i = 0; i < d.Products.Count; i++)
            {
                var p = d.Products[i];
                int need = p.Par - (p.OnHand + p.OnOrder);
                if (need <= 0) continue;
                float q = need;
                if (error > 0f) q *= Random.Range(1f - error, 1f + error);
                int cases = Mathf.Max(1, Mathf.CeilToInt(q / p.Def.CaseSize));
                p.OnOrder += cases * p.Def.CaseSize;
                lines++;
            }
            return lines;
        }

        public int OrderAllToPar(float error)
        {
            int lines = 0;
            for (int s = 0; s < SlotCount; s++) lines += OrderToPar(Slots[s], error);
            return lines;
        }

        void Deliver()
        {
            int space = BackCapacity - BackStockTotal;
            float cost = 0f;
            int units = 0, refused = 0;
            for (int s = 0; s < SlotCount; s++)
            {
                var d = Slots[s];
                if (d == null) continue;
                for (int i = 0; i < d.Products.Count; i++)
                {
                    var p = d.Products[i];
                    if (p.OnOrder <= 0) continue;
                    int q = Mathf.Min(p.OnOrder, Mathf.Max(0, space));
                    p.Back.Add(q, p.Life);
                    space -= q;
                    units += q;
                    cost += q * p.Def.Cost;
                    refused += p.OnOrder - q;
                    p.OnOrder = 0;
                }
            }
            if (units > 0)
            {
                Economy.PayWholesale(cost);
                Message("Delivery arrived: " + units + " units for " + Economy.Money(cost) + "."
                    + (refused > 0 ? " " + refused + " units refused: the back room is full." : ""));
                if (Builder.Truck != null) Builder.Truck.SetActive(true);
                truckTimer = 60f;
            }
            else
            {
                Message("No delivery today: nothing was ordered.");
            }
        }

        // ------------------------------------------------------------------ customers, checkouts, complaints

        public Vector2Int RandomStreetTile()
        {
            return new Vector2Int(Random.Range(0, Grid.W), Random.Range(0, 2));
        }

        public Vector2Int CheckoutWaitTile()
        {
            int last = Lanes[Lanes.Count - 1].CounterX + 1;
            return new Vector2Int(Random.Range(Lanes[0].CounterX - 1, last + 1), Random.Range(10, 12));
        }

        public Vector2Int RandomFloorTile()
        {
            int tries = 0;
            while (tries++ < 10)
            {
                var d = Slots[Random.Range(0, SlotCount)];
                if (d != null && d.Active && d.FloorTiles.Count > 0)
                    return d.FloorTiles[Random.Range(0, d.FloorTiles.Count)];
            }
            return new Vector2Int(Random.Range(1, Grid.W - 1), Random.Range(10, 12));
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
                if (Lanes[i].Cashier == null) return Lanes[i];
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

        public bool CounterStaffed(Department d)
        {
            return Staff.AnyOnShift(Role.DeliClerk, d);
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
            for (int i = 0; i < SlotCount; i++)
            {
                var d = Slots[i];
                if (d == null) continue;
                d.CounterQueue.Remove(c);
                for (int k = 0; k < d.Stations.Count; k++) if (d.Stations[k].Customer == c) d.Stations[k].Customer = null;
            }
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
