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

        public static Mesh GetMesh(string name)
        {
            Load();
            Mesh m;
            return meshes.TryGetValue(name, out m) ? m : null;
        }

        /// <summary>Placement of one model inside a merged mesh, with the same import fix-ups Spawn uses.</summary>
        public static Matrix4x4 UnitMatrix(Vector3 position, float yaw)
        {
            Load();
            return Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f) * fixRotation, fixScale);
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

        // ------------------------------------------------------------------ textured wall faces

        static readonly Dictionary<string, Mesh> faced = new Dictionary<string, Mesh>();

        static Vector3 ModelSpace(Vector3 v) { return fixRotation * Vector3.Scale(fixScale, v); }

        /// <summary>
        /// A wall model split three ways: sub-mesh 0 keeps the palette (trim, dado, rails), 1 and 2 are the body's
        /// two big faces (the innermost faces looking along -Z and +Z) with metre UVs (u along the wall, v up),
        /// so a tiling texture can go on them. Null if the model is missing or its mesh isn't readable.
        /// </summary>
        static Mesh Faced(string name)
        {
            Mesh m;
            if (faced.TryGetValue(name, out m)) return m;
            faced[name] = null;
            Mesh src;
            if (!meshes.TryGetValue(name, out src) || src == null || !src.isReadable || src.subMeshCount != 1) return null;
            var v = src.vertices;
            var n = src.normals;
            var uv = src.uv;
            var t = src.triangles;
            if (n == null || n.Length != v.Length || uv == null || uv.Length != v.Length) return null;

            // The body faces: of the faces looking straight along Z, the ones closest to the middle.
            float inner = float.MaxValue;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 nz = ModelSpace(n[t[i]] + n[t[i + 1]] + n[t[i + 2]]).normalized;
                if (Mathf.Abs(nz.z) < 0.98f) continue;
                inner = Mathf.Min(inner, Mathf.Abs(ModelSpace(v[t[i]]).z));
            }
            if (inner == float.MaxValue) return null;

            var verts = new List<Vector3>(v);
            var norms = new List<Vector3>(n);
            var uvs = new List<Vector2>(uv);
            var rest = new List<int>();
            var neg = new List<int>();
            var pos = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 nz = ModelSpace(n[t[i]] + n[t[i + 1]] + n[t[i + 2]]).normalized;
                bool body = Mathf.Abs(nz.z) >= 0.98f;
                for (int k = 0; k < 3 && body; k++) body = Mathf.Abs(Mathf.Abs(ModelSpace(v[t[i + k]]).z) - inner) < 0.002f;
                if (!body) { rest.Add(t[i]); rest.Add(t[i + 1]); rest.Add(t[i + 2]); continue; }
                var side = nz.z < 0f ? neg : pos;
                for (int k = 0; k < 3; k++)
                {
                    // Seen from its own side the texture reads left to right, not mirrored.
                    Vector3 p = ModelSpace(v[t[i + k]]);
                    side.Add(verts.Count);
                    verts.Add(v[t[i + k]]);
                    norms.Add(n[t[i + k]]);
                    uvs.Add(new Vector2(nz.z < 0f ? p.x : -p.x, p.y));
                }
            }
            if (neg.Count == 0 && pos.Count == 0) return null;

            m = new Mesh { name = name + " (faced)" };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.subMeshCount = 3;
            m.SetTriangles(rest, 0);
            m.SetTriangles(neg, 1);
            m.SetTriangles(pos, 2);
            m.RecalculateBounds();
            faced[name] = m;
            return m;
        }

        /// <summary>
        /// Spawns a wall piece with tiling textures on its two body faces: texNeg on the side its local -Z looks at,
        /// texPos on the other. Texture offsets follow the world position, so neighbouring pieces line up.
        /// Falls back to the plain palette model if the faces can't be made.
        /// </summary>
        public static GameObject SpawnFaced(string name, Transform parent, Vector3 position, float yaw, string texNeg, string texPos)
        {
            Load();
            var mesh = Faced(name);
            if (mesh == null) return Spawn(name, parent, position, yaw);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * fixRotation;
            go.transform.localScale = fixScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Vector3 along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float s = Vector3.Dot(position, along);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                palette,
                Mats.Textured(texNeg, Vector2.one, Color.white, new Vector2(Frac(s), 0f)),
                Mats.Textured(texPos, Vector2.one, Color.white, new Vector2(Frac(-s), 0f)),
            };
            return go;
        }

        static float Frac(float x) { return Mathf.Round((x - Mathf.Floor(x)) * 100f) / 100f % 1f; }

        public static string StockMesh(ProductDef p, bool counter)
        {
            return (counter ? "Deli_" : "Stock_") + p.Name.Replace(' ', '_');
        }
    }
}
