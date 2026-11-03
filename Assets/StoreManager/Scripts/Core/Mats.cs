using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StoreManager
{
    /// <summary>Grey-box helpers: cached coloured materials and primitive builders.</summary>
    public static class Mats
    {
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        static Shader shader;

        static Shader FindShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null)
                s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Unlit/Color");
            return s;
        }

        public static Material Get(Color c)
        {
            Material m;
            if (cache.TryGetValue(c, out m) && m != null) return m;
            if (shader == null) shader = FindShader();
            m = new Material(shader);
            m.color = c;
            if (Urp) m.SetColor("_BaseColor", c);
            cache[c] = m;
            return m;
        }

        static readonly Dictionary<string, Material> textured = new Dictionary<string, Material>();
        static readonly Dictionary<Color, Material> glows = new Dictionary<Color, Material>();

        static bool Urp { get { if (shader == null) shader = FindShader(); return shader != null && shader.name.StartsWith("Universal"); } }

        /// <summary>Tiled texture from Resources/Textures (1 texture = 1 m), tinted.</summary>
        public static Material Textured(string texture, Vector2 tiling, Color tint)
        {
            string key = texture + "|" + tiling.x + "x" + tiling.y + "|" + tint;
            Material m;
            if (textured.TryGetValue(key, out m) && m != null) return m;
            if (shader == null) shader = FindShader();
            var tex = Resources.Load<Texture2D>("Textures/" + texture);
            m = new Material(shader) { name = texture };
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling;
                if (Urp)
                {
                    m.SetTexture("_BaseMap", tex);
                    m.SetTextureScale("_BaseMap", tiling);
                }
            }
            m.color = tint;
            if (Urp)
            {
                m.SetColor("_BaseColor", tint);
                m.SetFloat("_Smoothness", texture.StartsWith("floor_store") ? 0.45f : 0.15f);
            }
            textured[key] = m;
            return m;
        }

        /// <summary>Self-lit colour for lights and indicators (picked up by bloom).</summary>
        public static Material Glow(Color c)
        {
            Material m;
            if (glows.TryGetValue(c, out m) && m != null) return m;
            if (shader == null) shader = FindShader();
            m = new Material(shader) { name = "Glow" };
            m.color = c;
            if (Urp) m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 2.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            glows[c] = m;
            return m;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = Get(color);
            if (!collider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            return go;
        }

        /// <summary>Axis-aligned box from a min corner (x,z on the ground) and a size.</summary>
        public static GameObject Box(string name, Transform parent, float x, float z, float w, float d, float y, float h, Color color, bool collider = false)
        {
            return Prim(PrimitiveType.Cube, name, parent,
                new Vector3(x + w * 0.5f, y + h * 0.5f, z + d * 0.5f),
                new Vector3(w, h, d), color, collider);
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }
}
