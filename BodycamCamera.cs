using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bodycam
{
    /// <summary>Efeito de camera corporal: grande-angular com barril, granulacao, aberracao, balanco de peito e falhas digitais.</summary>
    [RequireComponent(typeof(Camera))]
    public class BodycamCamera : MonoBehaviour
    {
        public PlayerController player;
        Camera cam; UniversalAdditionalCameraData data;
        LensDistortion lens; FilmGrain grain; ChromaticAberration ca; Vignette vig; ColorAdjustments col; Bloom bloom; Tonemapping tone;
        float bobT, glitch, nextGlitch = 6f;

        void Start()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 82f; cam.nearClipPlane = .04f; cam.allowHDR = true; cam.clearFlags = CameraClearFlags.Skybox;
            data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            var vg = new GameObject("BodycamVolume"); vg.transform.SetParent(transform, false);
            var vol = vg.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = prof;

            lens = prof.Add<LensDistortion>(true); lens.intensity.value = -.38f; lens.scale.value = 1.12f; lens.xMultiplier.value = 1f; lens.yMultiplier.value = 1f;
            grain = prof.Add<FilmGrain>(true); grain.type.value = FilmGrainLookup.Thin2; grain.intensity.value = .55f; grain.response.value = .6f;
            ca = prof.Add<ChromaticAberration>(true); ca.intensity.value = .22f;
            vig = prof.Add<Vignette>(true); vig.intensity.value = .32f; vig.smoothness.value = .45f;
            col = prof.Add<ColorAdjustments>(true); col.saturation.value = -18f; col.contrast.value = 14f; col.postExposure.value = .1f; col.colorFilter.value = new Color(.94f, 1f, .97f);
            bloom = prof.Add<Bloom>(true); bloom.intensity.value = .25f; bloom.threshold.value = 1f;
            tone = prof.Add<Tonemapping>(true); tone.mode.value = TonemappingMode.Neutral;

            Quality.Changed += ApplyQ; ApplyQ();
        }
        void OnDestroy() { Quality.Changed -= ApplyQ; }

        void ApplyQ()
        {
            if (cam == null) return;
            bloom.active = Quality.Level >= 1; ca.active = Quality.Level >= 1;
            cam.farClipPlane = BlockStreamer.LoadRadius + 150f;
            data.antialiasing = Quality.Level >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 p; float roll, pn, yn;
            float nx = Mathf.PerlinNoise(Time.time * .7f, 3.3f) - .5f, ny = Mathf.PerlinNoise(7.1f, Time.time * .7f) - .5f;
            if (player != null && player.InVehicle)
            {
                float sp = Mathf.Abs(player.Vehicle.ForwardSpeed);
                float a = sp * .0009f;
                p = new Vector3(nx * a * 2f, ny * a * 2f, 0); roll = nx * sp * .05f; pn = ny * sp * .03f; yn = nx * sp * .03f;
            }
            else
            {
                float s = player != null ? player.Speed01 : 0f;
                bobT += dt * (s > .01f ? Mathf.Lerp(7f, 12f, s) : 1.2f);
                float amp = Mathf.Lerp(0f, .07f, s);
                p = new Vector3(Mathf.Sin(bobT * .5f) * amp * .6f, Mathf.Abs(Mathf.Sin(bobT)) * amp - amp * .5f + Mathf.Sin(Time.time * 1.6f) * .004f, 0);
                roll = Mathf.Sin(bobT * .5f) * amp * 18f + nx * .8f; pn = ny * .6f + Mathf.Abs(Mathf.Sin(bobT)) * amp * 8f; yn = nx * .6f;
            }
            transform.localPosition = Vector3.Lerp(transform.localPosition, p, 12f * dt);
            transform.localRotation = Quaternion.Euler(pn, yn, roll);

            if (Time.time > nextGlitch) { glitch = .18f; nextGlitch = Time.time + Random.Range(7f, 18f); }
            bool g = glitch > 0f; if (g) glitch -= dt;
            float rain = DayNightWeather.I != null ? DayNightWeather.I.RainAmount : 0f;
            ca.intensity.value = g ? 1f : .22f;
            grain.intensity.value = g ? 1f : .55f + rain * .15f;
            lens.intensity.value = -.38f + (g ? Random.Range(-.1f, .1f) : 0f);
        }
    }
}
