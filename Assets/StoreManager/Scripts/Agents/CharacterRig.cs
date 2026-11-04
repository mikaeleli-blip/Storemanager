using System.Collections.Generic;
using UnityEngine;

namespace StoreManager
{
    /// <summary>What a person carries around the store.</summary>
    public enum Hold { None, Basket, Trolley }

    /// <summary>What a person's arms are doing when they stand still.</summary>
    public enum Pose { Idle, Work, Reach, Scan, Phone, Sit }

    /// <summary>
    /// What a person looks like: colours and which C_ parts from Characters.fbx they wear.
    /// Parts are named C_Part__role; role picks the colour (skin, hair, top, bottom, shoes, accent, accent2),
    /// and "pal" parts keep the shared palette (eyes, buttons, badges, stripes).
    /// </summary>
    public class Outfit
    {
        public float Height = 1.75f, Width = 1f;
        public Color Skin, Hair, Top, Bottom, Shoes, Accent, Accent2;
        public Color? Sleeve;   // arms in another colour than the top (the manager's blazer)
        public string Torso = "TorsoTee", Arms = "ArmShort", Legs = "Leg", HairStyle = "HairShort";
        public readonly List<string> Wear = new List<string>();   // over the torso (skirt, apron, vest, blazer)
        public readonly List<string> Head = new List<string>();   // on the head (beard, glasses, hats)
        public bool Elderly, Cane;

        public Color ColorOf(string role, bool arm)
        {
            switch (role)
            {
                case "skin": return Skin;
                case "hair": return Hair;
                case "top": return arm && Sleeve.HasValue ? Sleeve.Value : Top;
                case "bottom": return Bottom;
                case "shoes": return Shoes;
                case "accent": return Accent;
                case "accent2": return Accent2;
                default: return Color.magenta;
            }
        }

        // ------------------------------------------------------------------ palettes (few colours = few materials)

        static Color C(int hex) { return new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f); }

        public static readonly Color[] SkinTones =
        {
            C(0xf5d6b8), C(0xe6bd99), C(0xc79a73), C(0x9a6e4d), C(0x6b4a33), C(0xfae0cc)
        };
        static readonly Color[] HairColors = { C(0x2a2420), C(0x3b2a20), C(0x5a3a24), C(0x8a5a32), C(0xd9b56a), C(0xa4492a) };
        static readonly Color[] GreyHair = { C(0xb9b6b0), C(0xe8e6e0), C(0x8e8b86) };
        static readonly Color[] Tops =
        {
            C(0x3f6fb5), C(0xd97a8a), C(0x5b7a3a), C(0xf0c23c), C(0x7a3b6e), C(0x2a9d8f), C(0x9a3b3b), C(0x2f3a4f),
            C(0xe8e4dc), C(0x4a4d55), C(0xc8743a), C(0x6a8fc7), C(0x8e6bb8), C(0x3a6b5a), C(0xb8b2a6), C(0xd4504a)
        };
        static readonly Color[] Coats = { C(0x8a7a62), C(0x3a3e47), C(0x5b4a3a), C(0x2f4f6f), C(0x6b2f35), C(0x4a5a3a) };
        static readonly Color[] Inner = { C(0xe9e4d6), C(0xd9c39a), C(0x8e9096), C(0xf4f1ea) };
        static readonly Color[] Bottoms = { C(0x2c3e5c), C(0x39424f), C(0x25272c), C(0x4a4d55), C(0xc9b48a), C(0x6b5b4b), C(0x2f4f7a), C(0x3b3f47) };
        static readonly Color[] Skirts = { C(0x39424f), C(0x25272c), C(0x6b2f35), C(0x2f4f7a), C(0x5b4a3a) };
        static readonly Color[] ShoeColors = { C(0xe8e4dc), C(0x25272c), C(0x7a4f2c), C(0x4a3a2a), C(0x8e9096) };

        static readonly Color StaffTrousers = C(0x2c3038), StaffShoes = C(0x25272c), White = C(0xf4f1ea);

