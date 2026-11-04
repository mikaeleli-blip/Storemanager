using UnityEngine;
using UnityEngine.UIElements;

namespace StoreManager
{
    /// <summary>The new-game choice, printed as the till's first receipt.</summary>
    public class StartScreen
    {
        readonly VisualElement layer;
        readonly Label date, cash;

        public StartScreen(VisualElement root)
        {
            layer = Pos.Box("layer start", root);

            var printer = Pos.Box("start__printer", layer);
            Pos.Box("start__slot", printer);

            VisualElement body;
            var paper = Pos.Receipt(out body, false, true);
            paper.AddToClassList("start__paper");
            layer.Add(paper);

            body.Add(Pos.L("STOREMANAGER", "start__logo"));
            body.Add(Pos.L("POS TERMINAL 01  ·  CORNER SHOP", "receipt__sub"));
            date = Pos.L("", "receipt__sub");
            body.Add(date);
            body.Add(Pos.Rule());
            body.Add(Pos.L("WELCOME, MANAGER.", "receipt__total"));
            body.Add(Pos.L("A small, empty corner shop. You run it: you never stock a shelf or work a till yourself. "
                + "How do you want to start?", "receipt__line"));
            body.Add(Pos.Rule());

            body.Add(Option("HIRE YOUR OWN TEAM", "0 STAFF",
                "Start with nobody. Interview and hire every employee, set up the roster, build and stock the shop, "
                + "then open the doors when you're ready. Rent starts the day you open.",
                () => Store.I.ChooseStart(true)));
            body.Add(Option("START WITH A SMALL CREW", "5 STAFF",
                "Five people already work here: two full-time cashiers, a weekday stocker and two weekend part-timers. "
                + "The store is open from day one.",
                () => Store.I.ChooseStart(false)));

            body.Add(Pos.Rule());
            cash = Pos.L("", "receipt__total");
            body.Add(cash);
            var thanks = Pos.L("*** THANK YOU FOR SHOPPING ***", "receipt__sub");
            thanks.style.marginTop = 6f;
            body.Add(thanks);
            body.Add(Pos.L("*STOREMANAGER*", "barcode"));
        }

        static VisualElement Option(string head, string price, string text, System.Action pick)
        {
            var b = new Button(pick) { focusable = false };
            b.ClearClassList();
            b.AddToClassList("option");
            var top = Pos.Box("row", b);
            var h = Pos.L(head, "option__head");
            h.style.flexGrow = 1f;
            top.Add(h);
            top.Add(Pos.L(price, "option__price"));
            b.Add(Pos.L(text, "option__text"));
            return b;
        }

        public void Refresh(Store store, bool visible)
        {
            Pos.Show(layer, visible);
            if (!visible) return;
            var clk = store.Clock;
            Pos.Set(date, "DAY " + clk.Day + "  " + clk.Weekday.ToUpperInvariant() + " " + clk.TimeText + "      TRANS #0001");
            Pos.Set(cash, "CASH IN THE BANK".PadRight(30) + Economy.Money(store.Economy.Cash));
        }
    }
}
