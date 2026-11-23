using System.Collections.Generic;
using UnityEngine;

namespace Bodycam
{
    /// <summary>Audio gerado por codigo (chuva, motor, sirene, passos...). Substitua por clipes reais quando quiser.</summary>
    public static class ProcAudio
    {
        const int SR = 22050;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        static AudioClip Make(string n, float sec, System.Func<int, float, float> f)
        {
            AudioClip a;
            if (cache.TryGetValue(n, out a)) return a;
            int len = (int)(SR * sec); var d = new float[len];
            for (int i = 0; i < len; i++) d[i] = Mathf.Clamp(f(i, (float)i / SR), -1f, 1f);
            var clip = AudioClip.Create(n, len, 1, SR, false); clip.SetData(d, 0);
            cache[n] = clip; return clip;
        }
        public static AudioClip Rain() { float y = 0; return Make("rain", 3f, (i, t) => { y += .35f * ((Random.value * 2f - 1f) - y); return y * .7f; }); }
        public static AudioClip Murmur() { float y = 0; return Make("murmur", 4f, (i, t) => { y += .02f * ((Random.value * 2f - 1f) - y); return y * 7f * (.6f + .4f * Mathf.Sin(t * 1.5f)); }); }
        public static AudioClip Engine()
        {
            return Make("engine", 1f, (i, t) =>
            {
                float ph = t * 50f;
                float s = Mathf.Sin(ph * 6.2832f) * .5f + Mathf.Sin(ph * 12.566f) * .3f + ((ph * 3f) % 1f - .5f) * .3f;
                return s * .5f;
            });
        }
        public static AudioClip Siren() { float ph = 0; return Make("siren", 2f, (i, t) => { float f = 700f + 400f * Mathf.PingPong(t, 1f); ph += f / SR; return Mathf.Sin(ph * 6.2832f) * .35f; }); }
        public static AudioClip Alarm() { return Make("alarm", 1f, (i, t) => (t % .5f < .25f) ? Mathf.Sin(t * 6.2832f * 1500f) * .3f : 0f); }
        public static AudioClip Step() { float y = 0; return Make("step", .18f, (i, t) => { y += .2f * ((Random.value * 2f - 1f) - y); return y * Mathf.Exp(-t * 25f) * 1.6f; }); }
        public static AudioClip Thud() { return Make("thud", .3f, (i, t) => (Mathf.Sin(t * 6.2832f * 70f) * .7f + (Random.value - .5f) * .3f) * Mathf.Exp(-t * 14f)); }

        public static AudioSource Source(GameObject go, AudioClip clip, bool loop, float vol, float maxDist, bool spatial = true)
        {
            var a = go.AddComponent<AudioSource>();
            a.clip = clip; a.loop = loop; a.volume = vol; a.playOnAwake = false;
            a.spatialBlend = spatial ? 1f : 0f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = maxDist;
            return a;
        }
    }

    public static class Fx
    {
        public static ParticleSystem Rain()
        {
            var go = new GameObject("Rain"); var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = 1.4f; main.startSpeed = 0f; main.startSize = .05f;
            main.gravityModifier = 2.5f; main.maxParticles = 2500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(.8f, .85f, 1f, .35f);
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, .5f, 36f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .045f; r.lengthScale = 1f;
            r.sharedMaterial = Mats.Sprite(ProcTex.Soft(32, 2)); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public static ParticleSystem Smoke(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Smoke"); go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startLifetime = 5f; main.startSpeed = .8f; main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startColor = new Color(.25f, .25f, .25f, .55f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 10f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10f; sh.radius = .6f;
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .4f, 1, 1.6f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Mats.Sprite(ProcTex.Soft(64, 1));
            ps.Play();
            return ps;
        }
    }

    /// <summary>Niveis de qualidade (0 Baixo .. 3 Ultra) e limite de FPS.</summary>
    public static class Quality
    {
        public static int Level = 1, Fps = 30;
        public static readonly string[] Names = { "BAIXO", "MEDIO", "ALTO", "ULTRA" };
        static readonly float[] RS = { .6f, .75f, .9f, 1f };
        static readonly float[] SD = { 0f, 45f, 90f, 140f };
        static readonly float[] LR = { 110f, 140f, 170f, 210f };
        static readonly float[] NF = { .4f, .7f, 1f, 1.4f };
        static readonly int[] TR = { 8, 16, 28, 42 };
        static readonly int[] TC = { 5, 9, 15, 22 };
        public static float NpcFactor { get { return NF[Level]; } }
        public static int TreesPerBlock { get { return TR[Level]; } }
        public static int TrafficCount { get { return TC[Level]; } }
        public static bool InteriorLights { get { return Level >= 1; } }
        public static bool StreetLights { get { return Level >= 2; } }
        public static event System.Action Changed;

        public static int Auto()
        {
            int mem = SystemInfo.systemMemorySize;
            return mem < 4000 ? 0 : (mem < 6500 ? 1 : 2);
        }
        public static void Next() { Level = (Level + 1) % 4; Apply(); if (Hud.I != null) Hud.I.Toast("Graficos: " + Names[Level], 1.5f); }
        public static void NextFps() { Fps = Fps == 30 ? 45 : (Fps == 45 ? 60 : 30); Apply(); if (Hud.I != null) Hud.I.Toast("Limite de FPS: " + Fps, 1.5f); }

        public static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps;
            QualitySettings.pixelLightCount = new[] { 1, 2, 4, 6 }[Level];
            QualitySettings.lodBias = new[] { .6f, .8f, 1f, 1.5f }[Level];
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = RS[Level];
                urp.shadowDistance = SD[Level];
                urp.shadowCascadeCount = Level >= 2 ? 3 : (Level == 1 ? 2 : 1);
                urp.msaaSampleCount = Level >= 3 ? 4 : (Level == 2 ? 2 : 1);
            }
            if (DayNightWeather.Sun != null)
                DayNightWeather.Sun.shadows = Level == 0 ? LightShadows.None : (Level == 1 ? LightShadows.Hard : LightShadows.Soft);
            BlockStreamer.LoadRadius = LR[Level];
            if (Changed != null) Changed();
        }
    }
}