        static T Pick<T>(System.Random r, T[] a) { return a[r.Next(a.Length)]; }
        static float Range(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        static bool Chance(System.Random r, float p) { return r.NextDouble() < p; }

        /// <summary>Stable number from a name, so a staff member always looks the same.</summary>
        public static int Seed(string s)
        {
            unchecked
            {
                int h = (int)2166136261;
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619;
                return h & 0x7fffffff;
            }
        }

        /// <summary>Face, hair and build; shared by customers and staff.</summary>
        static void Person(Outfit o, System.Random r, bool female, int age)
        {
            o.Skin = Pick(r, SkinTones);
            o.Hair = age == 2 ? Pick(r, GreyHair) : Pick(r, HairColors);
            if (age == 0) { o.Height = female ? Range(r, 1.5f, 1.66f) : Range(r, 1.52f, 1.72f); o.Width = Range(r, 0.85f, 1.0f); }
            else
            {
                o.Height = female ? Range(r, 1.58f, 1.76f) : Range(r, 1.68f, 1.92f);
                if (age == 2) o.Height -= 0.05f;
                float b = (float)r.NextDouble();
                o.Width = 0.9f + b * b * (age == 2 ? 0.4f : 0.45f);
            }
            float h = (float)r.NextDouble();
            if (female)
                o.HairStyle = h < 0.32f ? "HairLong" : h < 0.52f ? "HairPony" : h < 0.7f ? "HairBun" : h < 0.86f ? "HairShort" : "HairCurly";
            else if (age == 2)
                o.HairStyle = h < 0.45f ? "HairBald" : h < 0.8f ? "HairShort" : "HairBuzz";
            else
                o.HairStyle = h < 0.42f ? "HairShort" : h < 0.62f ? "HairBuzz" : h < 0.76f ? "HairCurly" : h < 0.86f && age > 0 ? "HairBald" : h < 0.94f ? "HairLong" : "HairShort";
            if (!female && age > 0 && Chance(r, age == 2 ? 0.2f : 0.25f)) o.Head.Add("Beard");
            if (Chance(r, age == 2 ? 0.5f : age == 0 ? 0.08f : 0.15f)) o.Head.Add("Glasses");
        }

        /// <summary>A random shopper: teens, adults and older people, all builds.</summary>
        public static Outfit Customer(bool female)
        {
            var r = new System.Random(Random.Range(0, int.MaxValue));
            var o = new Outfit();
            float a = (float)r.NextDouble();
            int age = a < 0.14f ? 0 : a < 0.84f ? 1 : 2;
            Person(o, r, female, age);
            o.Elderly = age == 2;
            o.Cane = o.Elderly && Chance(r, 0.55f);
            o.Shoes = Pick(r, ShoeColors);
            o.Bottom = Pick(r, Bottoms);
            o.Accent = Pick(r, Inner);
            float t = (float)r.NextDouble();
            if (age == 2) o.Torso = t < 0.5f ? "TorsoCoat" : t < 0.8f ? "TorsoSweater" : "TorsoJacket";
            else if (age == 0) o.Torso = t < 0.45f ? "TorsoHoodie" : t < 0.9f ? "TorsoTee" : "TorsoJacket";
            else o.Torso = t < 0.25f ? "TorsoTee" : t < 0.47f ? "TorsoSweater" : t < 0.65f ? "TorsoHoodie" : t < 0.9f ? "TorsoJacket" : "TorsoCoat";
            o.Top = o.Torso == "TorsoCoat" || o.Torso == "TorsoJacket" ? (Chance(r, 0.6f) ? Pick(r, Coats) : Pick(r, Tops)) : Pick(r, Tops);
            o.Arms = o.Torso == "TorsoTee" ? "ArmShort" : "ArmLong";
            if (female && o.Torso != "TorsoCoat" && Chance(r, 0.25f))
            {
                o.Wear.Add("Skirt");
                o.Bottom = Pick(r, Skirts);
                o.Legs = "LegBare";
            }
            return o;
        }

        /// <summary>
        /// The store uniform for a role, on a person who always looks the same (seeded by their name).
        /// Shift leads and department managers also wear a lanyard.
        /// </summary>
        public static Outfit Staff(Role role, bool female, int seed, bool lead)
        {
            var r = new System.Random(seed);
            var o = new Outfit();
            Person(o, r, female, 1);
            o.Width = Mathf.Min(o.Width, 1.2f);
            o.Bottom = StaffTrousers;
            o.Shoes = StaffShoes;
            o.Torso = "TorsoPolo";
            o.Arms = "ArmShort";
            switch (role)
            {
                case Role.Cashier:
                    o.Top = Employee.RoleColor(role);
                    break;
                case Role.Stocker:
                    o.Top = C(0x8e9096);
                    o.Accent = Employee.RoleColor(role);
                    o.Wear.Add("Vest");
                    o.Wear.Add("VestStripes");
                    break;
                case Role.Cleaner:
                    o.Top = Employee.RoleColor(role);
                    break;
                case Role.Baker:
                    o.Top = Employee.RoleColor(role);
                    o.Accent = C(0xdcd6ca);
                    o.Wear.Add("Apron");
                    o.Head.Remove("Glasses");
                    o.Head.Add("HatBaker");
                    o.HairStyle = "HairBald";
                    break;
                case Role.DeliClerk:
                    o.Top = White;
                    o.Accent = C(0x7a1f2b);
                    o.Wear.Add("Apron");
                    o.Wear.Add("ApronStripes");
                    o.Head.Add("Cap");
                    o.HairStyle = "HairBald";
                    break;
                default:   // assistant manager: shirt, tie in the role colour and a lanyard
                    o.Torso = "TorsoShirt";
                    o.Arms = "ArmLong";
                    o.Top = White;
                    o.Accent2 = Employee.RoleColor(role);
                    o.Wear.Add("Lanyard");
                    break;
            }
            if (lead && !o.Wear.Contains("Lanyard")) o.Wear.Add("Lanyard");
            return o;
        }

        /// <summary>You: shirt, red tie and a charcoal blazer.</summary>
        public static Outfit Manager()
        {
            var o = new Outfit
            {
                Height = 1.78f, Skin = SkinTones[1], Hair = HairColors[2], HairStyle = "HairShort",
                Torso = "TorsoShirt", Arms = "ArmLong", Top = White, Accent = C(0x3a3e47), Accent2 = C(0xb8322a),
                Bottom = C(0x33363e), Shoes = C(0x2a1d16)
            };
            o.Sleeve = o.Accent;
            o.Wear.Add("Blazer");
            return o;
        }
    }

