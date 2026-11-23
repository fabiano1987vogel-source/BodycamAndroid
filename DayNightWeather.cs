using System.Collections.Generic;
using UnityEngine;

namespace Bodycam
{
    public class DayNightWeather : MonoBehaviour
    {
        public static DayNightWeather I;
        public static Light Sun;
        public static readonly string[] WxNames = { "Limpo", "Nublado", "Neblina", "Chuva" };
        public static readonly List<Light> Lamps = new List<Light>();
        public float Hour = 14f;
        public int Weather;
        public float RainAmount { get { return rain; } }
        public bool IsNight { get { return Hour < 5.5f || Hour > 19.5f; } }
        const float DayMinutes = 12f;

        Light moon; Material sky;
        float cloud, fog, rain, wet, nextWx = 120f, slowT;
        ParticleSystem rainPs; AudioSource rainAudio;

        void Awake()
        {
            I = this;
            var sg = new GameObject("Sun"); sg.transform.SetParent(transform);
            Sun = sg.AddComponent<Light>(); Sun.type = LightType.Directional; Sun.shadows = LightShadows.Soft; Sun.shadowStrength = .9f;
            var mg = new GameObject("Moon"); mg.transform.SetParent(transform);
            moon = mg.AddComponent<Light>(); moon.type = LightType.Directional; moon.color = new Color(.55f, .65f, 1f); moon.shadows = LightShadows.None;
            var sh = Shader.Find("Skybox/Procedural");
            if (sh != null) { sky = new Material(sh); RenderSettings.skybox = sky; }
            RenderSettings.sun = Sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            rainPs = Fx.Rain();
            rainAudio = ProcAudio.Source(gameObject, ProcAudio.Rain(), true, 0f, 50f, false);
            rainAudio.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Hour += dt * 24f / (DayMinutes * 60f); if (Hour >= 24f) Hour -= 24f;
            nextWx -= dt;
            if (nextWx <= 0f)
            {
                nextWx = Random.Range(100f, 200f); float r = Random.value;
                Weather = r < .4f ? 0 : (r < .65f ? 1 : (r < .8f ? 2 : 3));
            }
            float tc = Weather == 0 ? .1f : (Weather == 1 ? .7f : (Weather == 2 ? .5f : .95f));
            cloud = Mathf.MoveTowards(cloud, tc, dt * .05f);
            fog = Mathf.MoveTowards(fog, Weather == 2 ? 1f : (Weather == 3 ? .3f : 0f), dt * .05f);
            rain = Mathf.MoveTowards(rain, Weather == 3 ? 1f : 0f, dt * .1f);
            wet = rain > .1f ? Mathf.MoveTowards(wet, 1f, dt * .03f) : Mathf.MoveTowards(wet, 0f, dt * .005f);

            float sunH = Mathf.Sin((Hour - 6f) / 12f * Mathf.PI);
            float dayF = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunH * 2.5f + .25f));
            Sun.transform.rotation = Quaternion.Euler((Hour - 6f) / 24f * 360f, -35f, 0f);
            moon.transform.rotation = Quaternion.Euler((Hour - 6f) / 24f * 360f + 180f, -35f, 0f);
            Sun.intensity = Mathf.Clamp01(sunH * 4f) * 1.25f * (1f - .65f * cloud);
            Sun.color = Color.Lerp(new Color(1f, .45f, .2f), new Color(1f, .96f, .88f), Mathf.Clamp01(sunH * 2.5f));
            moon.intensity = (1f - dayF) * .18f;

            Color skyC = Color.Lerp(new Color(.02f, .03f, .07f), Color.Lerp(new Color(.45f, .55f, .7f), new Color(.4f, .42f, .45f), cloud), dayF);
            RenderSettings.ambientSkyColor = skyC; RenderSettings.ambientEquatorColor = skyC * .8f; RenderSettings.ambientGroundColor = skyC * .4f;
            RenderSettings.fogColor = Color.Lerp(new Color(.02f, .025f, .04f), Color.Lerp(new Color(.65f, .72f, .8f), new Color(.55f, .57f, .6f), cloud), dayF);
            RenderSettings.fogDensity = .003f + fog * .012f + rain * .003f;
            if (sky != null)
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(.08f, 1.2f, dayF) * (1f - .5f * cloud));
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(.6f, 1.1f, dayF) + cloud * .8f);
                sky.SetFloat("_SunSize", .04f * (1f - cloud));
            }

            var em = rainPs.emission; em.rateOverTime = 1500f * rain;
            if (PlayerController.I != null) rainPs.transform.position = PlayerController.I.transform.position + Vector3.up * 18f;
            rainAudio.volume = rain * .6f;

            slowT -= dt;
            if (slowT <= 0f)
            {
                slowT = .5f;
                Mats.SetWet(wet);
                float nf = 1f - dayF;
                Mats.Glow(Mats.Window, new Color(1f, .8f, .5f) * nf * 1.5f);
                Mats.Glow(Mats.Lamp, new Color(1f, .85f, .6f) * nf * 2.5f);
                bool lampsOn = dayF < .5f;
                foreach (var l in Lamps) if (l != null) l.enabled = lampsOn;
            }
        }
    }
}
