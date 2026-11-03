using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public enum DeptKind { Shelves, Bakery, Counter }
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

        public ProductDef(string name, float cost, float price, int life, float demand, int caseSize)
        {
            Name = name; Cost = cost; BasePrice = price; ShelfLifeDays = life; Demand = demand; CaseSize = caseSize;
        }
    }

    public class DepartmentDef
    {
        public string Id;
        public string Name;
        public DeptKind Kind;
        public Color Color;
        public float BuildCost;
        public bool NeedsSpecialist;
        public Role SpecialistRole;
        public readonly List<ProductDef> Products = new List<ProductDef>();

        public float TotalDemand
        {
            get { float t = 0f; for (int i = 0; i < Products.Count; i++) t += Products[i].Demand; return t; }
        }
    }

    /// <summary>
    /// All departments and products. The store type is just data: add or swap
    /// departments here without touching the simulation.
    /// </summary>
    public static class Catalog
    {
        public static readonly List<DepartmentDef> Departments = new List<DepartmentDef>();
        public static float TotalDemand;

        static Catalog()
        {
            var grocery = Dept("grocery", "Grocery", DeptKind.Shelves, "#d9a441", 0f);
            grocery.Products.Add(new ProductDef("Pasta", 1.1f, 2.0f, 0, 1.0f, 24));
            grocery.Products.Add(new ProductDef("Rice", 1.6f, 2.9f, 0, 0.8f, 12));
            grocery.Products.Add(new ProductDef("Canned Soup", 0.9f, 1.8f, 0, 0.9f, 24));
            grocery.Products.Add(new ProductDef("Cereal", 2.2f, 4.2f, 0, 1.0f, 12));
            grocery.Products.Add(new ProductDef("Coffee", 4.5f, 8.5f, 0, 0.8f, 12));
            grocery.Products.Add(new ProductDef("Snacks", 1.4f, 2.8f, 0, 1.2f, 24));
            grocery.Products.Add(new ProductDef("Soft Drinks", 0.8f, 1.7f, 0, 1.3f, 24));

            var produce = Dept("produce", "Produce", DeptKind.Shelves, "#6fbf4a", 0f);
            produce.Products.Add(new ProductDef("Apples", 0.6f, 1.3f, 10, 1.1f, 40));
            produce.Products.Add(new ProductDef("Bananas", 0.3f, 0.7f, 6, 1.2f, 40));
            produce.Products.Add(new ProductDef("Potatoes", 0.5f, 1.1f, 20, 0.9f, 30));
            produce.Products.Add(new ProductDef("Tomatoes", 0.8f, 1.8f, 5, 0.9f, 30));
            produce.Products.Add(new ProductDef("Lettuce", 0.7f, 1.6f, 4, 0.7f, 24));

            var dairy = Dept("dairy", "Dairy & Frozen", DeptKind.Shelves, "#7fb3e6", 0f);
            dairy.Products.Add(new ProductDef("Milk", 0.7f, 1.4f, 7, 1.4f, 24));
            dairy.Products.Add(new ProductDef("Eggs", 1.5f, 3.0f, 21, 1.0f, 12));
            dairy.Products.Add(new ProductDef("Cheese", 2.5f, 4.8f, 30, 0.9f, 12));
            dairy.Products.Add(new ProductDef("Yogurt", 0.5f, 1.1f, 14, 0.9f, 24));
            dairy.Products.Add(new ProductDef("Frozen Pizza", 2.2f, 4.5f, 0, 0.8f, 12));
            dairy.Products.Add(new ProductDef("Ice Cream", 2.5f, 5.0f, 0, 0.6f, 12));

            var household = Dept("household", "Household", DeptKind.Shelves, "#b48ad6", 4000f);
            household.Products.Add(new ProductDef("Detergent", 4.0f, 7.5f, 0, 0.6f, 6));
            household.Products.Add(new ProductDef("Toilet Paper", 3.0f, 5.5f, 0, 0.8f, 8));
            household.Products.Add(new ProductDef("Dish Soap", 1.2f, 2.4f, 0, 0.5f, 12));
            household.Products.Add(new ProductDef("Batteries", 3.5f, 7.0f, 0, 0.4f, 12));

            var bakery = Dept("bakery", "Bakery", DeptKind.Bakery, "#e3c08d", 9000f);
            bakery.NeedsSpecialist = true;
            bakery.SpecialistRole = Role.Baker;
            bakery.Products.Add(new ProductDef("Bread", 0.6f, 2.8f, 2, 1.4f, 12));
            bakery.Products.Add(new ProductDef("Croissants", 0.4f, 1.6f, 1, 1.0f, 12));
            bakery.Products.Add(new ProductDef("Cakes", 3.0f, 9.0f, 3, 0.5f, 6));

            var deli = Dept("deli", "Deli & Meat", DeptKind.Counter, "#d86a5c", 12000f);
            deli.NeedsSpecialist = true;
            deli.SpecialistRole = Role.DeliClerk;
            deli.Products.Add(new ProductDef("Sliced Ham", 1.8f, 3.8f, 5, 0.6f, 20));
            deli.Products.Add(new ProductDef("Chicken", 3.0f, 6.0f, 4, 0.7f, 12));
            deli.Products.Add(new ProductDef("Salads", 1.2f, 3.2f, 3, 0.4f, 12));
            deli.Products.Add(new ProductDef("Steak", 6.0f, 12.0f, 4, 0.3f, 10));

            TotalDemand = 0f;
            for (int i = 0; i < Departments.Count; i++) TotalDemand += Departments[i].TotalDemand;
        }

        static DepartmentDef Dept(string id, string name, DeptKind kind, string hex, float cost)
        {
            var d = new DepartmentDef { Id = id, Name = name, Kind = kind, Color = Mats.Hex(hex), BuildCost = cost };
            Departments.Add(d);
            return d;
        }

        public static DepartmentDef Get(string id)
        {
            for (int i = 0; i < Departments.Count; i++)
                if (Departments[i].Id == id) return Departments[i];
            return null;
        }

        public static DepartmentDef RandomDepartment()
        {
            float r = Random.value * TotalDemand;
            for (int i = 0; i < Departments.Count; i++)
            {
                r -= Departments[i].TotalDemand;
                if (r <= 0f) return Departments[i];
            }
            return Departments[Departments.Count - 1];
        }

        public static ProductDef RandomProduct(DepartmentDef d)
        {
            float r = Random.value * d.TotalDemand;
            for (int i = 0; i < d.Products.Count; i++)
            {
                r -= d.Products[i].Demand;
                if (r <= 0f) return d.Products[i];
            }
            return d.Products[d.Products.Count - 1];
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