    /// <summary>
    /// Builds a person from the C_ parts and animates them procedurally: walking (legs, arms, bob),
    /// carrying a basket or pushing a trolley, and standing poses (stocking, scanning, phone, sitting).
    /// Joint layout (1.76 m tall before scaling): hips 0.82 m up; torso 0.6 m; shoulders at +-0.27 * width.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        const float HeadTop = 1.76f, HipY = 0.82f, ShoulderX = 0.27f, ShoulderY = 0.56f, HipX = 0.095f;
        const float HandleY = 0.99f;   // trolley handle above the floor

        public static bool Available { get { return ModelLibrary.Has("C_Head__skin"); } }

        Agent agent;
        Outfit outfit;
        Hold hold;
        Transform root, hips, spine, body, neck, shoulderL, shoulderR, legL, legR;
        Transform basket, trolley, bag, cane, phone;
        readonly List<GameObject> items = new List<GameObject>();
        float scale = 1f, hunch, phase, walk, pose, t, trolleyAngle, trolleyReach, lookSeed;
        Pose shownPose = Pose.Idle;
        Vector3 lastPos;
        bool packed;

        public Hold Holding { get { return hold; } }
        public Outfit Look { get { return outfit; } }

        public static CharacterRig Build(Agent a, Outfit o, Hold hold)
        {
            var go = new GameObject("Rig");
            go.transform.SetParent(a.transform, false);
            var rig = go.AddComponent<CharacterRig>();
            rig.agent = a;
            rig.lookSeed = Random.value * 10f;
            rig.Assemble(o, hold);
            rig.lastPos = a.transform.position;

            // Click target for selecting people.
            var col = a.gameObject.GetComponent<CapsuleCollider>();
            if (col == null) col = a.gameObject.AddComponent<CapsuleCollider>();
            col.height = o.Height;
            col.radius = 0.32f;
            col.center = new Vector3(0f, o.Height * 0.5f, 0f);
            return rig;
        }

