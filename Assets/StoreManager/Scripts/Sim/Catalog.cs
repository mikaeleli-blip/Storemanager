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

        public ProductDef(string name, float cost, float price, int life, float demand, int caseSize)
        {
            Name = name; Cost = cost; BasePrice = price; ShelfLifeDays = life; Demand = demand; CaseSize = caseSize;
        }
    }

    /// <summary>A product category: what it sells, which fixture it goes on, and who it needs.</summary>
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
    /// Something the player can place. Footprint is W x D tiles in local space; local tiles outside the
    /// footprint (access, work, service, cashier) are where people stand. See Placement for rotation.
    /// </summary>
    public class FixtureDef
    {
        public string Id, Name, Model, Description;
        public FixtureKind Kind;
        public int W, D;
        public float Cost;
        public int Capacity;
        public float[] Levels = new float[0];   // shelf heights for stock rows (both sides)
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
        public static float TotalDemand;

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
                Description = "2 m open fridge for dairy and frozen food.",
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
            P(grocery, "Soft Drinks", 0.8f, 1.7f, 0, 1.3f, 24);

            var produce = Cat("produce", "Produce", "#6fbf4a", "produce");
            P(produce, "Apples", 0.6f, 1.3f, 10, 1.1f, 40);
            P(produce, "Bananas", 0.3f, 0.7f, 6, 1.2f, 40);
            P(produce, "Potatoes", 0.5f, 1.1f, 20, 0.9f, 30);
            P(produce, "Tomatoes", 0.8f, 1.8f, 5, 0.9f, 30);
            P(produce, "Lettuce", 0.7f, 1.6f, 4, 0.7f, 24);

            var dairy = Cat("dairy", "Dairy & Frozen", "#7fb3e6", "fridge");
            P(dairy, "Milk", 0.7f, 1.4f, 7, 1.4f, 24);
            P(dairy, "Eggs", 1.5f, 3.0f, 21, 1.0f, 12);
            P(dairy, "Cheese", 2.5f, 4.8f, 30, 0.9f, 12);
            P(dairy, "Yogurt", 0.5f, 1.1f, 14, 0.9f, 24);
            P(dairy, "Frozen Pizza", 2.2f, 4.5f, 0, 0.8f, 12);
            P(dairy, "Ice Cream", 2.5f, 5.0f, 0, 0.6f, 12);

            var household = Cat("household", "Household", "#b48ad6", "gondola");
            P(household, "Detergent", 4.0f, 7.5f, 0, 0.6f, 6);
            P(household, "Toilet Paper", 3.0f, 5.5f, 0, 0.8f, 8);
            P(household, "Dish Soap", 1.2f, 2.4f, 0, 0.5f, 12);
            P(household, "Batteries", 3.5f, 7.0f, 0, 0.4f, 12);

            var bakery = Cat("bakery", "Bakery", "#e3c08d", "bakery_shelf");
            bakery.Baked = true;
            bakery.NeedsSpecialist = true;
            bakery.SpecialistRole = Role.Baker;
            P(bakery, "Bread", 0.6f, 2.8f, 2, 1.4f, 12);
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
