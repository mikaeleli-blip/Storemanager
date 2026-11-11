using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Save games: the whole store as plain text, one record per line (tag, then tab-separated fields).
    /// What is kept: clock, money and ledgers, licences, level, the building, every fixture with its product
    /// and stock (with sell-by dates), back room, orders, suppliers and bills, staff with their contracts,
    /// rosters, records and what you know about them, applicants and interviews, the journal.
    /// What starts fresh on load: customers in the store, spills, and whatever staff were in the middle of
    /// (people who are rostered now clock straight back in).
    /// </summary>
    public partial class Store
    {
        public const int SaveVersion = 1;
        const string Magic = "STOREMANAGER-SAVE";

        // ------------------------------------------------------------------ text format

        class Writer
        {
            readonly StringBuilder sb = new StringBuilder();

            public Writer Rec(string tag) { if (sb.Length > 0) sb.Append('\n'); sb.Append(tag); return this; }
            void Sep() { sb.Append('\t'); }
            public Writer I(int v) { Sep(); sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
            public Writer F(float v) { Sep(); sb.Append(v.ToString("R", CultureInfo.InvariantCulture)); return this; }
            public Writer B(bool v) { Sep(); sb.Append(v ? '1' : '0'); return this; }
            public Writer S(string v)
            {
                Sep();
                if (v == null) { sb.Append("\\0"); return this; }
                for (int i = 0; i < v.Length; i++)
                {
                    char c = v[i];
                    if (c == '\\') sb.Append("\\\\");
                    else if (c == '\t') sb.Append("\\t");
                    else if (c == '\n') sb.Append("\\n");
                    else if (c == '\r') { }
                    else sb.Append(c);
                }
                return this;
            }
            public Writer Pile(StockPile p)
            {
                I(p.Batches.Count);
                for (int i = 0; i < p.Batches.Count; i++) { I(p.Batches[i].Qty); I(p.Batches[i].DaysLeft); }
                return this;
            }
            public override string ToString() { return sb.ToString(); }
        }

        class Reader
        {
            readonly string[] f;
            int k = 1;
            public readonly string Tag;

            public Reader(string line) { f = line.Split('\t'); Tag = f[0]; }
            string Next() { return k < f.Length ? f[k++] : "0"; }
            public bool More { get { return k < f.Length; } }
            public int I() { return int.Parse(Next(), CultureInfo.InvariantCulture); }
            public float F() { return float.Parse(Next(), CultureInfo.InvariantCulture); }
            public bool B() { return Next() == "1"; }
            public string S()
            {
                string v = Next();
                if (v == "\\0") return null;
                if (v.IndexOf('\\') < 0) return v;
                var sb = new StringBuilder(v.Length);
                for (int i = 0; i < v.Length; i++)
                {
                    char c = v[i];
                    if (c == '\\' && i + 1 < v.Length)
                    {
                        char n = v[++i];
                        sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n);
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }
            public void Pile(StockPile p)
            {
                p.Clear();
                int n = I();
                for (int i = 0; i < n; i++) { int q = I(); int d = I(); p.Add(q, d); }
            }
            public T E<T>() where T : struct { return (T)System.Enum.ToObject(typeof(T), I()); }
        }

        // ------------------------------------------------------------------ write

        /// <summary>Summary line shown in the load list (day, money, level).</summary>
        public string SaveSummary
        {
            get { return "Day " + Clock.Day + " " + Clock.Weekday + " " + Clock.TimeText + " · " + Economy.Money(Economy.Cash) + " · Level " + Level; }
        }

        public string WriteSave()
        {
            var w = new Writer();
            w.Rec(Magic).I(SaveVersion).S(SaveSummary);
            w.Rec("clock").I(Clock.Day).F(Clock.Minute).I(Clock.SpeedIndex).B(Clock.ClosedBoost);
            w.Rec("store").I(SlotCount).B(ExpansionPending).F(Reputation).I(Level).I(XP).B(StartChosen).B(Opened).I(OpenedDay)
                .I(nextFixtureId).I(nextInvoice).I((int)DateChecks).F(expressTimer).F(lastDailyCheck)
                .F(lastShiftCheck[0]).F(lastShiftCheck[1]).F(lastShiftCheck[2]);
            w.Rec("cash").F(Economy.Cash);
            var d = Delegation;
            w.Rec("delegation").B(d.Ordering).B(d.Complaints).B(d.Checkouts).B(d.Morale).B(d.Bills);
            for (int i = 0; i < Economy.History.Count; i++) Ledger(w, "ledger", Economy.History[i]);
            Ledger(w, "today", Economy.Today);
            foreach (var l in owned) w.Rec("licence").S(l.Id);
            for (int i = 0; i < Categories.Count; i++)
                w.Rec("category").S(Categories[i].Def.Id).F(Categories[i].PriceMult).F(Categories[i].SalesToday);
            for (int i = 0; i < Fixtures.Count; i++)
            {
                var f = Fixtures[i];
                if (f.Removed) continue;
                w.Rec("fixture").S(f.Def.Id).I(f.Id).I(f.Origin.x).I(f.Origin.y).I(f.Rot).S(f.Product != null ? f.Product.Name : null).Pile(WithCarried(f.Stock, f.Product, f));
            }
            // After the fixtures: putting a product on its first shelf resets its par.
            for (int i = 0; i < Products.Count; i++)
            {
                var p = Products[i];
                w.Rec("product").S(p.Name).I(p.OnOrder).I(p.OnExpress).I(p.Par).I(p.SoldToday).I(p.MissedToday).I(p.PriceSkipsToday)
                    .I(p.ExpiredFoundToday).I(p.BinnedToday).I(p.ExpiredLastNight).Pile(WithCarried(p.Back, p, null)).I(p.SoldHistory.Count);
                for (int k = 0; k < p.SoldHistory.Count; k++) w.I(p.SoldHistory[k]);
            }
            for (int i = 0; i < Suppliers.Count; i++)
            {
                var s = Suppliers[i];
                w.Rec("supplier").S(s.Def.Id).I(s.PendingDay).F(s.Credit).F(s.ReturnsValue).I(s.ReturnsUnits).B(s.OnHold);
            }
            for (int i = 0; i < Invoices.Count; i++)
            {
                var v = Invoices[i];
                w.Rec("invoice").I(v.Number).S(v.Supplier != null ? v.Supplier.Def.Id : null).I(v.IssuedDay).I(v.DueDay).I(v.PaidDay)
                    .I(v.Units).B(v.Express).B(v.Paid).F(v.Goods).F(v.CreditUsed).F(v.LateFee);
            }
            for (int i = 0; i < Cart.Lines.Count; i++) w.Rec("cart").S(Cart.Lines[i].Product.Name).I(Cart.Lines[i].Cases);
            w.Rec("staffday").I(Staff.InterviewsToday);
            for (int i = 0; i < Staff.All.Count; i++) if (Staff.All[i] != null) WriteEmployee(w, Staff.All[i]);
            for (int i = 0; i < Staff.Applicants.Count; i++) WriteApplicant(w, Staff.Applicants[i]);
            for (int i = 0; i < Log.Count; i++) w.Rec("log").S(Log[i]);
            return w.ToString();
        }

        static void Ledger(Writer w, string tag, DayLedger l)
        {
            w.Rec(tag).I(l.Day).F(l.Sales).F(l.Wholesale).F(l.Wages).F(l.Rent).F(l.Construction).F(l.WasteValue).F(l.ReturnCredit)
                .F(l.LateFees).I(l.WrittenOff).I(l.ExpiredFound).I(l.Customers).I(l.Walkouts).I(l.Complaints).I(l.ComplaintsResolved)
                .I(l.ItemsSold).F(l.SatisfactionSum).I(l.NotCarried.Count);
            foreach (var kv in l.NotCarried) w.S(kv.Key).I(kv.Value);
            w.I(l.SatisfactionLost.Count);
            foreach (var kv in l.SatisfactionLost) w.S(kv.Key).F(kv.Value);
        }

        static void ReadLedger(Reader r, DayLedger l)
        {
            l.Day = r.I(); l.Sales = r.F(); l.Wholesale = r.F(); l.Wages = r.F(); l.Rent = r.F(); l.Construction = r.F();
            l.WasteValue = r.F(); l.ReturnCredit = r.F(); l.LateFees = r.F(); l.WrittenOff = r.I(); l.ExpiredFound = r.I();
            l.Customers = r.I(); l.Walkouts = r.I(); l.Complaints = r.I(); l.ComplaintsResolved = r.I(); l.ItemsSold = r.I();
            l.SatisfactionSum = r.F();
            int n = r.I();
            for (int i = 0; i < n; i++) { string k = r.S(); l.NotCarried[k] = r.I(); }
            n = r.I();
            for (int i = 0; i < n; i++) { string k = r.S(); l.SatisfactionLost[k] = r.F(); }
        }

        /// <summary>
        /// Stock in someone's arms is saved where it would end up if they were interrupted: bakers' fresh
        /// batches on the shelf they were filling, everything else back in the stock room.
        /// </summary>
        StockPile WithCarried(StockPile pile, Product p, Fixture f)
        {
            if (p == null) return pile;
            StockPile all = null;
            for (int i = 0; i < Staff.All.Count; i++)
            {
                var e = Staff.All[i];
                if (e == null || e.CarryingProduct != p) continue;
                bool toShelf = p.Baked && e.Claimed != null && !e.Claimed.Removed && e.Claimed.Product == p;
                if (f != null ? !(toShelf && e.Claimed == f) : p.Baked) continue;
                if (all == null)
                {
                    all = new StockPile();
                    for (int b = 0; b < pile.Batches.Count; b++) all.Add(pile.Batches[b].Qty, pile.Batches[b].DaysLeft);
                }
                var c = e.Carrying;
                for (int b = 0; b < c.Batches.Count; b++) all.Add(c.Batches[b].Qty, c.Batches[b].DaysLeft);
            }
            return all ?? pile;
        }

        static void WriteEmployee(Writer w, Employee e)
        {
            w.Rec("employee").S(e.DisplayName).I((int)e.Role).I((int)e.Shift).S(e.AssignedCategory != null ? e.AssignedCategory.Def.Id : null)
                .F(e.Speed).F(e.Service).F(e.Reliability).F(e.Leadership).F(e.WorkEthic).F(e.Ambition).F(e.Charm)
                .I((int)e.Specialty).F(e.Wage).F(e.AskingWage).F(e.Morale).I(e.HiredDay)
                .I((int)e.Contract).B(e.NoNights).B(e.NoMornings).B(e.NoWeekends).I(e.ShiftsThisWeek).I(e.ShiftsWorked)
                .B(e.Overtime).B(e.ShortRest).I((int)e.Rank).S(e.Department != null ? e.Department.Def.Id : null)
                .B(e.OrdersForDepartment).I(e.ProbationEnds).I(e.NextReviewDay).I(e.WarnedDay).I(e.WrittenDay)
                .I(e.PromotionWaitShifts).I(e.PraisedDay).I(e.LowMoraleShifts).S(e.MoraleNote).B(e.OnShift && !e.ShiftEnding);
            for (int i = 0; i < 7; i++) w.I((int)e.Week[i]);
            for (int t = 0; t < Applicant.TraitCount; t++) w.F(e.Min[t]).F(e.Max[t]);
            for (int i = 0; i < e.Record.Count; i++)
            {
                var x = e.Record[i];
                w.Rec("record").I(x.Day).S(x.Time).I((int)x.Kind).S(x.Note).B(x.Addressed);
            }
            for (int i = 0; i < e.Reports.Count; i++)
            {
                var x = e.Reports[i];
                w.Rec("report").I(x.Day).I((int)x.Slot).F(x.HoursPaid).F(x.BusyShare).I(x.LateMinutes).I(x.BreakMinutes)
                    .I(x.SlackMinutes).I(x.Output);
            }
        }

        static void WriteApplicant(Writer w, Applicant a)
        {
            w.Rec("applicant").S(a.Name).I(a.Age).I(a.YearsExperience).S(a.LastJob)
                .F(a.Speed).F(a.Service).F(a.Reliability).F(a.Leadership).F(a.WorkEthic).F(a.Ambition).F(a.Charm)
                .I((int)a.Specialty).F(a.AskingWage).I(a.Days).B(a.WantsPartTime).B(a.WantsFullTime)
                .B(a.NoNights).B(a.NoMornings).B(a.NoWeekends).B(a.HasReferences)
                .B(a.Interviewed).B(a.ReferencesRequested).B(a.ReferencesIn).I(a.ReferencesDay).I(a.DeclinedDay);
            for (int t = 0; t < Applicant.TraitCount; t++) w.F(a.Min[t]).F(a.Max[t]);
            w.I(a.Asked.Count);
            for (int i = 0; i < a.Asked.Count; i++) w.I(a.Asked[i]);
            w.I(a.Transcript.Count);
            for (int i = 0; i < a.Transcript.Count; i++) w.S(a.Transcript[i]);
        }

        // ------------------------------------------------------------------ read

        /// <summary>Reads the summary line of a save without loading it (null if it isn't a save).</summary>
        public static string PeekSummary(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int nl = text.IndexOf('\n');
            var r = new Reader(nl < 0 ? text : text.Substring(0, nl));
            if (r.Tag != Magic) return null;
            r.I();
            return r.S();
        }

        /// <summary>
        /// Puts a saved game into this freshly started store (call straight after it is created).
        /// Returns null, or what went wrong.
        /// </summary>
        public string ApplySave(string text)
        {
            if (PeekSummary(text) == null) return "That isn't a Storemanager save.";
            var lines = text.Replace("\r", "").Split('\n');
            if (new Reader(lines[0]).I() > SaveVersion) return "That save is from a newer version of the game.";

            int line = 0;
            try
            {
                // The building first (a bigger store needs its walls before fixtures go in).
                for (line = 1; line < lines.Length; line++)
                {
                    var r = new Reader(lines[line]);
                    if (r.Tag != "store") continue;
                    int slots = r.I();
                    if (slots != SlotCount)
                    {
                        SlotCount = slots;
                        Builder.BuildAll();
                        UpdateCameraBounds();
                    }
                    break;
                }

                owned.Clear();
                Employee lastEmployee = null;
                Applicant lastApplicant = null;
                Log.Clear();
                Economy.History.Clear();

                for (line = 1; line < lines.Length; line++)
                {
                    if (lines[line].Length == 0) continue;
                    var r = new Reader(lines[line]);
                    switch (r.Tag)
                    {
                        case "clock":
                            Clock.Day = r.I();
                            Clock.Minute = r.F();
                            int speed = r.I();
                            Clock.ClosedBoost = r.B();
                            Clock.SetSpeed(0);
                            if (speed > 0) Clock.SetSpeed(speed);
                            Clock.SetSpeed(0);   // loaded games start paused
                            break;
                        case "store":
                            r.I();
                            ExpansionPending = r.B();
                            Reputation = r.F();
                            Level = r.I();
                            XP = r.I();
                            StartChosen = r.B();
                            Opened = r.B();
                            OpenedDay = r.I();
                            int nextFix = r.I();
                            nextInvoice = r.I();
                            DateChecks = r.E<DateCheckPolicy>();
                            expressTimer = r.F();
                            lastDailyCheck = r.F();
                            for (int i = 0; i < 3; i++) lastShiftCheck[i] = r.F();
                            savedNextFixture = nextFix;
                            break;
                        case "cash":
                            Economy.Cash = r.F();
                            break;
                        case "delegation":
                            Delegation.Ordering = r.B(); Delegation.Complaints = r.B(); Delegation.Checkouts = r.B();
                            Delegation.Morale = r.B(); Delegation.Bills = r.B();
                            break;
                        case "ledger":
                            var old = new DayLedger();
                            ReadLedger(r, old);
                            Economy.History.Add(old);
                            break;
                        case "today":
                            Economy.Today = new DayLedger();
                            ReadLedger(r, Economy.Today);
                            break;
                        case "licence":
                            var lic = Catalog.LicenceById(r.S());
                            if (lic != null) owned.Add(lic);
                            break;
                        case "category":
                            var cd = Catalog.CategoryById(r.S());
                            var cat = cd != null ? CategoryOf(cd) : null;
                            if (cat != null) { cat.PriceMult = r.F(); cat.SalesToday = r.F(); }
                            break;
                        case "fixture":
                            ReadFixture(r);
                            break;
                        case "product":
                            ReadProduct(r);
                            break;
                        case "supplier":
                            var sd = Catalog.SupplierById(r.S());
                            var ss = sd != null ? SupplierState(sd) : null;
                            if (ss != null)
                            {
                                ss.PendingDay = r.I(); ss.Credit = r.F(); ss.ReturnsValue = r.F(); ss.ReturnsUnits = r.I(); ss.OnHold = r.B();
                            }
                            break;
                        case "invoice":
                            var inv = new Invoice { Number = r.I() };
                            var isd = Catalog.SupplierById(r.S());
                            inv.Supplier = isd != null ? SupplierState(isd) : null;
                            inv.IssuedDay = r.I(); inv.DueDay = r.I(); inv.PaidDay = r.I(); inv.Units = r.I();
                            inv.Express = r.B(); inv.Paid = r.B(); inv.Goods = r.F(); inv.CreditUsed = r.F(); inv.LateFee = r.F();
                            if (inv.Supplier != null) Invoices.Add(inv);
                            break;
                        case "cart":
                            var cp = Catalog.ProductByName(r.S());
                            int cases = r.I();
                            if (cp != null) Cart.Add(ProductOf(cp), cases);
                            break;
                        case "staffday":
                            Staff.InterviewsToday = r.I();
                            break;
                        case "employee":
                            lastEmployee = ReadEmployee(r);
                            break;
                        case "record":
                            if (lastEmployee != null)
                                lastEmployee.Record.Add(new StaffIncident { Day = r.I(), Time = r.S(), Kind = r.E<IncidentKind>(), Note = r.S(), Addressed = r.B() });
                            break;
                        case "report":
                            if (lastEmployee != null)
                                lastEmployee.Reports.Add(new ShiftReport
                                {
                                    Day = r.I(), Slot = r.E<ShiftSlot>(), HoursPaid = r.F(), BusyShare = r.F(), LateMinutes = r.I(),
                                    BreakMinutes = r.I(), SlackMinutes = r.I(), Output = r.I()
                                });
                            break;
                        case "applicant":
                            if (lastApplicant == null) Staff.Applicants.Clear();
                            lastApplicant = ReadApplicant(r);
                            Staff.Applicants.Add(lastApplicant);
                            break;
                        case "log":
                            Log.Add(r.S());
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                return "The save is damaged (line " + (line + 1) + "): " + ex.Message;
            }

            nextFixtureId = Mathf.Max(nextFixtureId, savedNextFixture);
            Economy.Today.Day = Clock.Day;
            if (InOpeningWeek) PutUpOpeningDecor();
            for (int i = 0; i < Fixtures.Count; i++) Fixtures[i].ResetVisuals();
            RepathAll();

            // Whoever was at work when you saved carries on (their shift was already counted).
            for (int i = 0; i < workingAtSave.Count; i++)
            {
                var e = workingAtSave[i];
                var now = Staff.SlotNow(e);
                if (now == ShiftSlot.Off) continue;
                int week = e.ShiftsThisWeek, worked = e.ShiftsWorked;
                bool overtime = e.Overtime, shortRest = e.ShortRest;
                e.StartShift(StaffManager.ToShift(now), false);
                e.ShiftsThisWeek = week;
                e.ShiftsWorked = worked;
                e.Overtime = overtime;
                e.ShortRest = shortRest;
            }
            workingAtSave.Clear();
            Message("Game loaded.");
            return null;
        }

        int savedNextFixture;
        readonly List<Employee> workingAtSave = new List<Employee>();

        void ReadFixture(Reader r)
        {
            var def = Catalog.FixtureById(r.S());
            nextFixtureId = r.I();   // keep the saved id (it seeds how stock is arranged on the shelf)
            var origin = new Vector2Int(r.I(), r.I());
            int rot = r.I();
            string product = r.S();
            if (def == null) return;
            // Put back without paying again (the build cost is already in the books).
            float cash = Economy.Cash, built = Economy.Today.Construction;
            Economy.Cash = 1e9f;
            string why = CanPlace(def, origin, rot);
            var f = why == null ? Place(def, origin, rot) : null;
            Economy.Cash = cash;
            Economy.Today.Construction = built;
            if (why != null) Message("Couldn't put back the " + def.Name + " at " + origin + ": " + why);
            if (f == null) return;
            if (product != null)
            {
                var pd = Catalog.ProductByName(product);
                if (pd != null) AssignProduct(f, pd);
            }
            r.Pile(f.Stock);
        }

        void ReadProduct(Reader r)
        {
            var pd = Catalog.ProductByName(r.S());
            if (pd == null) return;
            var p = ProductOf(pd);
            p.OnOrder = r.I(); p.OnExpress = r.I(); p.Par = r.I(); p.SoldToday = r.I(); p.MissedToday = r.I();
            p.PriceSkipsToday = r.I(); p.ExpiredFoundToday = r.I(); p.BinnedToday = r.I(); p.ExpiredLastNight = r.I();
            r.Pile(p.Back);
            p.SoldHistory.Clear();
            int n = r.I();
            for (int i = 0; i < n; i++) p.SoldHistory.Add(r.I());
        }

        Employee ReadEmployee(Reader r)
        {
            var a = new Applicant { Name = r.S() };
            var role = r.E<Role>();
            var shift = r.E<Shift>();
            var assigned = Catalog.CategoryById(r.S() ?? "");
            a.Speed = r.F(); a.Service = r.F(); a.Reliability = r.F(); a.Leadership = r.F();
            a.WorkEthic = r.F(); a.Ambition = r.F(); a.Charm = r.F();
            a.Specialty = r.E<Specialty>();
            float wage = r.F();
            a.AskingWage = r.F();
            float morale = r.F();
            int hired = r.I();
            var contract = r.E<Contract>();
            a.NoNights = r.B(); a.NoMornings = r.B(); a.NoWeekends = r.B();

            var e = Staff.Restore(a, role, contract, assigned != null ? CategoryOf(assigned) : null, wage);
            e.Shift = shift;
            e.Morale = morale;
            e.HiredDay = hired;
            e.ShiftsThisWeek = r.I(); e.ShiftsWorked = r.I(); e.Overtime = r.B(); e.ShortRest = r.B();
            e.Rank = r.E<Rank>();
            var dept = Catalog.CategoryById(r.S() ?? "");
            e.Department = dept != null ? CategoryOf(dept) : null;
            e.OrdersForDepartment = r.B(); e.ProbationEnds = r.I(); e.NextReviewDay = r.I(); e.WarnedDay = r.I(); e.WrittenDay = r.I();
            e.PromotionWaitShifts = r.I(); e.PraisedDay = r.I(); e.LowMoraleShifts = r.I(); e.MoraleNote = r.S() ?? "";
            if (r.B()) workingAtSave.Add(e);
            for (int i = 0; i < 7; i++) e.Week[i] = r.E<ShiftSlot>();
            for (int t = 0; t < Applicant.TraitCount; t++) { e.Min[t] = r.F(); e.Max[t] = r.F(); }
            e.Record.Clear();
            e.RefreshColor();   // uniform for the saved role and rank
            return e;
        }

        static Applicant ReadApplicant(Reader r)
        {
            var a = new Applicant
            {
                Name = r.S(), Age = r.I(), YearsExperience = r.I(), LastJob = r.S(),
                Speed = r.F(), Service = r.F(), Reliability = r.F(), Leadership = r.F(), WorkEthic = r.F(), Ambition = r.F(), Charm = r.F(),
            };
            a.Specialty = r.E<Specialty>();
            a.AskingWage = r.F(); a.Days = r.I(); a.WantsPartTime = r.B(); a.WantsFullTime = r.B();
            a.NoNights = r.B(); a.NoMornings = r.B(); a.NoWeekends = r.B(); a.HasReferences = r.B();
            a.Interviewed = r.B(); a.ReferencesRequested = r.B(); a.ReferencesIn = r.B(); a.ReferencesDay = r.I(); a.DeclinedDay = r.I();
            for (int t = 0; t < Applicant.TraitCount; t++) { a.Min[t] = r.F(); a.Max[t] = r.F(); }
            int n = r.I();
            for (int i = 0; i < n; i++) a.Asked.Add(r.I());
            n = r.I();
            for (int i = 0; i < n; i++) a.Transcript.Add(r.S());
            return a;
        }
    }
}
