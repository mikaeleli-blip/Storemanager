using System.Collections.Generic;

namespace StoreManager
{
    public class DayLedger
    {
        public int Day;
        public float Sales, Wholesale, Wages, Rent, Construction, WasteValue;
        public int Customers, Walkouts, Complaints, ComplaintsResolved, ItemsSold;
        public float SatisfactionSum;
        public readonly Dictionary<string, int> NotCarried = new Dictionary<string, int>();
        public readonly Dictionary<string, float> SatisfactionLost = new Dictionary<string, float>();

        public void AddLoss(string reason, float points)
        {
            float v;
            SatisfactionLost.TryGetValue(reason, out v);
            SatisfactionLost[reason] = v + points;
        }

        public float Costs { get { return Wholesale + Wages + Rent + Construction; } }
        public float Profit { get { return Sales - Costs; } }
        public float AvgSatisfaction { get { return Customers > 0 ? SatisfactionSum / Customers : 0f; } }

        public void AddNotCarried(string dept)
        {
            int n;
            NotCarried.TryGetValue(dept, out n);
            NotCarried[dept] = n + 1;
        }
    }

    public class Economy
    {
        public float Cash = 60000f;
        public DayLedger Today = new DayLedger { Day = 1 };
        public readonly List<DayLedger> History = new List<DayLedger>();

        public void Sale(float amount, int items)
        {
            Cash += amount;
            Today.Sales += amount;
            Today.ItemsSold += items;
        }

        public void PayWholesale(float amount) { Cash -= amount; Today.Wholesale += amount; }
        public void PayWages(float amount) { Cash -= amount; Today.Wages += amount; }
        public void PayRent(float amount) { Cash -= amount; Today.Rent += amount; }
        public void PayConstruction(float amount) { Cash -= amount; Today.Construction += amount; }

        public DayLedger CloseDay(int newDay)
        {
            var done = Today;
            History.Add(done);
            if (History.Count > 60) History.RemoveAt(0);
            Today = new DayLedger { Day = newDay };
            return done;
        }

        public static string Money(float v)
        {
            string s = "$" + System.Math.Abs(v).ToString("#,0");
            return v < 0f ? "-" + s : s;
        }
    }

    /// <summary>Jobs the player hands to the assistant manager.</summary>
    public class Delegation
    {
        public bool Ordering = true;
        public bool Complaints = true;
        public bool Checkouts = true;
        public bool Morale = true;
    }
}