        /// <summary>Change clothes (new role, promotion) or what they carry.</summary>
        public void Rebuild(Outfit o, Hold h)
        {
            if (root != null) Destroy(root.gameObject);
            if (trolley != null) Destroy(trolley.gameObject);
            items.Clear();
            basket = trolley = bag = cane = phone = null;
            packed = false;
            Assemble(o, h);
        }

        static Transform Node(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            return go.transform;
        }

        void Wear(string part, Transform at, bool arm = false)
        {
            var list = ModelLibrary.CharacterPart(part);
            if (list == null) return;
            // A lead's lanyard goes over the vest or apron.
            var pos = part == "Lanyard" && (outfit.Wear.Contains("Vest") || outfit.Wear.Contains("Apron"))
                ? new Vector3(0f, 0f, 0.018f) : Vector3.zero;
            for (int i = 0; i < list.Count; i++)
            {
                var go = ModelLibrary.Spawn(list[i].Key, at, pos);
                if (go == null) continue;
                if (list[i].Value != "pal") go.GetComponent<Renderer>().sharedMaterial = Mats.Get(outfit.ColorOf(list[i].Value, arm));
            }
        }

        Transform Carry(string part, Transform at, Vector3 pos)
        {
            var n = Node(part, at, pos);
            Wear(part, n);
            return n;
        }

