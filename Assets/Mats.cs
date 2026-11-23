using System.Collections.Generic;
using UnityEngine;

namespace Bodycam
{
    /// <summary>Texturas procedurais (sem depender de assets externos). Troque por texturas PBR reais depois.</summary>
    public static class ProcTex
    {
        public static Texture2D Make(int w, int h, System.Func<int, int, Color> f, bool mip = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mip);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear; t.anisoLevel = 4;
            t.Apply(mip);
            return t;
        }
        static float P(float x, float y, float s, float o = 0f) { return Mathf.PerlinNoise(x * s + o, y * s + o); }

        public static Texture2D Noise(Color c, float amt, int size = 128, float scale = 0.07f)
        {
            return Make(size, size, (x, y) =>
            {
                float n = (P(x, y, scale) * 0.6f + P(x, y, scale * 4f, 50f) * 0.4f - 0.5f) * amt;
                float g = (Random.value - 0.5f) * amt * 0.25f;
                return new Color(c.r + n + g, c.g + n + g, c.b + n + g, 1f);
            });
        }
        public static Texture2D Brick(Color c)
        {
            return Make(128, 128, (x, y) =>
            {
                int row = y / 16; int off = (row % 2) * 16; int bx = (x + off) % 32;
                bool mortar = (y % 16) < 2 || bx < 2;
                float n = P(x, y, 0.2f) * 0.2f;
                return mortar ? new Color(.6f, .6f, .58f) : new Color(c.r + n - .1f, c.g + n * .5f - .05f, c.b + n * .5f - .05f);
            });
        }
        public static Texture2D Facade(Color wall, Color glass)
        {
            return Make(128, 128, (x, y) =>
            {
                int cx = x % 32, cy = y % 32;
                bool win = cx > 6 && cx < 26 && cy > 8 && cy < 26;
                float n = P(x, y, 0.15f) * 0.08f;
                if (win) { float g = P(x, y, 0.1f, 9f) * 0.2f; return new Color(glass.r + g, glass.g + g, glass.b + g, 1f); }
                return new Color(wall.r + n, wall.g + n, wall.b + n, 1f);
            });
        }
        public static Texture2D Road()
        {
            return Make(64, 64, (x, y) =>
            {
                float n = (Random.value - .5f) * .05f + P(x, y, 0.1f) * .06f;
                var c = new Color(.14f + n, .14f + n, .15f + n, 1f);
                if ((x >= 3 && x <= 4) || (x >= 59 && x <= 60)) c = new Color(.85f, .85f, .85f, 1f);
                if (x >= 30 && x <= 33 && y < 32) c = new Color(.9f, .75f, .15f, 1f);
                return c;
            });
        }
        /// <summary>mode 0 = circulo duro (UI), 1 = fumaca macia, 2 = risco de chuva</summary>
        public static Texture2D Soft(int s, int mode)
        {
            return Make(s, s, (x, y) =>
            {
                float dx = (x - s / 2f) / (s / 2f), dy = (y - s / 2f) / (s / 2f);
                float a;
                if (mode == 0) { float r = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01((1f - r) * s * 0.5f); }
                else if (mode == 1) { float r = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01(1f - r); a *= a; }
                else { a = Mathf.Clamp01(1f - Mathf.Abs(dx) * 5f) * Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(dy), 4f)); }
                return new Color(1f, 1f, 1f, a);
            }, false);
        }
    }

    public static class Mats
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit;
        public static Material Asphalt, Road, Concrete, Grass, Brick, Facade, Metal, Wood, Glass, Window, Lamp, Roof, Leaf, Bark, Skin, Rubber, RedLed, BlueLed, LightV, LightH;

        public static Material New(Color c, float smooth = 0.2f, float metal = 0f, Texture tex = null)
        {
            if (lit == null) { lit = Shader.Find("Universal Render Pipeline/Lit"); if (lit == null) lit = Shader.Find("Standard"); }
            var m = new Material(lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
            m.enableInstancing = true;
            return m;
        }
        public static Material Paint(Color c, float smooth = 0.5f, float metal = 0.1f)
        {
            string k = "p" + ColorUtility.ToHtmlStringRGB(c) + smooth + metal;
            Material m;
            if (!cache.TryGetValue(k, out m)) { m = New(c, smooth, metal); cache[k] = m; }
            return m;
        }
        public static void Glow(Material m, Color e)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", e);
        }
        public static Material Sprite(Texture tex)
        {
            var sh = Shader.Find("Sprites/Default");
            var m = new Material(sh); m.mainTexture = tex; return m;
        }
        /// <summary>Chuva deixa o asfalto/concreto molhado (reflexo mais forte).</summary>
        public static void SetWet(float w)
        {
            Road.SetFloat("_Smoothness", Mathf.Lerp(.35f, .92f, w));
            Asphalt.SetFloat("_Smoothness", Mathf.Lerp(.4f, .9f, w));
            Concrete.SetFloat("_Smoothness", Mathf.Lerp(.12f, .65f, w));
        }
        public static void Init()
        {
            Asphalt = New(Color.white, .4f, 0, ProcTex.Noise(new Color(.17f, .17f, .18f), .08f));
            Road = New(Color.white, .4f, 0, ProcTex.Road());
            Concrete = New(Color.white, .12f, 0, ProcTex.Noise(new Color(.62f, .62f, .6f), .1f));
            Grass = New(Color.white, .05f, 0, ProcTex.Noise(new Color(.22f, .4f, .15f), .2f, 128, .1f));
            Brick = New(Color.white, .1f, 0, ProcTex.Brick(new Color(.55f, .28f, .2f)));
            Facade = New(Color.white, .5f, 0, ProcTex.Facade(new Color(.5f, .5f, .52f), new Color(.12f, .18f, .25f)));
            Metal = New(new Color(.6f, .62f, .65f), .7f, .8f);
            Wood = New(Color.white, .2f, 0, ProcTex.Noise(new Color(.45f, .3f, .18f), .15f));
            Glass = New(new Color(.1f, .16f, .2f), .95f, 0);
            Window = New(new Color(.08f, .12f, .18f), .95f, 0);
            Lamp = New(new Color(.9f, .9f, .85f), .3f, 0);
            Roof = New(Color.white, .2f, 0, ProcTex.Noise(new Color(.28f, .24f, .22f), .1f));
            Leaf = New(Color.white, .1f, 0, ProcTex.Noise(new Color(.12f, .32f, .1f), .25f, 64, .15f));
            Bark = New(Color.white, .1f, 0, ProcTex.Noise(new Color(.25f, .17f, .1f), .15f));
            Skin = New(new Color(.8f, .62f, .5f), .3f, 0);
            Rubber = New(new Color(.05f, .05f, .05f), .2f, 0);
            RedLed = New(Color.red, .5f, 0); Glow(RedLed, new Color(3f, 0f, 0f));
            BlueLed = New(Color.blue, .5f, 0); Glow(BlueLed, new Color(0f, .3f, 3f));
            LightV = New(Color.black, .5f, 0); LightH = New(Color.black, .5f, 0);
        }
    }

    /// <summary>Helpers de geometria. Cada caixa gera sua propria malha (UV em metros) para permitir static batching.</summary>
    public static class W
    {
        public static readonly List<GameObject> Pending = new List<GameObject>();

        static void Face(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 c, Vector3 u, Vector3 w, Vector3 nr)
        {
            int b = v.Count;
            v.Add(c - u - w); v.Add(c + u - w); v.Add(c + u + w); v.Add(c - u + w);
            for (int i = 0; i < 4; i++) n.Add(nr);
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }

        public static Mesh CubeMesh(Vector3 s, float tile, bool road)
        {
            if (tile <= 0f) tile = 1f;
            float hx = s.x / 2f, hy = s.y / 2f, hz = s.z / 2f;
            var v = new List<Vector3>(24); var n = new List<Vector3>(24); var t = new List<int>(36);
            Vector3 X = new Vector3(hx, 0, 0), Y = new Vector3(0, hy, 0), Z = new Vector3(0, 0, hz);
            Face(v, n, t, Z, X, Y, Vector3.forward);
            Face(v, n, t, -Z, Y, X, Vector3.back);
            Face(v, n, t, X, Y, Z, Vector3.right);
            Face(v, n, t, -X, Z, Y, Vector3.left);
            Face(v, n, t, Y, Z, X, Vector3.up);
            Face(v, n, t, -Y, X, Z, Vector3.down);
            var uv = new Vector2[24];
            for (int i = 0; i < 24; i++)
            {
                Vector3 p = v[i], nn = n[i];
                float ax = Mathf.Abs(nn.x), ay = Mathf.Abs(nn.y), az = Mathf.Abs(nn.z);
                Vector2 q;
                if (road) q = ay > .5f ? new Vector2(p.x / s.x + .5f, p.z / 12f) : Vector2.zero;
                else
                {
                    if (az > .5f) q = new Vector2(p.x, p.y); else if (ax > .5f) q = new Vector2(p.z, p.y); else q = new Vector2(p.x, p.z);
                    q /= tile;
                }
                uv[i] = q;
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        public static GameObject Box(Transform p, string name, Vector3 lp, Vector3 size, Material mat, float tile = 4f,
            bool collider = true, bool stat = true, Vector3? euler = null, bool road = false)
        {
            var g = new GameObject(name); var t = g.transform;
            t.SetParent(p, false); t.localPosition = lp;
            if (euler.HasValue) t.localEulerAngles = euler.Value;
            g.AddComponent<MeshFilter>().sharedMesh = CubeMesh(size, tile, road);
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<BoxCollider>().size = size;
            if (stat) { g.isStatic = true; Pending.Add(g); }
            return g;
        }

        public static GameObject Prim(PrimitiveType pt, Transform p, string name, Vector3 lp, Vector3 scale, Material mat, bool collider = true)
        {
            var g = GameObject.CreatePrimitive(pt); g.name = name;
            g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            return g;
        }

        public static void NoShadow(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public static void Flush(GameObject root)
        {
            if (Pending.Count > 0)
            {
                try { StaticBatchingUtility.Combine(Pending.ToArray(), root); } catch { }
                Pending.Clear();
            }
        }
    }
}
