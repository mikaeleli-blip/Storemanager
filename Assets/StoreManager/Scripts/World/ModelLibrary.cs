using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StoreManager
{
    /// <summary>
    /// The Blender-made models (Resources/Models/*.fbx). Each FBX holds many named meshes;
    /// the game spawns them by name with one shared palette material (+ glass).
    /// Falls back to grey-box primitives when the models aren't there (e.g. headless tests).
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, List<KeyValuePair<string, string>>> characterParts =
            new Dictionary<string, List<KeyValuePair<string, string>>>();
        static bool loaded;
        static Material palette, glass;
        static Quaternion fixRotation = Quaternion.identity;
        static Vector3 fixScale = Vector3.one;

        public static bool Available { get { Load(); return meshes.Count > 0; } }
        public static Material Palette { get { Load(); return palette; } }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            string[] files = { "Models/Fixtures", "Models/Products", "Models/Decor", "Models/Characters" };
            for (int f = 0; f < files.Length; f++)
            {
                var root = Resources.Load<GameObject>(files[f]);
                if (root == null) continue;
                var filters = root.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                    if (filters[i].sharedMesh != null) meshes[filters[i].gameObject.name] = filters[i].sharedMesh;
            }
            if (meshes.Count == 0) return;
            IndexCharacterParts();
            BuildMaterials();
            Calibrate();
        }

        static Shader LitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static void BuildMaterials()
        {
            var shader = LitShader();
            bool urp = shader != null && shader.name.StartsWith("Universal");
            var tex = Resources.Load<Texture2D>("Models/palette");
            var emi = Resources.Load<Texture2D>("Models/palette_e");
            if (tex != null) tex.filterMode = FilterMode.Point;
            if (emi != null) emi.filterMode = FilterMode.Point;

            palette = new Material(shader) { name = "Store Palette", enableInstancing = true };
            if (urp)
            {
                palette.SetTexture("_BaseMap", tex);
                palette.SetColor("_BaseColor", Color.white);
                palette.SetFloat("_Smoothness", 0.18f);
                palette.SetFloat("_Metallic", 0f);
            }
            palette.mainTexture = tex;
            palette.color = Color.white;
            if (emi != null)
            {
                palette.SetTexture("_EmissionMap", emi);
                palette.SetColor("_EmissionColor", Color.white * 1.8f);
                palette.EnableKeyword("_EMISSION");
                palette.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            glass = Transparent(new Color(0.78f, 0.92f, 1f, 0.22f), 0.9f);
            glass.name = "Store Glass";
        }

        /// <summary>A see-through material (glass, build-mode ghosts).</summary>
        public static Material Transparent(Color c, float smoothness = 0.5f)
        {
            var shader = LitShader();
            bool urp = shader != null && shader.name.StartsWith("Universal");
            var m = new Material(shader) { name = "Transparent" };
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Smoothness", smoothness);
                m.SetColor("_BaseColor", c);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            m.color = c;
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>
        /// Safety net for import settings: the gondola is 0.92 x 1.86 x 1.0 m with y up, and the checkout
        /// sticks out further on +X (impulse rack). Fix scale, up axis and mirroring if they come in wrong.
        /// </summary>
        static void Calibrate()
        {
            Mesh g;
            if (!meshes.TryGetValue("Gondola", out g)) return;
            Vector3 size = g.bounds.size;
            float tallest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float s = tallest > 0.001f ? 1.86f / tallest : 1f;
            if (Mathf.Abs(s - 1f) > 0.05f) fixScale = Vector3.one * s;
            if (size.z > size.y * 1.2f && size.z >= size.x)
            {
                bool baseAtMin = g.bounds.center.z > 0f;
                fixRotation = Quaternion.Euler(baseAtMin ? -90f : 90f, 0f, 0f);
                Debug.LogWarning("[Storemanager] Models came in Z-up; rotating them. Check the FBX import settings.");
            }
            Mesh c;
            if (meshes.TryGetValue("Checkout", out c))
            {
                Vector3 center = fixRotation * c.bounds.center;
                if (center.x < 0f)
                {
                    fixScale.x = -fixScale.x;
                    Debug.LogWarning("[Storemanager] Models came in mirrored on X; flipping them.");
                }
            }
            if (fixScale != Vector3.one) Debug.LogWarning("[Storemanager] Model scale corrected by " + fixScale);
        }

        /// <summary>People are built from C_Part__role meshes; this groups them by part.</summary>
        static void IndexCharacterParts()
        {
            foreach (var name in meshes.Keys)
            {
                if (!name.StartsWith("C_")) continue;
                int cut = name.IndexOf("__");
                if (cut < 0) continue;
                string part = name.Substring(2, cut - 2), role = name.Substring(cut + 2);
                List<KeyValuePair<string, string>> list;
                if (!characterParts.TryGetValue(part, out list)) characterParts[part] = list = new List<KeyValuePair<string, string>>();
                list.Add(new KeyValuePair<string, string>(name, role));
            }
        }

        /// <summary>The meshes (name, colour role) that make up one character part, or null.</summary>
        public static List<KeyValuePair<string, string>> CharacterPart(string part)
        {
            Load();
            List<KeyValuePair<string, string>> list;
            return characterParts.TryGetValue(part, out list) ? list : null;
        }

        public static bool Has(string name)
        {
            Load();
            return meshes.ContainsKey(name);
        }

        /// <summary>Spawns a model at a local position with Unity Euler(0, yaw, tilt). Null if the model is missing.</summary>
        public static GameObject Spawn(string name, Transform parent, Vector3 position, float yaw = 0f, float tilt = 0f)
        {
            Load();
            Mesh mesh;
            if (!meshes.TryGetValue(name, out mesh)) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, tilt) * fixRotation;
            go.transform.localScale = fixScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mesh.subMeshCount > 1 ? new[] { palette, glass } : new[] { palette };
            return go;
        }

        public static string StockMesh(ProductDef p, bool counter)
        {
            return (counter ? "Deli_" : "Stock_") + p.Name.Replace(' ', '_');
        }
    }
}
