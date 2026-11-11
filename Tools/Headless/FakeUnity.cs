// Minimal stand-in for the UnityEngine API used by Storemanager.
// Enough to type-check the game and run its simulation headless (no rendering, no GUI).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum KeyCode { None, W, A, S, D, Q, E, R, Tab, Space, Escape, Alpha1, Alpha2, Alpha3, Alpha4, F5, F9 }

    public static class Application
    {
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "storemanager-headless");
    }

    public class AsyncOperation { public event System.Action<AsyncOperation> completed; internal void Done() { if (completed != null) completed(this); } }
    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public enum FindObjectsSortMode { None, InstanceID }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum EventType { MouseDown, MouseUp, MouseMove, MouseDrag, KeyDown, KeyUp, ScrollWheel, Repaint, Layout }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Rad2Deg = 57.29578f, Deg2Rad = 0.01745329f;
        public static float Acos(float v) { return (float)Math.Acos(v); }
        public static float Floor(float v) { return (float)Math.Floor(v); }
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Abs(float v) { return Math.Abs(v); }
        public static int Abs(int v) { return Math.Abs(v); }
        public static int FloorToInt(float v) { return (int)Math.Floor(v); }
        public static int CeilToInt(float v) { return (int)Math.Ceiling(v); }
        public static int RoundToInt(float v) { return (int)Math.Round(v, MidpointRounding.ToEven); }
        public static float Round(float v) { return (float)Math.Round(v, MidpointRounding.ToEven); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float Exp(float v) { return (float)Math.Exp(v); }
        public static float InverseLerp(float a, float b, float v) { return a == b ? 0f : Clamp01((v - a) / (b - a)); }
        public static float MoveTowards(float a, float b, float d) { return Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d; }
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return b * t + a * (1f - t); }
        public static float Sqrt(float v) { return (float)Math.Sqrt(v); }
        public static float Sin(float v) { return (float)Math.Sin(v); }
        public static float Cos(float v) { return (float)Math.Cos(v); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
    }

    public static class Random
    {
        static System.Random rng = new System.Random(12345);
        public static void InitState(int seed) { rng = new System.Random(seed); }
        public static float value { get { return (float)rng.NextDouble(); } }
        public static float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        public static int Range(int a, int b) { return b <= a ? a : rng.Next(a, b); }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) { return new Vector2Int(a.x + b.x, a.y + b.y); }
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) { return new Vector2Int(a.x - b.x, a.y - b.y); }
        public static Vector2Int right { get { return new Vector2Int(1, 0); } }
        public static Vector2Int left { get { return new Vector2Int(-1, 0); } }
        public static Vector2Int up { get { return new Vector2Int(0, 1); } }
        public static Vector2Int down { get { return new Vector2Int(0, -1); } }
        public static bool operator ==(Vector2Int a, Vector2Int b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2Int a, Vector2Int b) { return !(a == b); }
        public bool Equals(Vector2Int o) { return this == o; }
        public override bool Equals(object o) { return o is Vector2Int && this == (Vector2Int)o; }
        public override int GetHashCode() { return x * 73856093 ^ y * 19349663; }
        public override string ToString() { return "(" + x + ", " + y + ")"; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 back { get { return new Vector3(0, 0, -1); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); }
        public static Vector3 operator *(float f, Vector3 a) { return a * f; }
        public static Vector3 operator /(Vector3 a, float f) { return new Vector3(a.x / f, a.y / f, a.z / f); }
        public static bool operator ==(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude < 1e-10f; }
        public static bool operator !=(Vector3 a, Vector3 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector3 && this == (Vector3)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode(); }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public override string ToString() { return "(" + x.ToString("0.00") + ", " + y.ToString("0.00") + ", " + z.ToString("0.00") + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }
        public static Quaternion Inverse(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }
        public static Quaternion FromToRotation(Vector3 a, Vector3 b) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            float d = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            if (d < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); }
            var q = new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return n > 0f ? new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n) : identity;
        }
        public static Quaternion Euler(float ex, float ey, float ez)
        {
            float d = Mathf.PI / 180f;
            float cx = Mathf.Cos(ex * d * 0.5f), sx = Mathf.Sin(ex * d * 0.5f);
            float cy = Mathf.Cos(ey * d * 0.5f), sy = Mathf.Sin(ey * d * 0.5f);
            float cz = Mathf.Cos(ez * d * 0.5f), sz = Mathf.Sin(ez * d * 0.5f);
            // Unity order: Z, then X, then Y  => q = qy * qx * qz
            var qx = new Quaternion(sx, 0, 0, cx);
            var qy = new Quaternion(0, sy, 0, cy);
            var qz = new Quaternion(0, 0, sz, cz);
            return qy * qx * qz;
        }
        public static Quaternion LookRotation(Vector3 f)
        {
            float yaw = Mathf.Atan2(f.x, f.z) * 180f / Mathf.PI;
            return Euler(0, yaw, 0);
        }
        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2, y2 = q.y * 2, z2 = q.z * 2;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3(
                (1 - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1 - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1 - (xx + yy)) * v.z);
        }
    }

    public struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color red { get { return new Color(1, 0, 0); } }
        public static Color green { get { return new Color(0, 1, 0); } }
        public static Color white { get { return new Color(1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0); } }
        public static Color magenta { get { return new Color(1, 0, 1); } }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a * f); }
        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
        public static Color HSVToRGB(float h, float s, float v) { return new Color(v, v * (1 - s), v * (1 - s * h)); }
        public bool Equals(Color o) { return r == o.r && g == o.g && b == o.b && a == o.a; }
        public override bool Equals(object o) { return o is Color && Equals((Color)o); }
        public override int GetHashCode() { return r.GetHashCode() ^ (g.GetHashCode() << 2) ^ (b.GetHashCode() >> 2) ^ a.GetHashCode(); }
    }

    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s, out Color c)
        {
            c = new Color(0, 0, 0);
            if (string.IsNullOrEmpty(s) || s[0] != '#' || s.Length != 7) return false;
            int v = Convert.ToInt32(s.Substring(1), 16);
            c = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
            return true;
        }
        public static string ToHtmlStringRGB(Color c)
        {
            return ((int)(Mathf.Clamp01(c.r) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.g) * 255)).ToString("X2") + ((int)(Mathf.Clamp01(c.b) * 255)).ToString("X2");
        }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public bool Contains(Vector2 p) { return p.x >= x && p.y >= y && p.x < x + width && p.y < y + height; }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
    }

    public struct Matrix4x4
    {
        public static Matrix4x4 Scale(Vector3 v) { return new Matrix4x4(); }
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) { return new Matrix4x4(); }
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; }
        public Vector3 GetPoint(float t) { return origin + direction * t; }
    }

    public struct Plane
    {
        Vector3 n; float d;
        public Plane(Vector3 normal, Vector3 point) { n = normal; d = -(normal.x * point.x + normal.y * point.y + normal.z * point.z); }
        public bool Raycast(Ray r, out float enter) { enter = 0; return false; }
    }

    public struct RaycastHit
    {
        public Collider collider { get { return null; } }
    }

    public static class Physics
    {
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance) { hit = new RaycastHit(); return false; }
    }

    public static class Time
    {
        public static float deltaTime, unscaledDeltaTime, unscaledTime, time;
        public static float timeScale = 1f;
        public static int frameCount;
    }

    public static class Screen
    {
        public static int width = 1920, height = 1080;
    }

    public static class Input
    {
        public static bool GetKey(KeyCode k) { return false; }
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static Vector3 mousePosition { get { return Vector3.zero; } }
        public static Vector2 mouseScrollDelta { get { return Vector2.zero; } }
    }

    public static class Debug
    {
        public static void Log(object o) { Console.WriteLine(o); }
        public static void LogWarning(object o) { Console.WriteLine("WARN " + o); }
        public static void LogError(object o) { Console.WriteLine("ERROR " + o); }
    }

    // ------------------------------------------------------------------ objects

    public class Object
    {
        internal bool destroyed;
        public string name = "";
        static int nextId = 1;
        readonly int instanceId = nextId++;
        public int GetInstanceID() { return instanceId; }

        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.destroyed;
            bool bn = ReferenceEquals(b, null) || b.destroyed;
            if (an || bn) return an && bn;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object o) { return ReferenceEquals(this, o); }
        public override int GetHashCode() { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); }
        public static implicit operator bool(Object o) { return o != null; }

        public static void Destroy(Object o)
        {
            if (ReferenceEquals(o, null)) return;
            Engine.PendingDestroy.Add(o);
        }

        public static T FindAnyObjectByType<T>() where T : Object
        {
            foreach (var c in Engine.AllComponents) if (!c.destroyed && c is T) return (T)(Object)c;
            return null;
        }

        public static T[] FindObjectsByType<T>() where T : Object { return FindObjectsByType<T>(FindObjectsSortMode.None); }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object
        {
            var l = new List<T>();
            foreach (var c in Engine.AllComponents) if (!c.destroyed && c is T) l.Add((T)(Object)c);
            return l.ToArray();
        }
    }

    public class GameObject : Object
    {
        public readonly Transform transform;
        internal readonly List<Component> components = new List<Component>();
        bool activeSelfField = true;
        public string tag = "Untagged";

        public GameObject() : this("GameObject") { }
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform();
            transform.gameObjectField = this;
            components.Add(transform);
            Engine.AllObjects.Add(this);
        }

        public bool activeSelf { get { return activeSelfField; } }
        public bool activeInHierarchy
        {
            get
            {
                if (destroyed || !activeSelfField) return false;
                var p = transform.parent;
                return p == null || p.gameObject.activeInHierarchy;
            }
        }

        public void SetActive(bool v)
        {
            if (activeSelfField == v) return;
            activeSelfField = v;
            if (!v)
            {
                foreach (var c in components) { var mb = c as MonoBehaviour; if (mb != null) mb.StopAllCoroutines(); }
            }
        }

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T();
            c.gameObjectField = this;
            components.Add(c);
            Engine.AllComponents.Add(c);
            var mb = c as MonoBehaviour;
            if (mb != null) Engine.Register(mb);
            return c;
        }

        public T GetComponent<T>() where T : class
        {
            foreach (var c in components) if (!c.destroyed && c is T) return c as T;
            return null;
        }

        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class { return new T[0]; }

        public static GameObject CreatePrimitive(PrimitiveType t)
        {
            var go = new GameObject(t.ToString());
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
            return go;
        }
    }

    public class Component : Object
    {
        internal GameObject gameObjectField;
        public GameObject gameObject { get { return gameObjectField; } }
        public Transform transform { get { return gameObjectField.transform; } }
        public T GetComponent<T>() where T : class { return gameObjectField.GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class { return new T[0]; }
        public T GetComponentInParent<T>() where T : class
        {
            var t = transform;
            while (t != null)
            {
                var c = t.gameObject.GetComponent<T>();
                if (c != null) return c;
                t = t.parent;
            }
            return null;
        }
    }

    public class Transform : Component
    {
        Transform parentField;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 forward { get { return rotation * Vector3.forward; } }
        public Vector3 right { get { return rotation * Vector3.right; } }
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public Transform parent { get { return parentField; } }
        public Vector3 localPosition
        {
            get { return parentField != null ? position - parentField.position : position; }
            set { position = parentField != null ? parentField.position + value : value; }
        }
        public void SetParent(Transform p, bool worldPositionStays) { parentField = p; }
        public Transform Find(string n)
        {
            foreach (var go in Engine.AllObjects)
                if (!go.destroyed && go.transform.parentField == this && go.name == n) return go.transform;
            return null;
        }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
    }

    public class Renderer : Component
    {
        public Material sharedMaterial;
        public Material[] sharedMaterials { get { return new[] { sharedMaterial }; } set { sharedMaterial = value != null && value.Length > 0 ? value[0] : null; } }
    }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public struct Bounds { public Vector3 center, size; }
    public class Mesh : Object
    {
        public Bounds bounds; public int subMeshCount = 1;
        public UnityEngine.Rendering.IndexFormat indexFormat;
        public void Clear() { }
        public void MarkDynamic() { }
        public void CombineMeshes(CombineInstance[] parts, bool merge, bool useMatrices) { }
        public string name;
        public bool isReadable;
        public Vector3[] vertices = new Vector3[0], normals = new Vector3[0];
        public Vector2[] uv = new Vector2[0];
        public int[] triangles = new int[0];
        public void SetVertices(System.Collections.Generic.List<Vector3> v) { }
        public void SetNormals(System.Collections.Generic.List<Vector3> v) { }
        public void SetUVs(int channel, System.Collections.Generic.List<Vector2> v) { }
        public void SetTriangles(System.Collections.Generic.List<int> t, int submesh) { }
        public void RecalculateBounds() { }
    }
    public struct CombineInstance { public Mesh mesh; public Matrix4x4 transform; }
    public class TextAsset : Object { public string text = ""; }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public class Texture : Object { public FilterMode filterMode; }
    public enum TextureWrapMode { Repeat, Clamp }
    public class Texture2D : Texture
    {
        public TextureWrapMode wrapMode;
        public Texture2D() { }
        public Texture2D(int w, int h) { }
        public void SetPixel(int x, int y, Color c) { }
        public void Apply() { }
        static Texture2D white;
        public static Texture2D whiteTexture { get { return white ?? (white = new Texture2D(1, 1)); } }
    }
    public enum MaterialGlobalIlluminationFlags { None, RealtimeEmissive, BakedEmissive, AnyEmissive, EmissiveIsBlack }
    public static class Resources
    {
        public static T Load<T>(string path) where T : Object { return null; }
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() { return new T(); }
    }
    public class MeshRenderer : Renderer { }
    public class Collider : Component { }
    public class BoxCollider : Collider { }
    public class CapsuleCollider : Collider { public float radius, height; public Vector3 center; }
    public class AudioListener : Behaviour { }

    public class Light : Behaviour
    {
        public LightType type;
        public float intensity, shadowStrength, shadowBias, shadowNormalBias;
        public LightShadows shadows;
        public Color color;
    }

    public class Camera : Behaviour
    {
        public static Camera main { get { foreach (var c in Engine.AllComponents) { var cam = c as Camera; if (cam != null && !cam.destroyed && cam.gameObject.tag == "MainCamera") return cam; } return null; } }
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float fieldOfView, nearClipPlane, farClipPlane;
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(Vector3.zero, Vector3.forward); }
        public Vector3 WorldToScreenPoint(Vector3 p) { return new Vector3(0, 0, -1); }
    }

    public class Shader : Object
    {
        public static Shader Find(string n) { return new Shader { name = n }; }
    }

    public class Material : Object
    {
        public Color color;
        public Texture mainTexture;
        public Vector2 mainTextureScale, mainTextureOffset;
        public bool enableInstancing;
        public int renderQueue;
        public MaterialGlobalIlluminationFlags globalIlluminationFlags;
        public Shader shader;
        public Material(Shader s) { shader = s; }
        public void SetTexture(string n, Texture t) { }
        public void SetTextureScale(string n, Vector2 v) { }
        public void SetTextureOffset(string n, Vector2 v) { }
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float v) { }
        public void SetInt(string n, int v) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
        public void SetOverrideTag(string a, string b) { }
    }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode;
        public static Color ambientLight, ambientSkyColor, ambientEquatorColor, ambientGroundColor;
        public static Light sun;
        public static Material skybox;
        public static bool fog;
    }

    public class YieldInstruction { }

    public class Coroutine : YieldInstruction
    {
        internal IEnumerator routine;
        internal MonoBehaviour owner;
        internal bool done;
        internal int readyFrame;
        internal Coroutine waitingOn;
        internal Coroutine parentWaiting;
    }

    public class MonoBehaviour : Behaviour
    {
        internal readonly List<Coroutine> routines = new List<Coroutine>();
        internal bool started;

        public Coroutine StartCoroutine(IEnumerator e)
        {
            if (!gameObject.activeInHierarchy) throw new Exception("Coroutine couldn't be started because the game object '" + gameObject.name + "' is inactive!");
            var c = new Coroutine { routine = e, owner = this };
            routines.Add(c);
            Engine.Step(c);
            return c;
        }

        public void StopAllCoroutines()
        {
            foreach (var c in routines) c.done = true;
            routines.Clear();
        }

        public static void Destroy(Object o) { Object.Destroy(o); }
    }

    // ------------------------------------------------------------------ GUI (no-ops)

    public class GUIContent { public static GUIContent none = new GUIContent(); }
    public class GUIStyleState { public Color textColor; public Texture2D background; }
    public class RectOffset { public RectOffset() { } public RectOffset(int l, int r, int t, int b) { } }
    public class GUIStyle
    {
        public int fontSize;
        public bool wordWrap, richText;
        public FontStyle fontStyle;
        public TextAnchor alignment;
        public GUIStyleState normal = new GUIStyleState(), onNormal = new GUIStyleState();
        public RectOffset padding = new RectOffset();
        public static GUIStyle none = new GUIStyle();
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public static implicit operator GUIStyle(string s) { return new GUIStyle(); }
    }
    public class GUISkin { public GUIStyle label = new GUIStyle(), button = new GUIStyle(), box = new GUIStyle(); }
    public class GUILayoutOption { }
    public class Event { public static Event current = new Event(); public EventType type = EventType.Layout; public Vector2 mousePosition; }
    public static class GUIUtility { public static int hotControl; }

    public static class GUI
    {
        public static Matrix4x4 matrix;
        public static bool enabled = true;
        public static GUISkin skin = new GUISkin();
        public static void Box(Rect r, GUIContent c) { }
        public static void Label(Rect r, string t, GUIStyle s) { }
        public static bool Button(Rect r, string t) { return false; }
        public static bool Button(Rect r, GUIContent c, GUIStyle s) { return false; }
        public static Color color = new Color(1, 1, 1), backgroundColor = new Color(1, 1, 1);
        public static void DrawTexture(Rect r, Texture t) { }
        public delegate void WindowFunction(int id);
        public static Rect Window(int id, Rect r, WindowFunction f, GUIContent c, GUIStyle s) { f(id); return r; }
        public static Rect Window(int id, Rect r, WindowFunction f, string t) { f(id); return r; }
        public static void DragWindow(Rect r) { }
        public static void FocusWindow(int id) { }
    }

    public static class GUILayout
    {
        public static GUILayoutOption Width(float w) { return new GUILayoutOption(); }
        public static GUILayoutOption Height(float h) { return new GUILayoutOption(); }
        public static void BeginArea(Rect r) { }
        public static void EndArea() { }
        public static void BeginHorizontal(params GUILayoutOption[] o) { }
        public static void BeginHorizontal(GUIStyle s, params GUILayoutOption[] o) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(GUIStyle s, params GUILayoutOption[] o) { }
        public static void BeginVertical(params GUILayoutOption[] o) { }
        public static void EndVertical() { }
        public static void Label(string t, params GUILayoutOption[] o) { }
        public static void Label(string t, GUIStyle s, params GUILayoutOption[] o) { }
        public static bool Button(string t, params GUILayoutOption[] o) { return false; }
        public static bool Button(string t, GUIStyle s, params GUILayoutOption[] o) { return false; }
        public static bool Toggle(bool v, string t, params GUILayoutOption[] o) { return v; }
        public static bool Toggle(bool v, string t, GUIStyle s, params GUILayoutOption[] o) { return v; }
        public static int Toolbar(int sel, string[] texts, params GUILayoutOption[] o) { return sel; }
        public static void FlexibleSpace() { }
        public static void Space(float px) { }
        public static Vector2 BeginScrollView(Vector2 p, params GUILayoutOption[] o) { return p; }
        public static void EndScrollView() { }
    }

    // ------------------------------------------------------------------ engine loop

    public static class Engine
    {
        public static readonly List<GameObject> AllObjects = new List<GameObject>();
        public static readonly List<Component> AllComponents = new List<Component>();
        public static readonly List<Object> PendingDestroy = new List<Object>();
        static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        static readonly Dictionary<Type, MethodInfo[]> methods = new Dictionary<Type, MethodInfo[]>();
        public static int Frame;
        public static int Errors;

        static MethodInfo Find(Type t, string name)
        {
            for (var x = t; x != null && x != typeof(MonoBehaviour); x = x.BaseType)
            {
                var m = x.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null) return m;
            }
            return null;
        }

        static MethodInfo[] Methods(Type t)
        {
            MethodInfo[] m;
            if (!methods.TryGetValue(t, out m))
            {
                m = new[] { Find(t, "Awake"), Find(t, "Start"), Find(t, "Update"), Find(t, "LateUpdate"), Find(t, "OnDestroy") };
                methods[t] = m;
            }
            return m;
        }

        static void Call(MonoBehaviour mb, int idx)
        {
            var m = Methods(mb.GetType())[idx];
            if (m == null) return;
            try { m.Invoke(mb, null); }
            catch (TargetInvocationException e) { Errors++; Console.WriteLine("EXCEPTION in " + mb.GetType().Name + "." + m.Name + ": " + e.InnerException); }
        }

        internal static void Register(MonoBehaviour mb)
        {
            behaviours.Add(mb);
            Call(mb, 0);
        }

        internal static void Step(Coroutine c)
        {
            if (c.done) return;
            bool more;
            try { more = c.routine.MoveNext(); }
            catch (Exception e)
            {
                Errors++;
                Console.WriteLine("EXCEPTION in coroutine of " + c.owner.GetType().Name + ": " + e);
                more = false;
            }
            if (c.done) return; // stopped while running
            if (!more) { Finish(c); return; }
            object y = c.routine.Current;
            c.readyFrame = Frame + 1;
            c.waitingOn = null;
            var child = y as Coroutine;
            if (child == null && y is IEnumerator) child = c.owner.StartCoroutine((IEnumerator)y);
            if (child != null && !child.done)
            {
                c.waitingOn = child;
                child.parentWaiting = c;
            }
        }

        static void Finish(Coroutine c)
        {
            c.done = true;
            c.owner.routines.Remove(c);
            if (c.parentWaiting != null && c.parentWaiting.waitingOn == c)
            {
                c.parentWaiting.waitingOn = null;
                c.parentWaiting.readyFrame = Frame + 1;
            }
        }

        public static void Tick(float unscaledDt)
        {
            Frame++;
            Time.frameCount = Frame;
            Time.unscaledDeltaTime = unscaledDt;
            Time.unscaledTime += unscaledDt;
            Time.deltaTime = Math.Min(unscaledDt, 0.3333f) * Time.timeScale;
            Time.time += Time.deltaTime;

            var snapshot = behaviours.ToArray();
            foreach (var mb in snapshot)
            {
                if (mb.destroyed || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
                if (!mb.started) { mb.started = true; Call(mb, 1); }
            }
            foreach (var mb in snapshot)
            {
                if (mb.destroyed || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
                Call(mb, 2);
            }
            foreach (var mb in snapshot)
            {
                if (mb.destroyed || !mb.gameObject.activeInHierarchy) continue;
                foreach (var c in mb.routines.ToArray())
                {
                    if (c.done || c.waitingOn != null || c.readyFrame > Frame) continue;
                    Step(c);
                }
            }
            foreach (var mb in snapshot)
            {
                if (mb.destroyed || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
                Call(mb, 3);
            }
            ProcessDestroy();
        }

        static void ProcessDestroy()
        {
            while (PendingDestroy.Count > 0)
            {
                var list = PendingDestroy.ToArray();
                PendingDestroy.Clear();
                foreach (var o in list)
                {
                    if (o.destroyed) continue;
                    var go = o as GameObject;
                    if (go != null) DestroyGameObject(go);
                    else
                    {
                        var mb = o as MonoBehaviour;
                        if (mb != null) { mb.StopAllCoroutines(); Call(mb, 4); behaviours.Remove(mb); }
                        o.destroyed = true;
                        AllComponents.Remove(o as Component);
                    }
                }
            }
        }

        static void DestroyGameObject(GameObject go)
        {
            if (go.destroyed) return;
            // children first
            foreach (var child in AllObjects.ToArray())
                if (!child.destroyed && child.transform.parent != null && ReferenceEquals(child.transform.parent.gameObject, go))
                    DestroyGameObject(child);
            foreach (var c in go.components)
            {
                var mb = c as MonoBehaviour;
                if (mb != null) { mb.StopAllCoroutines(); Call(mb, 4); behaviours.Remove(mb); }
                c.destroyed = true;
                AllComponents.Remove(c);
            }
            go.destroyed = true;
            AllObjects.Remove(go);
        }

        public static int BehaviourCount { get { return behaviours.Count; } }

        /// <summary>Destroys every object right away (a scene unload).</summary>
        public static void DestroyAll()
        {
            foreach (var go in AllObjects.ToArray()) if (!go.destroyed && go.transform.parent == null) DestroyGameObject(go);
            foreach (var go in AllObjects.ToArray()) if (!go.destroyed) DestroyGameObject(go);
            PendingDestroy.Clear();
        }
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; }

    public static class SceneManager
    {
        static Scene active = new Scene { name = "Headless" };
        public static Scene GetActiveScene() { return active; }
        public static Scene CreateScene(string name) { return new Scene { name = name }; }
        public static bool SetActiveScene(Scene s) { active = s; return true; }
        /// <summary>Headless: unloads at once and returns null (the caller boots straight away).</summary>
        public static UnityEngine.AsyncOperation UnloadSceneAsync(Scene s) { UnityEngine.Engine.DestroyAll(); return null; }
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16, UInt32 }
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
    public enum BlendMode { Zero, One, DstColor, SrcColor, OneMinusDstColor, SrcAlpha, OneMinusSrcColor, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate, OneMinusSrcAlpha }
    public class VolumeParameter<T>
    {
        public T value;
        public void Override(T v) { value = v; }
    }
    public class VolumeComponent : UnityEngine.ScriptableObject { }
    public class VolumeProfile : UnityEngine.ScriptableObject
    {
        public T Add<T>(bool overrides) where T : VolumeComponent, new() { return new T(); }
    }
    public class Volume : UnityEngine.MonoBehaviour
    {
        public bool isGlobal;
        public float priority;
        public VolumeProfile sharedProfile;
    }
    public class RenderPipelineAsset : UnityEngine.Object { }
    public static class GraphicsSettings
    {
        public static RenderPipelineAsset currentRenderPipeline { get { return null; } }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public class Tonemapping : VolumeComponent { public VolumeParameter<TonemappingMode> mode = new VolumeParameter<TonemappingMode>(); }
    public class Bloom : VolumeComponent
    {
        public VolumeParameter<float> threshold = new VolumeParameter<float>(), intensity = new VolumeParameter<float>(), scatter = new VolumeParameter<float>();
    }
    public class ColorAdjustments : VolumeComponent
    {
        public VolumeParameter<float> contrast = new VolumeParameter<float>(), saturation = new VolumeParameter<float>(), postExposure = new VolumeParameter<float>();
        public VolumeParameter<Color> colorFilter = new VolumeParameter<Color>();
    }
    public class Vignette : VolumeComponent { public VolumeParameter<float> intensity = new VolumeParameter<float>(), smoothness = new VolumeParameter<float>(); }
    public class UniversalAdditionalCameraData : UnityEngine.MonoBehaviour
    {
        public bool renderPostProcessing, renderShadows;
        public AntialiasingMode antialiasing;
    }
    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera cam) { return new UniversalAdditionalCameraData(); }
    }
}