        void Assemble(Outfit o, Hold h)
        {
            outfit = o;
            hold = h;
            scale = o.Height / HeadTop;
            hunch = o.Elderly ? 11f : 0f;
            float w = o.Width;

            root = Node("Body", transform, Vector3.zero);
            root.localScale = Vector3.one * scale;
            hips = Node("Hips", root, new Vector3(0f, HipY, 0f));
            spine = Node("Spine", hips, Vector3.zero);
            body = Node("Torso", spine, Vector3.zero);
            body.localScale = new Vector3(w, 1f, 0.6f + 0.4f * w);
            Wear(o.Torso, body);
            for (int i = 0; i < o.Wear.Count; i++) Wear(o.Wear[i], body);

            neck = Node("Neck", spine, new Vector3(0f, 0.6f, 0f));
            Wear("Head", neck);
            Wear(o.HairStyle, neck);
            for (int i = 0; i < o.Head.Count; i++) Wear(o.Head[i], neck);

            shoulderL = Node("ArmL", spine, new Vector3(-ShoulderX * w, ShoulderY, 0f));
            shoulderR = Node("ArmR", spine, new Vector3(ShoulderX * w, ShoulderY, 0f));
            Wear(o.Arms, shoulderL, true);
            Wear(o.Arms, shoulderR, true);

            float lw = 0.8f + 0.2f * w;
            legL = Node("LegL", hips, new Vector3(-HipX * w, 0f, 0f));
            legR = Node("LegR", hips, new Vector3(HipX * w, 0f, 0f));
            legL.localScale = legR.localScale = new Vector3(lw, 1f, lw);
            Wear(o.Legs, legL);
            Wear(o.Legs, legR);

            if (h == Hold.Basket) basket = Carry("Basket", shoulderL, new Vector3(0f, -0.55f, 0f));
            if (h == Hold.Trolley)
            {
                // Arms reach the handle whatever the person's height; the trolley itself doesn't scale.
                float sh = scale * (HipY + ShoulderY);
                float c = Mathf.Clamp01((sh - HandleY) / (0.55f * scale));
                trolleyAngle = Mathf.Acos(c) * Mathf.Rad2Deg;
                trolleyReach = 0.55f * scale * Mathf.Sin(trolleyAngle * Mathf.Deg2Rad);
                trolley = Carry("Trolley", transform, new Vector3(0f, HandleY, trolleyReach + 0.06f));
            }
            if (o.Cane && h != Hold.Trolley) cane = Carry("Cane", shoulderR, new Vector3(0f, -0.55f, 0f));
            phone = Carry("Phone", shoulderR, new Vector3(0f, -0.5f, 0.05f));
            phone.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ shopping

        /// <summary>Drops a picked product into the basket or trolley (only a handful show).</summary>
        public void AddItem(ProductDef def)
        {
            if (def == null || packed) return;
            Transform at = hold == Hold.Basket ? basket : hold == Hold.Trolley ? trolley : null;
            if (at == null) return;
            string mesh = "Item_" + def.Name.Replace(' ', '_');
            if (!ModelLibrary.Has(mesh)) return;
            int n = items.Count;
            Vector3 pos;
            float s;
            if (hold == Hold.Basket)
            {
                if (n >= 8) return;
                int layer = n / 4, k = n % 4;
                pos = new Vector3((k % 2 == 0 ? -0.055f : 0.055f), -0.235f + layer * 0.07f, (k < 2 ? -0.09f : 0.09f));
                s = 0.8f;
            }
            else
            {
                if (n >= 18) return;
                int layer = n / 9, k = n % 9;
                pos = new Vector3(-0.15f + (k % 3) * 0.15f, -0.545f + layer * 0.12f, 0.17f + (k / 3) * 0.2f);
                s = 0.9f;
            }
            var go = ModelLibrary.Spawn(mesh, at, pos, Random.Range(-30f, 30f) + (n % 2) * 90f);
            if (go == null) return;
            go.transform.localScale = go.transform.localScale * s;
            items.Add(go);
        }

        public void ClearItems()
        {
            for (int i = 0; i < items.Count; i++) if (items[i] != null) Destroy(items[i]);
            items.Clear();
        }

        /// <summary>Paid: the basket stays at the till and the shopping goes home in a paper bag.</summary>
        public void PackBags()
        {
            if (packed) return;
            packed = true;
            ClearItems();
            if (hold == Hold.Basket && basket != null)
            {
                Destroy(basket.gameObject);
                basket = null;
                hold = Hold.None;
                if (cane == null) bag = Carry("Bag", shoulderR, new Vector3(0f, -0.55f, 0f));
                else bag = Carry("Bag", shoulderL, new Vector3(0f, -0.55f, 0f));
            }
            else if (hold == Hold.Trolley && trolley != null)
            {
                Carry("Bag", trolley, new Vector3(-0.11f, -0.17f, 0.3f)).localRotation = Quaternion.Euler(0f, 80f, 0f);
                Carry("Bag", trolley, new Vector3(0.12f, -0.17f, 0.52f)).localRotation = Quaternion.Euler(0f, -10f, 0f);
            }
        }

        // ------------------------------------------------------------------ animation

        void LateUpdate()
        {
            if (root == null || agent == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            t += dt;

            Vector3 p = agent.transform.position;
            Vector3 d = p - lastPos;
            d.y = 0f;
            lastPos = p;
            float speed = d.magnitude / dt;
            if (speed > 12f) speed = 0f;   // teleported
            walk = Mathf.MoveTowards(walk, Mathf.Clamp01(speed / 1.5f), dt * 5f);
            phase += speed * dt / (1.1f * scale) * Mathf.PI * 2f;
            float s = Mathf.Sin(phase);

            Pose want = agent.CurrentPose;
            if (want != shownPose && pose < 0.05f) shownPose = want;
            float target = want == shownPose && want != Pose.Idle && walk < 0.3f ? 1f : 0f;
            pose = Mathf.MoveTowards(pose, target, dt * 4f);
            if (phone != null) phone.gameObject.SetActive(shownPose == Pose.Phone && pose > 0.5f);

            // Legs and hips
            float legAmp = (outfit.Elderly ? 20f : 30f) * walk;
            float bob = (Mathf.Abs(Mathf.Cos(phase)) - 0.6f) * 0.035f * walk;
            float hipY = HipY + bob;
            float lean = hunch + 3f * walk;
            Quaternion lL = Quaternion.Euler(s * legAmp, 0f, 0f), lR = Quaternion.Euler(-s * legAmp, 0f, 0f);
            if (shownPose == Pose.Sit && pose > 0f)
            {
                hipY = Mathf.Lerp(hipY, 0.5f / scale, pose);
                lL = Quaternion.Slerp(lL, Quaternion.Euler(-85f, 0f, 0f), pose);
                lR = Quaternion.Slerp(lR, Quaternion.Euler(-85f, 0f, 0f), pose);
            }
            legL.localRotation = lL;
            legR.localRotation = lR;
            hips.localPosition = new Vector3(0f, hipY, 0f);
            hips.localRotation = Quaternion.Euler(0f, s * 4f * walk, 0f);

            // Arms: swing opposite the legs, then whatever they're carrying or doing.
            float armAmp = (outfit.Elderly ? 14f : 24f) * walk;
            Quaternion aL = Quaternion.Euler(-s * armAmp, 0f, -5f);
            Quaternion aR = Quaternion.Euler(s * armAmp, 0f, 5f);
            if (hold == Hold.Basket) aL = Quaternion.Euler(-6f - s * 5f * walk, 0f, -15f);
            if (hold == Hold.Trolley)
            {
                aL = Quaternion.Euler(-trolleyAngle, 0f, 6f);
                aR = Quaternion.Euler(-trolleyAngle, 0f, -6f);
            }
            if (cane != null) aR = Quaternion.Euler(-14f + s * 10f * walk, 0f, 6f);
            if (bag != null && bag.parent == shoulderR) aR = Quaternion.Euler(s * armAmp * 0.4f, 0f, 9f);
            if (bag != null && bag.parent == shoulderL) aL = Quaternion.Euler(-s * armAmp * 0.4f, 0f, -9f);

            float headX = -hunch * 0.7f, headY = 0f;
            if (pose > 0f)
            {
                Quaternion pL = aL, pR = aR;
                float w = Mathf.Sin(t * 5f);
                switch (shownPose)
                {
                    case Pose.Work:
                        pL = Quaternion.Euler(-55f + w * 14f, 0f, 4f);
                        pR = Quaternion.Euler(-55f - w * 14f, 0f, -4f);
                        lean += 6f * pose;
                        break;
                    case Pose.Scan:
                        pL = Quaternion.Euler(-40f, 0f, 6f);
                        pR = Quaternion.Euler(-42f, 0f, -6f + Mathf.Sin(t * 3.5f) * 16f);
                        headX += 12f * pose;
                        break;
                    case Pose.Reach:
                        if (hold != Hold.Basket) pL = Quaternion.Euler(-70f + Mathf.Sin(t * 2.6f) * 10f, 0f, 2f);
                        else if (cane == null) pR = Quaternion.Euler(-72f + Mathf.Sin(t * 2.6f) * 10f, 0f, -2f);
                        headX -= 6f * pose;
                        break;
                    case Pose.Phone:
                        pR = Quaternion.Euler(-68f, 0f, -16f);
                        pL = Quaternion.Euler(0f, 0f, -4f);
                        headX += 20f * pose;
                        break;
                    case Pose.Sit:
                        pL = Quaternion.Euler(-48f + w * 4f, 0f, 6f);
                        pR = Quaternion.Euler(-48f - w * 4f, 0f, -6f);
                        break;
                }
                if (hold == Hold.Trolley && shownPose != Pose.Reach) { pL = aL; pR = aR; }
                aL = Quaternion.Slerp(aL, pL, pose);
                aR = Quaternion.Slerp(aR, pR, pose);
            }
            else if (walk < 0.2f)
            {
                headY = Mathf.Sin(t * 0.35f + lookSeed) * 14f;   // looking around
            }
            shoulderL.localRotation = aL;
            shoulderR.localRotation = aR;
            spine.localRotation = Quaternion.Euler(lean, -s * 3f * walk, 0f);
            neck.localRotation = Quaternion.Euler(headX, headY, 0f);
            body.localScale = new Vector3(outfit.Width, 1f + Mathf.Sin(t * 2.1f + lookSeed) * 0.008f, 0.6f + 0.4f * outfit.Width);

            // Carried things hang straight down from the hand.
            if (basket != null) basket.localRotation = Quaternion.Inverse(aL);
            if (bag != null) bag.localRotation = Quaternion.Inverse(bag.parent == shoulderL ? aL : aR);
            if (cane != null) cane.localRotation = Quaternion.Inverse(aR) * Quaternion.Euler(-lean, 0f, 0f);
            if (phone != null) phone.localRotation = Quaternion.Inverse(aR) * Quaternion.Euler(32f, 0f, 0f);
            if (trolley != null)
                trolley.localPosition = new Vector3(0f, HandleY + bob * 0.3f, trolleyReach + 0.06f);
        }
    }
}
