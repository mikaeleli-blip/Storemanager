using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    public enum ShiftSlot { Off, Morning, Evening, Night }
    public enum Contract { FullTime, PartTime }
    public enum Rank { Staff, ShiftLead, DeptManager }

    public enum IncidentKind
    {
        Late, LongBreak, CaughtSlacking, NoShow, Sick,
        VerbalWarning, WrittenWarning, Praise, Review, Promotion, Demotion, Raise, Hired, ProbationPassed,
    }

    /// <summary>One line in an employee's record (time clock, things you saw, talks you had).</summary>
    public class StaffIncident
    {
        public int Day;
        public string Time;
        public IncidentKind Kind;
        public string Note;
        public bool Addressed;   // a written warning was given for it
    }

    /// <summary>What the time clock and the till say about one employee over their last shifts.</summary>
    public class ShiftReport
    {
        public int Day;
        public ShiftSlot Slot;
        public float HoursPaid, BusyShare;
        public int LateMinutes, BreakMinutes, SlackMinutes, Output;
    }

    /// <summary>
    /// A job applicant. Their real skills, work ethic, ambition and charm are hidden: the CV gives a rough
    /// picture, interview answers narrow it down (smooth talkers oversell themselves), references tell the truth.
    /// </summary>
    public class Applicant
    {
        public string Name;
        public int Age, YearsExperience;
        public string LastJob;
        public float Speed, Service, Reliability, Leadership, WorkEthic, Ambition, Charm;
        public Specialty Specialty;
        public float AskingWage;
        public int Days;
        public bool WantsPartTime, WantsFullTime;   // neither = either
        public bool NoNights, NoMornings, NoWeekends;
        public bool HasReferences;

        // what you know
        public readonly float[] Min = new float[TraitCount], Max = new float[TraitCount];
        public readonly List<string> Transcript = new List<string>();
        public readonly List<int> Asked = new List<int>();
        public bool Interviewed, ReferencesRequested, ReferencesIn;
        public int ReferencesDay = -1;
        public int DeclinedDay = -1;

        public const int TraitCount = 6;
        public static readonly string[] TraitNames = { "Speed", "Service", "Reliability", "Leadership", "Work ethic", "Ambition" };

        public float True(int t)
        {
            switch (t)
            {
                case 0: return Speed;
                case 1: return Service;
                case 2: return Reliability;
                case 3: return Leadership;
                case 4: return WorkEthic;
                default: return Ambition;
            }
        }

        static readonly string[] Jobs =
        {
            "cashier at a petrol station", "shelf stacker at a discount chain", "waiter", "warehouse picker", "barista",
            "delivery driver", "student (no job)", "fishmonger", "baker's assistant", "call centre agent", "school canteen",
            "night guard", "shop assistant in a clothes shop", "line cook", "summer job at a farm",
        };

        public static Applicant Generate()
        {
            var a = new Applicant
            {
                Name = Names.Random(),
                Speed = Skill(), Service = Skill(), Reliability = Skill(), Leadership = Skill(),
                WorkEthic = Skill(), Ambition = Skill(), Charm = Skill(),
            };
            a.Age = Random.value < 0.35f ? Random.Range(17, 23) : Random.Range(23, 62);
            a.YearsExperience = Mathf.Clamp(Random.Range(0, Mathf.Max(1, a.Age - 16)), 0, 20);
            a.LastJob = a.Age < 20 && Random.value < 0.5f ? "student (no job)" : Jobs[Random.Range(0, Jobs.Length)];
            float r = Random.value;
            a.Specialty = r < 0.15f ? Specialty.Baking : r < 0.27f ? Specialty.Butchery : Specialty.None;
            if (a.LastJob.StartsWith("baker")) a.Specialty = Specialty.Baking;
            if (a.LastJob.StartsWith("fishmonger") || a.LastJob.StartsWith("line cook")) a.Specialty = Specialty.Butchery;

            bool student = a.Age < 23 && Random.value < 0.7f;
            a.WantsPartTime = student || Random.value < 0.12f;
            a.WantsFullTime = !a.WantsPartTime && Random.value < 0.4f;
            a.NoMornings = student && Random.value < 0.6f;
            a.NoNights = Random.value < 0.45f;
            a.NoWeekends = !student && Random.value < 0.12f;
            a.HasReferences = a.YearsExperience > 0 && a.LastJob != "student (no job)";

            // Experience makes people a bit quicker and better with customers.
            a.Speed = Mathf.Clamp(a.Speed + Mathf.Min(2f, a.YearsExperience * 0.2f), 1f, 10f);
            a.Service = Mathf.Clamp(a.Service + Mathf.Min(1.5f, a.YearsExperience * 0.15f), 1f, 10f);

            float avg = (a.Speed + a.Service + a.Reliability) / 3f;
            a.AskingWage = Mathf.Round(11f + avg * 1.0f + a.Leadership * 0.3f + Mathf.Min(3f, a.YearsExperience * 0.25f)
                + (a.Specialty != Specialty.None ? 2f : 0f) + (a.Charm - 5f) * 0.3f);

            // Before the interview you only know roughly what the CV suggests.
            for (int t = 0; t < TraitCount; t++) { a.Min[t] = 1f; a.Max[t] = 10f; }
            float exp = Mathf.Min(1f, a.YearsExperience / 6f);
            a.Narrow(0, a.Speed + Random.Range(-1f, 1f), 3.5f - exp);
            a.Narrow(1, a.Service + Random.Range(-1f, 1f), 3.5f - exp);
            return a;
        }

        public static float Skill()
        {
            return Mathf.Round(Mathf.Clamp((Random.Range(1f, 10f) + Random.Range(1f, 10f)) * 0.5f, 1f, 10f));
        }

        public void Narrow(int t, float centre, float halfWidth)
        {
            float lo = Mathf.Clamp(Mathf.Round(centre - halfWidth), 1f, 10f), hi = Mathf.Clamp(Mathf.Round(centre + halfWidth), 1f, 10f);
            // keep the new range inside what we already know, unless the two don't overlap (then trust the newer, wider one)
            float nlo = Mathf.Max(Min[t], lo), nhi = Mathf.Min(Max[t], hi);
            if (nlo > nhi) { nlo = lo; nhi = hi; }
            Min[t] = nlo;
            Max[t] = nhi;
        }

        public string Estimate(int t)
        {
            if (Min[t] <= 1f && Max[t] >= 10f) return "?";
            return Min[t] == Max[t] ? Min[t].ToString("0") : Min[t].ToString("0") + "-" + Max[t].ToString("0");
        }

        public float Mid(int t) { return (Min[t] + Max[t]) * 0.5f; }

        public string Availability
        {
            get
            {
                var list = new List<string>();
                if (NoMornings) list.Add("no weekday mornings");
                if (NoNights) list.Add("no nights");
                if (NoWeekends) list.Add("no weekends");
                return list.Count == 0 ? "any shift" : string.Join(", ", list.ToArray());
            }
        }

        public string ContractWish
        {
            get { return WantsPartTime ? "part-time only" : WantsFullTime ? "full-time only" : "full or part-time"; }
        }

        public string Cv
        {
            get
            {
                return Age + " years old. " + (YearsExperience > 0 ? YearsExperience + " years working, last as " + LastJob : "No work experience yet")
                    + ". " + (Specialty == Specialty.Baking ? "Has baking experience. " : Specialty == Specialty.Butchery ? "Has worked with meat. " : "")
                    + "Wants " + ContractWish + ", " + Availability + ".";
            }
        }
    }

    /// <summary>The interview questions: what each one tells you, and how people answer.</summary>
    public static class Interview
    {
        public const int MaxQuestions = 3;
        public const int MaxPerDay = 4;

        public class Question
        {
            public string Text;
            public int[] Traits;      // which traits the answer says something about
            public float Error;       // how vague the answer is
            public bool CharmBias;    // smooth talkers sound better than they are
            public string[] Low, Mid, High;
        }

        public static readonly Question[] Questions =
        {
            new Question { Text = "Tell me about your last job.", Traits = new[] { 0, 1 }, Error = 2f, CharmBias = true,
                Low = new[] { "\"It was fine I guess. I did what they told me.\"", "\"Honestly I don't remember much of it.\"" },
                Mid = new[] { "\"I kept things running, nothing fancy. Busy days were okay.\"", "\"Normal stuff. I learned the ropes quickly enough.\"" },
                High = new[] { "\"I was usually the one they called when it got busy. I liked the pace.\"", "\"I trained two new people and customers asked for me by name.\"" } },
            new Question { Text = "How would you handle an angry customer?", Traits = new[] { 1 }, Error = 1f, CharmBias = true,
                Low = new[] { "\"Tell them to calm down or get the manager.\"", "\"Not my problem if they're rude, to be honest.\"" },
                Mid = new[] { "\"Listen, say sorry, and find someone who can fix it.\"", "\"Stay polite and try to sort it out.\"" },
                High = new[] { "\"Let them talk it out, apologise, and fix it right there if I can. Most people just want to be heard.\"",
                               "\"I'd stay calm, offer a solution, and make sure they leave happier than they came.\"" } },
            new Question { Text = "What do you do when there's nothing to do?", Traits = new[] { 4 }, Error = 2f, CharmBias = true,
                Low = new[] { "\"Take a breather? Check my phone for a bit.\"", "\"Wait until someone tells me what to do.\"" },
                Mid = new[] { "\"Tidy up, face the shelves, that kind of thing.\"", "\"Ask my supervisor what needs doing.\"" },
                High = new[] { "\"There's always something: date checks, facing, cleaning the till. I find it.\"", "\"I hate standing around. I'd check the back room and get ahead.\"" } },
            new Question { Text = "Have you ever led a team?", Traits = new[] { 3 }, Error = 1f, CharmBias = true,
                Low = new[] { "\"No, I'd rather just do my own work.\"", "\"Not really my thing.\"" },
                Mid = new[] { "\"I filled in for my shift lead a few times.\"", "\"A little. I can if I have to.\"" },
                High = new[] { "\"I ran the evening shift at my last place. People knew they could count on me.\"", "\"Yes, and I'd like to do more of it.\"" } },
            new Question { Text = "How often were you off sick last year?", Traits = new[] { 2 }, Error = 2f, CharmBias = true,
                Low = new[] { "\"A fair bit, I had a rough year.\" (looks away)", "\"Hard to say. Stuff came up.\"" },
                Mid = new[] { "\"A few days, the usual flu.\"", "\"Not much. Maybe a week in total.\"" },
                High = new[] { "\"Once, I think. I don't miss shifts.\"", "\"Never. I've had perfect attendance two years running.\"" } },
            new Question { Text = "Where do you see yourself in two years?", Traits = new[] { 5 }, Error = 1f, CharmBias = false,
                Low = new[] { "\"Probably doing this, if it's alright.\"", "\"I don't really plan that far.\"" },
                Mid = new[] { "\"Maybe a shift lead, we'll see.\"", "\"Somewhere stable, maybe with more responsibility.\"" },
                High = new[] { "\"Running a department. I want to move up.\"", "\"Your job, honestly.\" (smiles)" } },
            new Question { Text = "How do you cope with a busy Saturday rush?", Traits = new[] { 0, 4 }, Error = 1.5f, CharmBias = true,
                Low = new[] { "\"I get flustered when it's hectic.\"", "\"I'd rather work quiet shifts.\"" },
                Mid = new[] { "\"Just keep going, one customer at a time.\"", "\"It's tiring but fine.\"" },
                High = new[] { "\"Love it. Time flies and I'm quick on the till.\"", "\"That's when I'm at my best.\"" } },
            new Question { Text = "Why do you want to work here?", Traits = new[] { 4, 5 }, Error = 2f, CharmBias = true,
                Low = new[] { "\"Need the money.\" (shrugs)", "\"It's close to my place.\"" },
                Mid = new[] { "\"Steady job, decent people, seems like a good store.\"", "\"I like retail and the hours suit me.\"" },
                High = new[] { "\"I've shopped here for years and I want to help make it the best store in town.\"", "\"I want to learn the business from the ground up.\"" } },
        };

        /// <summary>Asks a question: adds the answer to the transcript and narrows what you know.</summary>
        public static void Ask(Applicant a, int q)
        {
            if (a.Asked.Contains(q) || a.Asked.Count >= MaxQuestions) return;
            a.Asked.Add(q);
            a.Interviewed = true;
            var Q = Questions[q];
            float sum = 0f;
            for (int i = 0; i < Q.Traits.Length; i++)
            {
                int t = Q.Traits[i];
                float seen = a.True(t) + Random.Range(-0.6f, 0.6f) + (Q.CharmBias ? (a.Charm - 5f) * 0.45f : 0f);
                seen = Mathf.Clamp(seen, 1f, 10f);
                a.Narrow(t, seen, Q.Error);
                sum += seen;
            }
            float avg = sum / Q.Traits.Length;
            string[] pool = avg < 4.2f ? Q.Low : avg < 7f ? Q.Mid : Q.High;
            a.Transcript.Add("<b>You:</b> " + Q.Text);
            a.Transcript.Add("<b>" + a.Name.Split(' ')[0] + ":</b> " + pool[Random.Range(0, pool.Length)]);
        }

        /// <summary>The next morning: what former employers say. Honest about reliability and work ethic.</summary>
        public static void References(Applicant a)
        {
            a.ReferencesIn = true;
            if (!a.HasReferences)
            {
                a.Transcript.Add("<i>References: none to call (no previous employer).</i>");
                return;
            }
            a.Narrow(2, a.Reliability + Random.Range(-0.5f, 0.5f), 1f);
            a.Narrow(4, a.WorkEthic + Random.Range(-0.5f, 0.5f), 1f);
            string rel = a.Reliability < 4f ? "often late or off sick" : a.Reliability < 7f ? "turned up fine, mostly" : "never missed a shift";
            string eth = a.WorkEthic < 4f ? "needed watching" : a.WorkEthic < 7f ? "did the job" : "worked hard without being told";
            a.Transcript.Add("<i>References (" + a.LastJob + "): \"" + rel + ", " + eth + ".\"</i>");
        }

        /// <summary>Chance they accept a wage offer.</summary>
        public static float AcceptChance(Applicant a, float offer)
        {
            if (offer >= a.AskingWage) return 1f;
            return Mathf.Clamp01(1f - (a.AskingWage - offer) * 0.35f);
        }
    }
}
