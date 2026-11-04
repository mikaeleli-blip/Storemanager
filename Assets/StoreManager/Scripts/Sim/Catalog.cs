using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public enum FixtureKind { Shelf, Counter, Oven, Checkout }
    public enum Role { Cashier, Stocker, Cleaner, Baker, DeliClerk, AssistantManager }
    public enum Shift { Morning, Evening, Night }
    public enum Specialty { None, Baking, Butchery }

    public class ProductDef
    {
        public string Name;
        public float Cost;          // wholesale (or ingredients, for baked goods)
        public float BasePrice;
        public int ShelfLifeDays;   // 0 = does not expire
        public float Demand;        // relative weight on shopping lists
        public int CaseSize;
        public CategoryDef Category;
        public LicenceDef Licence;
        public SupplierDef Supplier;   // null = baked in the store

        public ProductDef(string name, float cost, float price, int life, float demand, int caseSize)
        {
            Name = name; Cost = cost; BasePrice = price; ShelfLifeDays = life; Demand = demand; CaseSize = caseSize;
        }
    }

    /// <summary>A product category: what it sells, which fixture it goes on, and who it needs.</summary>
    public enum SupplierKind { Warehouse, Direct }

    /// <summary>
    /// Where stock comes from: the central warehouse (most things, every day) or a direct supplier
    /// (soda, dairy, bread, meat) with its own delivery days, minimum order, payment terms and returns policy.
    /// </summary>
    public class SupplierDef
    {
        public string Id, Name, Description;
        public SupplierKind Kind;
        public readonly bool[] Days = new bool[7];   // Mon..Sun
        public int TermsDays;                         // invoice due this many days after delivery
        public float ReturnCredit;                    // share of cost credited for expired stock sent back
        public float MinOrder;                        // per truck order
        public float CreditBase, CreditPerLevel;      // most you can owe them: base + per store level

        static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public string DaysText
        {
            get
            {
                int n = 0;
                for (int i = 0; i < 7; i++) if (Days[i]) n++;
                if (n == 7) return "every day";
                if (n == 6 && !Days[6]) return "Mon-Sat";
                var list = new List<string>();
                for (int i = 0; i < 7; i++) if (Days[i]) list.Add(DayNames[i]);
                return string.Join(", ", list.ToArray());
            }
        }

        /// <summary>Longest wait between two deliveries, in days.</summary>
        public int MaxGap
        {
            get
            {
                int best = 1;
                for (int i = 0; i < 7; i++)
                {
                    if (!Days[i]) continue;
                    int gap = 1;
                    while (gap < 7 && !Days[(i + gap) % 7]) gap++;
                    best = Mathf.Max(best, gap);
                }
                return best;
            }
        }
    }

    public class CategoryDef
    {
        public string Id;
        public string Name;
        public Color Color;
        public string Fixture;          // FixtureDef id its products go on
        public bool Baked;              // made on site by bakers (needs an oven), never ordered
        public bool NeedsSpecialist;
        public Role SpecialistRole;
        public readonly List<ProductDef> Products = new List<ProductDef>();

        public float TotalDemand
        {
            get { float t = 0f; for (int i = 0; i < Products.Count; i++) t += Products[i].Demand; return t; }
        }
    }

    /// <summary>
    /// What a licence unlocks. Department licences open a fixture type (and sometimes a staff role) with a
    /// first few products; later licences in the same department add more products.
    /// </summary>
    public class LicenceDef
    {
        public string Id, Name;
        public int Level;
        public float Cost;
        public bool Department;
        public LicenceDef Requires;
        public readonly List<ProductDef> Products = new List<ProductDef>();
        public readonly List<string> Fixtures = new List<string>();
        public readonly List<Role> Roles = new List<Role>();
    }

    /// <summary>
    /// Something the player can place. Footprint is W x D tiles in local space; local tiles outside the
    /// footprint (access, work, service, cashier) are where people stand. See Placement for rotation.
    /// </summary>
    public class FixtureDef
    {
        public string Id, Name, Model, Description;
        public FixtureKind Kind;
        public int W, D;
        public float Cost;
        public int Capacity;   // counters; shelves without a product. Shelves: see Fixture.Capacity
        public float[] Levels = new float[0];   // shelf heights for stock rows (both sides)
        /// <summary>Stock rows on a shelf: every level on both sides of every segment.</summary>
        public int ShelfRowCount { get { return Segments.Length * 2 * Levels.Length; } }
        public bool Tilted;                      // produce crates
        public Vector2[] Segments = new Vector2[0];   // model centres (local)
        public Vector2Int[] Access = new Vector2Int[0];
        public bool HasWork, HasService, HasCashier;
        public Vector2Int Work, Service, Cashier;
        public Vector2Int QueueDir;               // local direction the queue grows from the service tile
    }

    /// <summary>All categories, products and fixtures. The store type is data: change it here.</summary>
    public static class Catalog
    {
        public static readonly List<CategoryDef> Categories = new List<CategoryDef>();
        public static readonly List<ProductDef> Products = new List<ProductDef>();
        public static readonly List<FixtureDef> Fixtures = new List<FixtureDef>();
        public static readonly List<LicenceDef> Licences = new List<LicenceDef>();
        public static readonly List<SupplierDef> Suppliers = new List<SupplierDef>();
        public static float TotalDemand;

        /// <summary>Total XP needed to reach level i + 1 (index 0 = level 1).</summary>
        public static readonly int[] LevelXp = { 0, 900, 2400, 4600, 7600, 11600, 16800, 24000, 34000, 47000, 63000, 83000 };
        public static int MaxLevel { get { return LevelXp.Length; } }

        /// <summary>Store level needed for each expansion (first, second, ...).</summary>
        public static readonly int[] ExpansionLevels = { 3, 5, 7, 9, 11 };

        public static int RoleLevel(Role r)
        {
            switch (r)
            {
                case Role.Cleaner: return 2;
                case Role.AssistantManager: return 3;
                default: return 1;
            }
        }

        static Catalog()
        {
            // ---- fixtures
            var shelfSegs = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1.5f) };
            var shelfAccess = new[] { new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) };
            float[] four = { 0.12f, 0.52f, 0.92f, 1.32f };

            AddFixture(new FixtureDef
            {
                Id = "gondola", Name = "Shelf", Model = "Gondola", Kind = FixtureKind.Shelf, W = 1, D = 2,
                Cost = 800f, Capacity = 64, Levels = four, Segments = shelfSegs, Access = shelfAccess,
                Description = "2 m double-sided shelf for groceries and household goods.",
            });
            AddFixture(new FixtureDef
            {
                Id = "fridge", Name = "Fridge", Model = "Fridge", Kind = FixtureKind.Shelf, W = 1, D = 2,
                Cost = 2400f, Capacity = 48, Levels = four, Segments = shelfSegs, Access = shelfAccess,
                Description = "2 m double-sided glass-door fridge for dairy.",
            });
            AddFixture(new FixtureDef
            {
                Id = "freezer", Name = "Chest freezer", Model = "Freezer", Kind = FixtureKind.Shelf, W = 1, D = 2,
                Cost = 2600f, Capacity = 40, Levels = new[] { 0.45f }, Segments = shelfSegs, Access = shelfAccess,
                Description = "2 m island chest freezer with sliding glass lids, for frozen food.",
            });
            AddFixture(new FixtureDef
            {
                Id = "produce", Name = "Produce table", Model = "ProduceTable", Kind = FixtureKind.Shelf, W = 1, D = 2,
                Cost = 900f, Capacity = 50, Levels = new[] { 0.8f }, Tilted = true, Segments = shelfSegs, Access = shelfAccess,
                Description = "2 m table of crates for fruit and vegetables.",
            });
            AddFixture(new FixtureDef
            {
                Id = "bakery_shelf", Name = "Bakery shelf", Model = "BakeryShelf", Kind = FixtureKind.Shelf, W = 1, D = 2,
                Cost = 700f, Capacity = 36, Levels = new[] { 0.12f, 0.62f, 1.12f }, Segments = shelfSegs, Access = shelfAccess,
                Description = "Bread shelf. Bakers fill it from an oven.",
            });
            AddFixture(new FixtureDef
            {
                Id = "deli_counter", Name = "Deli counter", Model = "DeliCounter", Kind = FixtureKind.Counter, W = 3, D = 1,
                Cost = 4500f, Capacity = 40,
                Segments = new[] { new Vector2(0.5f, 0.5f), new Vector2(1.5f, 0.5f), new Vector2(2.5f, 0.5f) },
                HasService = true, Service = new Vector2Int(1, -1), QueueDir = new Vector2Int(0, -1),
                HasWork = true, Work = new Vector2Int(1, 1),
                Description = "Serve-over counter. A deli clerk stands behind it and serves customers.",
            });
            AddFixture(new FixtureDef
            {
                Id = "oven", Name = "Bakery oven", Model = "Oven", Kind = FixtureKind.Oven, W = 2, D = 3,
                Cost = 6000f, Segments = new[] { new Vector2(1f, 1.5f) },
                HasWork = true, Work = new Vector2Int(-1, 1),
                Description = "Bakers bake here for the bakery shelves.",
            });
            AddFixture(new FixtureDef
            {
                Id = "checkout", Name = "Checkout", Model = "Checkout", Kind = FixtureKind.Checkout, W = 1, D = 2,
                Cost = 1500f, Segments = new[] { new Vector2(0.5f, 1f) },
                HasCashier = true, Cashier = new Vector2Int(-1, 0),
                HasService = true, Service = new Vector2Int(1, 0), QueueDir = new Vector2Int(0, 1),
                Description = "Till with a belt. The cashier stands on one side, the queue forms on the other.",
            });

            // ---- categories and products
            var grocery = Cat("grocery", "Grocery", "#d9a441", "gondola");
            P(grocery, "Pasta", 1.1f, 2.0f, 0, 1.0f, 24);
            P(grocery, "Rice", 1.6f, 2.9f, 0, 0.8f, 12);
            P(grocery, "Canned Soup", 0.9f, 1.8f, 0, 0.9f, 24);
            P(grocery, "Cereal", 2.2f, 4.2f, 0, 1.0f, 12);
            P(grocery, "Coffee", 4.5f, 8.5f, 0, 0.8f, 12);
            P(grocery, "Snacks", 1.4f, 2.8f, 0, 1.2f, 24);
            P(grocery, "Bubbla Cola", 0.8f, 1.7f, 0, 0.75f, 24);
            P(grocery, "Zing Cola", 0.75f, 1.6f, 0, 0.65f, 24);
            P(grocery, "Sliced Bread", 1.0f, 2.2f, 5, 0.9f, 10);

            var produce = Cat("produce", "Produce", "#6fbf4a", "produce");
            P(produce, "Apples", 0.6f, 1.3f, 10, 1.1f, 40);
            P(produce, "Bananas", 0.3f, 0.7f, 6, 1.2f, 40);
            P(produce, "Potatoes", 0.5f, 1.1f, 20, 0.9f, 30);
            P(produce, "Tomatoes", 0.8f, 1.8f, 5, 0.9f, 30);
            P(produce, "Lettuce", 0.7f, 1.6f, 4, 0.7f, 24);

            var dairy = Cat("dairy", "Dairy", "#7fb3e6", "fridge");
            P(dairy, "Milk", 0.7f, 1.4f, 7, 1.4f, 24);
            P(dairy, "Eggs", 1.5f, 3.0f, 21, 1.0f, 12);
            P(dairy, "Cheese", 2.5f, 4.8f, 30, 0.9f, 12);
            P(dairy, "Yogurt", 0.5f, 1.1f, 14, 0.9f, 24);

            var frozen = Cat("frozen", "Frozen", "#9fd8f0", "freezer");
            P(frozen, "Frozen Pizza", 2.2f, 4.5f, 0, 0.8f, 12);
            P(frozen, "Ice Cream", 2.5f, 5.0f, 0, 0.6f, 12);

            var household = Cat("household", "Household", "#b48ad6", "gondola");
            P(household, "Detergent", 4.0f, 7.5f, 0, 0.6f, 6);
            P(household, "Toilet Paper", 3.0f, 5.5f, 0, 0.8f, 8);
            P(household, "Dish Soap", 1.2f, 2.4f, 0, 0.5f, 12);
            P(household, "Batteries", 3.5f, 7.0f, 0, 0.4f, 12);

            var bakery = Cat("bakery", "Bakery", "#e3c08d", "bakery_shelf");
            bakery.Baked = true;
            bakery.NeedsSpecialist = true;
            bakery.SpecialistRole = Role.Baker;
            P(bakery, "Fresh Bread", 0.6f, 2.8f, 2, 1.4f, 12);
            P(bakery, "Croissants", 0.4f, 1.6f, 1, 1.0f, 12);
            P(bakery, "Cakes", 3.0f, 9.0f, 3, 0.5f, 6);

            var deli = Cat("deli", "Deli & Meat", "#d86a5c", "deli_counter");
            deli.NeedsSpecialist = true;
            deli.SpecialistRole = Role.DeliClerk;
            P(deli, "Sliced Ham", 1.8f, 3.8f, 5, 0.6f, 20);
            P(deli, "Chicken", 3.0f, 6.0f, 4, 0.7f, 12);
            P(deli, "Salads", 1.2f, 3.2f, 3, 0.4f, 12);
            P(deli, "Steak", 6.0f, 12.0f, 4, 0.3f, 10);

            TotalDemand = 0f;
            for (int i = 0; i < Products.Count; i++) TotalDemand += Products[i].Demand;

            // ---- suppliers (days are Mon..Sun)
            var warehouse = Sup("warehouse", "Central Warehouse", SupplierKind.Warehouse, "1111111", 10, 0f, 0f, 6000f, 2000f,
                "Dry goods, produce, frozen and household. Delivers every day. No credit for expired stock.");
            Sup("bubbla", "Bubbla Beverages", SupplierKind.Direct, "1010100", 14, 1f, 100f, 800f, 400f,
                "Bubbla Cola. Their own trucks on Mon, Wed and Fri. Takes back expired stock for full credit.");
            Sup("zing", "Zing Drinks", SupplierKind.Direct, "0101010", 14, 1f, 100f, 800f, 400f,
                "Zing Cola. Delivers Tue, Thu and Sat. Takes back expired stock for full credit.");
            Sup("dairy", "Valley Dairy Co-op", SupplierKind.Direct, "1111110", 7, 0.75f, 60f, 1000f, 500f,
                "Milk, eggs, yogurt and cheese, Mon-Sat. Credits 75% of expired stock sent back.");
            Sup("bakery", "Millhouse Bakery", SupplierKind.Direct, "1111111", 7, 1f, 40f, 400f, 200f,
                "Packaged sliced bread, every morning. Takes back old bread for full credit.");
            Sup("meats", "Fjord Meats", SupplierKind.Direct, "1001000", 7, 0.5f, 150f, 2000f, 1000f,
                "Deli meats and salads on Mon and Thu. Credits 50% of expired stock sent back.");
            for (int i = 0; i < Products.Count; i++) if (!Products[i].Category.Baked) Products[i].Supplier = warehouse;
            Supply("bubbla", "Bubbla Cola");
            Supply("zing", "Zing Cola");
            Supply("dairy", "Milk", "Eggs", "Yogurt", "Cheese");
            Supply("bakery", "Sliced Bread");
            Supply("meats", "Sliced Ham", "Chicken", "Salads", "Steak");

            // ---- licences (level, cost, needs, unlocks)
            var basics = Lic("basics", "Basic Groceries", 1, 0f, null, true, new[] { "Pasta", "Canned Soup", "Bubbla Cola", "Zing Cola", "Snacks" },
                new[] { "gondola", "checkout" }, new[] { Role.Cashier, Role.Stocker });
            Lic("pantry", "Pantry Staples", 2, 1200f, basics, false, new[] { "Rice", "Cereal", "Sliced Bread" }, null, null);
            var produceL = Lic("produce", "Produce Department", 2, 2500f, null, true, new[] { "Apples", "Bananas", "Potatoes" },
                new[] { "produce" }, null);
            Lic("coffee", "Coffee", 3, 1500f, basics, false, new[] { "Coffee" }, null, null);
            var dairyL = Lic("dairy", "Dairy Department", 3, 4000f, null, true, new[] { "Milk", "Eggs", "Yogurt" },
                new[] { "fridge" }, null);
            Lic("salad_veg", "Salad Vegetables", 4, 1800f, produceL, false, new[] { "Tomatoes", "Lettuce" }, null, null);
            var householdL = Lic("household", "Household Department", 4, 2500f, null, true, new[] { "Toilet Paper", "Dish Soap" },
                null, null);
            Lic("frozen", "Frozen Food", 5, 3500f, dairyL, true, new[] { "Frozen Pizza", "Ice Cream" }, new[] { "freezer" }, null);
            Lic("cheese", "Cheese", 5, 1500f, dairyL, false, new[] { "Cheese" }, null, null);
            var bakeryL = Lic("bakery", "Bakery Department", 6, 8000f, null, true, new[] { "Fresh Bread", "Croissants" },
                new[] { "oven", "bakery_shelf" }, new[] { Role.Baker });
            Lic("cleaning", "Cleaning & Batteries", 6, 2000f, householdL, false, new[] { "Detergent", "Batteries" }, null, null);
            var deliL = Lic("deli", "Deli Department", 7, 12000f, null, true, new[] { "Sliced Ham", "Chicken" },
                new[] { "deli_counter" }, new[] { Role.DeliClerk });
            Lic("cakes", "Cakes", 8, 3000f, bakeryL, false, new[] { "Cakes" }, null, null);
            Lic("deli_salads", "Deli Salads", 9, 3000f, deliL, false, new[] { "Salads" }, null, null);
            Lic("butcher", "Butcher's Cuts", 10, 6000f, deliL, false, new[] { "Steak" }, null, null);
        }

        static SupplierDef Sup(string id, string name, SupplierKind kind, string days, int terms, float credit, float min,
            float creditBase, float creditPerLevel, string desc)
        {
            var s = new SupplierDef
            {
                Id = id, Name = name, Kind = kind, TermsDays = terms, ReturnCredit = credit, MinOrder = min,
                CreditBase = creditBase, CreditPerLevel = creditPerLevel, Description = desc,
            };
            for (int i = 0; i < 7; i++) s.Days[i] = days[i] == '1';
            Suppliers.Add(s);
            return s;
        }

        static void Supply(string supplier, params string[] products)
        {
            var s = SupplierById(supplier);
            for (int i = 0; i < products.Length; i++) ProductByName(products[i]).Supplier = s;
        }

        public static SupplierDef SupplierById(string id)
        {
            for (int i = 0; i < Suppliers.Count; i++) if (Suppliers[i].Id == id) return Suppliers[i];
            return null;
        }

        static LicenceDef Lic(string id, string name, int level, float cost, LicenceDef requires, bool dept,
            string[] products, string[] fixtures, Role[] roles)
        {
            var l = new LicenceDef { Id = id, Name = name, Level = level, Cost = cost, Requires = requires, Department = dept };
            for (int i = 0; i < products.Length; i++)
            {
                var p = ProductByName(products[i]);
                l.Products.Add(p);
                p.Licence = l;
            }
            if (fixtures != null) l.Fixtures.AddRange(fixtures);
            if (roles != null) l.Roles.AddRange(roles);
            Licences.Add(l);
            return l;
        }

        public static LicenceDef LicenceById(string id)
        {
            for (int i = 0; i < Licences.Count; i++) if (Licences[i].Id == id) return Licences[i];
            return null;
        }

        /// <summary>The licence that unlocks a fixture type, or null if it's always available.</summary>
        public static LicenceDef LicenceForFixture(string fixtureId)
        {
            for (int i = 0; i < Licences.Count; i++) if (Licences[i].Fixtures.Contains(fixtureId)) return Licences[i];
            return null;
        }

        public static LicenceDef LicenceForRole(Role r)
        {
            for (int i = 0; i < Licences.Count; i++)
                if (Licences[i].Roles.Contains(r) && Licences[i].Cost > 0f) return Licences[i];
            return null;
        }

        static void AddFixture(FixtureDef f) { Fixtures.Add(f); }

        static CategoryDef Cat(string id, string name, string hex, string fixture)
        {
            var c = new CategoryDef { Id = id, Name = name, Color = Mats.Hex(hex), Fixture = fixture };
            Categories.Add(c);
            return c;
        }

        static void P(CategoryDef c, string name, float cost, float price, int life, float demand, int caseSize)
        {
            var p = new ProductDef(name, cost, price, life, demand, caseSize) { Category = c };
            c.Products.Add(p);
            Products.Add(p);
        }

        public static FixtureDef FixtureById(string id)
        {
            for (int i = 0; i < Fixtures.Count; i++) if (Fixtures[i].Id == id) return Fixtures[i];
            return null;
        }

        public static CategoryDef CategoryById(string id)
        {
            for (int i = 0; i < Categories.Count; i++) if (Categories[i].Id == id) return Categories[i];
            return null;
        }

        public static ProductDef ProductByName(string name)
        {
            for (int i = 0; i < Products.Count; i++) if (Products[i].Name == name) return Products[i];
            return null;
        }

        /// <summary>Products that can go on this fixture type.</summary>
        public static List<ProductDef> ProductsFor(FixtureDef f)
        {
            var list = new List<ProductDef>();
            for (int i = 0; i < Products.Count; i++)
                if (Products[i].Category.Fixture == f.Id) list.Add(Products[i]);
            return list;
        }

        public static ProductDef RandomProduct()
        {
            float r = Random.value * TotalDemand;
            for (int i = 0; i < Products.Count; i++)
            {
                r -= Products[i].Demand;
                if (r <= 0f) return Products[i];
            }
            return Products[Products.Count - 1];
        }

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Role.DeliClerk: return "Deli Clerk";
                case Role.AssistantManager: return "Asst. Manager";
                default: return r.ToString();
            }
        }
    }
}
