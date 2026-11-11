using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StoreManager
{
    /// <summary>
    /// Sun, sky, ambient light and the post-processing grade (simplified from Real Life's Atmosphere).
    /// Follows the game clock: bright daytime, warm evenings, a darker night where the store's lights glow.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        public Light Sun { get; private set; }
        public float Daylight { get; private set; }

        Material sky;
        Bloom bloom;
        ColorAdjustments color;
        Vignette vignette;
        float lastHour = -99f;

        static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

        public void Init(Camera cam)
        {
            Light sun = null;
            var lights = FindObjectsByType<Light>();
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.SetParent(transform, true);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
            Sun = sun;

            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                sky = new Material(skyShader) { name = "Store Sky" };
                sky.SetFloat("_SunSize", 0.03f);
                sky.SetColor("_GroundColor", Hex("#5c6066"));
                RenderSettings.skybox = sky;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;

            if (GraphicsSettings.currentRenderPipeline == null) return;   // post-processing needs URP

            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.renderShadows = true;

            var go = new GameObject("Post Processing");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.65f);
            color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(10f);
            color.saturation.Override(8f);
            color.postExposure.Override(0.45f);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
        }

        void Update()
        {
            var store = Store.I;
            if (store == null || store.Clock == null) return;
            float hour = store.Clock.Minute / 60f;
            if (Mathf.Abs(hour - lastHour) < 0.05f) return;
            lastHour = hour;
            Apply(hour);
        }

        static readonly Color SunLow = Hex("#ffa060"), SunHigh = Hex("#fff3e2"), Moon = Hex("#9fb4e0");

        public void Apply(float hour)
        {
            if (Sun == null) return;
            const float rise = 6f, set = 21f;
            float t = (hour - rise) / (set - rise);
            float sunSin = (t >= 0f && t <= 1f) ? Mathf.Sin(t * Mathf.PI) : -0.2f;
            Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.25f, sunSin));
            float golden = Mathf.Clamp01(1f - Mathf.Abs(sunSin - 0.15f) / 0.2f) * Daylight;

            float elev = Mathf.Lerp(38f, 62f, Mathf.Clamp01(sunSin));
            float yaw = Mathf.Lerp(-70f, 70f, Mathf.Clamp01(t)) + 20f;
            Sun.transform.rotation = Quaternion.Euler(elev, yaw, 0f);
            Sun.color = Color.Lerp(Moon, Color.Lerp(SunLow, SunHigh, Mathf.Clamp01(sunSin * 2f)), Daylight);
            Sun.intensity = Mathf.Lerp(0.45f, 1.35f, Daylight);

            RenderSettings.ambientSkyColor = Color.Lerp(Hex("#45507a"), Hex("#b5c6dc"), Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(Hex("#3a3f58"), Hex("#c2bfb2"), Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(Hex("#22232c"), Hex("#6d675a"), Daylight);

            if (sky != null)
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.15f, 1.2f, Daylight));
                sky.SetColor("_SkyTint", Color.Lerp(Hex("#38466a"), Hex("#9aaac0"), Daylight));
                sky.SetFloat("_AtmosphereThickness", 0.8f + golden * 0.5f);
            }
            if (color != null)
            {
                color.postExposure.value = Mathf.Lerp(0.6f, 0.45f, Daylight);
                color.colorFilter.value = Color.Lerp(Hex("#d6defc"), Color.white, Daylight);
                bloom.intensity.value = Mathf.Lerp(0.9f, 0.4f, Daylight);
                vignette.intensity.value = Mathf.Lerp(0.26f, 0.18f, Daylight);
            }
        }
    }
}
