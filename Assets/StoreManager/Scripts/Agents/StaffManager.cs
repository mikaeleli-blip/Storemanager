using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public static class Names
    {
        static readonly string[] First =
        {
            "Anna", "Ben", "Clara", "Daniel", "Elin", "Felix", "Greta", "Hugo", "Ida", "Jonas",
            "Kara", "Leo", "Maja", "Nils", "Olivia", "Pétur", "Rosa", "Sam", "Tara", "Viktor",
            "Wendy", "Yusuf", "Zoe", "Arnar", "Birna", "Carlos", "Dina", "Emil", "Freya", "Gunnar",
            "Hanna", "Ivan", "Julia", "Kevin", "Lena", "Marco", "Nora", "Omar", "Priya", "Ragnar"
        };
        static readonly string[] Last =
        {
            "Berg", "Carter", "Dahl", "Evans", "Fischer", "Garcia", "Hansen", "Ito", "Jensen", "Kowalski",
            "Larsen", "Moreno", "Nowak", "Olsen", "Park", "Quinn", "Rossi", "Sato", "Torres", "Ullman",
            "Vidal", "Walsh", "Young", "Jónsson", "Sigurdsdóttir", "Novak", "Brennan", "Khan", "Lund", "Meyer"
        };

        public static string Random()
        {
            return First[UnityEngine.Random.Range(0, First.Length)] + " " + Last[UnityEngine.Random.Range(0, Last.Length)];
        }
    }

    public class Applicant
    {
        public string Name;
        public float Speed, Service, Reliability, Leadership;
        public Specialty Specialty;
        public float AskingWage;
        public int Days;

        public static Applicant Generate()
        {
            var a = new Applicant
            {
                Name = Names.Random(),
                Speed = Skill(),
                Service = Skill(),
                Reliability = Skill(),
                Leadership = Skill(),
            };
            float r = Random.value;
            a.Specialty = r < 0.15f ? Specialty.Baking : r < 0.27f ? Specialty.Butchery : Specialty.None;
            float avg = (a.Speed + a.Service + a.Reliability) / 3f;
            a.AskingWage = Mathf.Round(11f + avg * 1.2f + a.Leadership * 0.35f + (a.Specialty != Specialty.None ? 2f : 0f));
            return a;
        }

        static float Skill()
        {
            return Mathf.Round(Mathf.Clamp((Random.Range(1f, 10f) + Random.Range(1f, 10f)) * 0.5f, 1f, 10f));
        }
    }

    /// <summary>Roster, hiring pool, shifts and payroll.</summary>
    public class StaffManager
    {
        public readonly List<Employee> All = new List<Employee>();
        public readonly List<Applicant> Applicants = new List<Applicant>();
        readonly Store store;

        public StaffManager(Store store) { this.store = store; }

        public Employee Assistant
        {
            get
            {
                for (int i = 0; i < All.Count; i++)
                    if (All[i] != null && All[i].Role == Role.AssistantManager && !All[i].Leaving) return All[i];
                return null;
            }
        }

        public static int ShiftStart(Shift s) { return s == Shift.Morning ? 7 : s == Shift.Evening ? 14 : 22; }
        public static int ShiftEnd(Shift s) { return s == Shift.Morning ? 15 : s == Shift.Evening ? 22 : 6; }
        public static string ShiftName(Shift s)
        {
            return s == Shift.Morning ? "Morning 07-15" : s == Shift.Evening ? "Evening 14-22" : "Night 22-06";
        }

        public static bool ShiftActiveAt(Shift s, int hour)
        {
            int a = ShiftStart(s), b = ShiftEnd(s);
            return a < b ? hour >= a && hour < b : hour >= a || hour < b;
        }

        public void OnHour(int hour)
        {
            var copy = All.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                var e = copy[i];
                if (e == null) continue;
                if (hour == ShiftEnd(e.Shift)) e.EndShift();
            }
            for (int i = 0; i < copy.Length; i++)
            {
                var e = copy[i];
                if (e == null || e.Leaving) continue;
                if (hour == ShiftStart(e.Shift)) e.StartShift(true);
            }
        }

        public int OnShiftCount(Role r)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Role == r && All[i].OnShift) n++;
            return n;
        }

        public bool AnyOnShift(Role r)
        {
            for (int i = 0; i < All.Count; i++)
            {
                var e = All[i];
                if (e != null && e.Role == r && e.OnShift && !e.ShiftEnding) return true;
            }
            return false;
        }

        public bool AnyScheduled(Role r)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].Role == r && !All[i].Leaving) return true;
            return false;
        }

        public Employee Hire(Applicant a, Role role, Shift shift, Category category)
        {
            if (role == Role.AssistantManager && Assistant != null) role = Role.Stocker;
            var go = new GameObject("Staff " + a.Name);
            var e = go.AddComponent<Employee>();
            e.DisplayName = a.Name;
            e.Role = role;
            e.Shift = shift;
            e.AssignedCategory = role == Role.Stocker ? category : null;
            e.Speed = a.Speed;
            e.Service = a.Service;
            e.Reliability = a.Reliability;
            e.Leadership = a.Leadership;
            e.Specialty = a.Specialty;
            e.AskingWage = a.AskingWage;
            e.Wage = a.AskingWage;
            e.HiredDay = store.Clock.Day;
            e.BuildBody(Employee.RoleColor(role), new Color(0.93f, 0.78f, 0.65f), 1.6f);
            All.Add(e);
            Applicants.Remove(a);

            if (ShiftActiveAt(shift, store.Clock.Hour)) e.StartShift(false);
            else go.SetActive(false);
            return e;
        }

        public void Remove(Employee e)
        {
            All.Remove(e);
        }

        public void Fire(Employee e)
        {
            if (e == null || e.Leaving) return;
            store.Message("You let " + e.DisplayName + " go.");
            All.Remove(e);
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null) All[i].Morale = Mathf.Max(0f, All[i].Morale - 3f);
            e.Dismiss();
        }

        public void SetRole(Employee e, Role r)
        {
            if (r == Role.AssistantManager && Assistant != null && Assistant != e) return;
            e.Role = r;
            if (r != Role.Stocker) e.AssignedCategory = null;
            e.RefreshColor();
            e.Restart();
        }

        public void SetShift(Employee e, Shift s)
        {
            e.Shift = s;
            bool activeNow = ShiftActiveAt(s, store.Clock.Hour);
            if (e.OnShift && !activeNow) e.EndShift();
            else if (!e.OnShift && activeNow) e.StartShift(false);
        }

        public void SetCategory(Employee e, Category c)
        {
            e.AssignedCategory = c;
            e.Restart();
        }

        public void RefreshApplicants(bool full)
        {
            if (full) Applicants.Clear();
            for (int i = Applicants.Count - 1; i >= 0; i--)
            {
                Applicants[i].Days++;
                if (Applicants[i].Days > 3) Applicants.RemoveAt(i);
            }
            while (Applicants.Count < 8) Applicants.Add(Applicant.Generate());
        }

        /// <summary>The starting crew.</summary>
        public void HireStartingStaff()
        {
            HireFixed(Role.Cashier, Shift.Morning, null);
            HireFixed(Role.Cashier, Shift.Morning, null);
            HireFixed(Role.Stocker, Shift.Morning, null);
            HireFixed(Role.Stocker, Shift.Morning, null);
            HireFixed(Role.Cleaner, Shift.Morning, null);
            HireFixed(Role.Cashier, Shift.Evening, null);
            HireFixed(Role.Cashier, Shift.Evening, null);
            HireFixed(Role.Stocker, Shift.Evening, null);
        }

        void HireFixed(Role r, Shift s, Category d)
        {
            var a = Applicant.Generate();
            a.Specialty = Specialty.None;
            a.Leadership = Mathf.Min(a.Leadership, 5f);
            a.AskingWage = Mathf.Round(11f + (a.Speed + a.Service + a.Reliability) / 3f * 1.2f + a.Leadership * 0.35f);
            Applicants.Add(a);
            Hire(a, r, s, d);
        }
    }
}
