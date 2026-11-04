using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoreManager
{
    /// <summary>
    /// Building blocks for the "retail till" UI (styles in Resources/UI/Store.uss): till keys, receipt paper with
    /// torn edges, dashed rules, VFD display fields and text helpers.
    /// </summary>
    public static class Pos
    {
        public static readonly Color Paper = new Color32(0xf7, 0xf3, 0xe8, 0xff);

        public static Label L(string text, string cls = null)
        {
            var l = new Label(text);
            if (cls != null) foreach (var c in cls.Split(' ')) l.AddToClassList(c);
            return l;
        }

        public static VisualElement Box(string cls = null, VisualElement parent = null)
        {
            var v = new VisualElement();
            if (cls != null) foreach (var c in cls.Split(' ')) v.AddToClassList(c);
            if (parent != null) parent.Add(v);
            return v;
        }

        /// <summary>A till key. Keys never take keyboard focus, so Space/Enter stay game shortcuts.</summary>
        public static Button Key(string text, Action onClick, string mods = null)
        {
            var b = new Button(onClick) { text = text, focusable = false };
            b.AddToClassList("key");
            if (mods != null) foreach (var m in mods.Split(' ')) b.AddToClassList("key--" + m);
            return b;
        }

        public static Label Cap(string text) { return L(text, "cap"); }

        /// <summary>A strip of receipt paper: optional torn top/bottom edges around a body.</summary>
        public static VisualElement Receipt(out VisualElement body, bool tornTop, bool tornBottom)
        {
            var wrap = new VisualElement();
            if (tornTop) wrap.Add(Edge(true));
            body = Box("receipt", wrap);
            if (tornBottom) wrap.Add(Edge(false));
            return wrap;
        }

        public static Label Rule()
        {
            return L("- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", "receipt__rule");
        }

        static Texture2D down, up;

        static VisualElement Edge(bool top)
        {
            if (down == null) { down = Zigzag(false); up = Zigzag(true); }
            var e = Box("receipt__edge");
            e.style.backgroundImage = Background.FromTexture2D(top ? up : down);
            e.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.NoRepeat);
            e.style.backgroundSize = new BackgroundSize(new Length(16f), new Length(8f));
            return e;
        }

        /// <summary>Torn-paper teeth: paper colour, pointing down (bottom edge) or up (top edge).</summary>
        static Texture2D Zigzag(bool pointUp)
        {
            var t = new Texture2D(16, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 16; x++)
                {
                    float fromPaper = pointUp ? y : 7 - y;           // distance from the paper side
                    float tooth = 8f - Mathf.Abs(x + 0.5f - 8f);       // 0.5 at the edges, 8 in the middle
                    float a = Mathf.Clamp01(tooth - fromPaper);
                    t.SetPixel(x, y, new Color(Paper.r, Paper.g, Paper.b, a));
                }
            t.Apply();
            return t;
        }

        /// <summary>Sets text only when it changed (avoids relayout every frame).</summary>
        public static void Set(Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        public static void Show(VisualElement v, bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (v.style.display != d) v.style.display = d;
        }

        public static void Toggle(VisualElement v, string cls, bool on)
        {
            if (on != v.ClassListContains(cls)) v.EnableInClassList(cls, on);
        }

        /// <summary>
        /// The sim writes colours for a dark background (&lt;color=#ffcc55&gt;...). On paper we use ink instead:
        /// strips the tag and returns red, amber, blue or plain.
        /// </summary>
        public static string Ink(string s, out string ink)
        {
            ink = null;
            if (s.StartsWith("<color="))
            {
                int end = s.IndexOf('>');
                string col = end > 7 ? s.Substring(7, end - 7).ToLowerInvariant() : "";
                ink = col.StartsWith("#ff6") || col.StartsWith("#ff8") || col.StartsWith("#ff7") ? "red"
                    : col.StartsWith("#ffc") || col.StartsWith("#ffd") || col.StartsWith("#ff9") ? "amber"
                    : col.StartsWith("#9f") || col.StartsWith("#cf") ? "blue" : null;
                s = s.Substring(end + 1);
                if (s.EndsWith("</color>")) s = s.Substring(0, s.Length - 8);
            }
            return s;
        }

        /// <summary>Removes rich-text tags (for display fonts and world labels).</summary>
        public static string Plain(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '<') tag = true;
                else if (c == '>' && tag) tag = false;
                else if (!tag) sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool Light(Color c) { return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.7f; }
    }
}
