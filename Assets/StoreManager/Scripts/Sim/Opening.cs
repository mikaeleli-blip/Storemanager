using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>
    /// Opening week of a small shop: a hand-painted banner, a few balloons, curious neighbours, first impressions
    /// that count double, and a short piece in the local paper the morning after.
    /// </summary>
    public partial class Store
    {
        GameObject openingDecor;
        public const int OpeningDays = 3;

        /// <summary>Extra customers in the first days: people come to have a look.</summary>
        public float OpeningBoost
        {
            get
            {
                if (!Opened) return 1f;
                int d = Clock.Day - OpenedDay;
                return d == 0 ? 1.4f : d == 1 ? 1.2f : d == 2 ? 1.1f : 1f;
            }
        }

        public bool InOpeningWeek { get { return Opened && Clock.Day - OpenedDay < OpeningDays; } }

        /// <summary>Where the banner hangs (for the "NOW OPEN" text).</summary>
        public Vector3 BannerPoint { get { return new Vector3(4f, 2.45f, 2.75f); } }

        void PutUpOpeningDecor()
        {
            if (openingDecor != null) return;
            openingDecor = new GameObject("Opening decorations");
            openingDecor.transform.SetParent(transform, false);
            var r = openingDecor.transform;
            var wood = Mats.Hex("#7a5232");
            // two poles and a cloth banner across the entrance
            Mats.Box("Banner pole L", r, 1.75f, 2.65f, 0.08f, 0.08f, 0f, 2.6f, wood);
            Mats.Box("Banner pole R", r, 6.17f, 2.65f, 0.08f, 0.08f, 0f, 2.6f, wood);
            Mats.Box("Banner", r, 1.83f, 2.68f, 4.34f, 0.03f, 2.15f, 0.55f, Mats.Hex("#f4ead2"));
            Mats.Box("Banner stripe", r, 1.83f, 2.665f, 4.34f, 0.02f, 2.15f, 0.07f, Mats.Hex("#c8433a"));
            Mats.Box("Banner stripe 2", r, 1.83f, 2.665f, 4.34f, 0.02f, 2.63f, 0.07f, Mats.Hex("#c8433a"));
            // a few balloons tied to the poles
            Color[] cols = { Mats.Hex("#e8483d"), Mats.Hex("#f2c230"), Mats.Hex("#3d8de8"), Mats.Hex("#5cc46a"), Mats.Hex("#e86fb0") };
            Vector3[] spots =
            {
                new Vector3(1.55f, 2.95f, 2.6f), new Vector3(1.9f, 3.15f, 2.75f), new Vector3(1.7f, 3.35f, 2.55f),
                new Vector3(6.45f, 3.0f, 2.6f), new Vector3(6.1f, 3.25f, 2.75f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = spots[i];
                Mats.Prim(PrimitiveType.Sphere, "Balloon", r, p, new Vector3(0.32f, 0.38f, 0.32f), cols[i % cols.Length], false);
                float poleX = p.x < 4f ? 1.79f : 6.21f;
                var top = new Vector3(poleX, 2.4f, 2.69f);
                var mid = (p + top) * 0.5f;
                var line = Mats.Prim(PrimitiveType.Cylinder, "String", r, mid, new Vector3(0.012f, (p - top).magnitude * 0.5f, 0.012f), Color.white, false);
                line.transform.rotation = Quaternion.FromToRotation(Vector3.up, (p - top).normalized);
            }
            // chalkboard sign on the pavement
            Mats.Box("Sign board", r, 7.0f, 1.9f, 0.7f, 0.06f, 0.15f, 0.95f, Mats.Hex("#2b3530"));
            Mats.Box("Sign frame", r, 6.97f, 1.88f, 0.76f, 0.04f, 0.1f, 1.05f, wood);
            Mats.Box("Sign leg", r, 7.3f, 2.1f, 0.06f, 0.06f, 0f, 0.95f, wood);
        }

        void TakeDownOpeningDecor()
        {
            if (openingDecor != null) Destroy(openingDecor);
            openingDecor = null;
        }

        /// <summary>The morning after opening day: what the local paper wrote.</summary>
        void LocalPaper(DayLedger d)
        {
            float sat = d.AvgSatisfaction;
            string verdict = d.Customers == 0 ? "opened its doors yesterday, though hardly anyone noticed"
                : sat >= 72f ? "opened yesterday and it already feels like part of the neighbourhood"
                : sat >= 58f ? "opened yesterday to a steady trickle of curious locals"
                : sat >= 45f ? "opened yesterday with a few teething problems"
                : "had a rocky first day yesterday";
            string top = null;
            float most = 0f;
            foreach (var kv in d.SatisfactionLost) if (kv.Value > most) { most = kv.Value; top = kv.Key; }
            string but = "";
            if (top != null && d.Customers > 0 && most / d.Customers > 2f)
            {
                switch (top)
                {
                    case "Empty shelves": but = " Some shelves were bare by the afternoon."; break;
                    case "Checkout queues": case "No open checkout": case "Walked out": but = " The queue at the till tested a few people's patience."; break;
                    case "Things you don't sell": case "Found nothing to buy": but = " Shoppers hope the range will grow."; break;
                    case "Prices": but = " A few found the prices on the steep side."; break;
                    case "Dirty floors": but = " The floor could have done with a mop."; break;
                    case "Expired products": but = " One reader found a carton past its date."; break;
                    case "Unfriendly cashiers": but = " The welcome at the till could be warmer."; break;
                }
            }
            string praise = sat >= 58f && d.Customers > 0 ? " \"Handy to have it so close,\" said one customer." : "";
            Message("The local paper: \"The new corner shop " + verdict + ". About " + (d.Customers / 10 * 10) + " people came through the door"
                + (d.ItemsSold > 0 ? " and bought " + d.ItemsSold + " items" : "") + "." + but + praise + "\"");
        }
    }
}
