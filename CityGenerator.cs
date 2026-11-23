using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Bodycam
{
    public enum Kind { House, Shop, Market, Police, Hospital, Workshop, Gas, Warehouse, Tower }

    public class Door : MonoBehaviour
    {
        public bool open; float ang; AudioSource au;
        void Awake() { au = ProcAudio.Source(gameObject, ProcAudio.Thud(), false, .7f, 25f); }
        public void Toggle() { open = !open; au.Play(); }
        void Update()
        {
            ang = Mathf.MoveTowards(ang, open ? 95f : 0f, 200f * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0, ang, 0);
        }
    }

    public class Pickable : MonoBehaviour
    {
        public string itemName = "Objeto";
        Rigidbody rb; Collider col; Vector3 home;
        void Awake() { rb = GetComponent<Rigidbody>(); col = GetComponent<Collider>(); home = transform.position; }
        public void Carry(Transform hand)
        {
            rb.isKinematic = true; col.enabled = false;
            transform.SetParent(hand, false); transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity;
        }
        public void Drop(Vector3 fwd)
        {
            transform.SetParent(null, true); col.enabled = true; rb.isKinematic = false; rb.velocity = fwd * 2f;
        }
        void Update() { if (transform.parent == null && transform.position.y < -5f) { transform.position = home + Vector3.up; rb.velocity = Vector3.zero; } }
    }

    /// <summary>Cria blocos da cidade sob demanda e desativa os distantes (carregamento inteligente).</summary>
    public class BlockStreamer : MonoBehaviour
    {
        public static float LoadRadius = 140f;
        public Transform Target;
        class Blk { public Vector3 c; public float ext; public System.Action<Transform> build; public GameObject go; public bool on; }
        readonly List<Blk> blks = new List<Blk>();
        float t;
        public void Register(Vector3 c, float ext, System.Action<Transform> build) { blks.Add(new Blk { c = c, ext = ext, build = build }); }
        void Update() { t -= Time.deltaTime; if (t > 0f || Target == null) return; t = .4f; Tick(false); }
        public void Tick(bool all)
        {
            Vector3 p = Target.position; int budget = all ? 999 : 1;
            foreach (var b in blks)
            {
                float dx = b.c.x - p.x, dz = b.c.z - p.z, r = LoadRadius + b.ext;
                if (dx * dx + dz * dz < r * r)
                {
                    if (b.go == null)
                    {
                        if (budget <= 0) continue;
                        budget--;
                        b.go = new GameObject("Block"); b.go.transform.SetParent(transform, false); b.go.transform.position = b.c;
                        b.build(b.go.transform); W.Flush(b.go); b.on = true;
                    }
                    else if (!b.on) { b.go.SetActive(true); b.on = true; }
                }
                else if (b.go != null && b.on) { b.go.SetActive(false); b.on = false; }
            }
        }
    }

    public class CityGenerator : MonoBehaviour
    {
        public const float Pitch = 80f, RoadW = 14f, BlockS = 66f;
        public const int N = 7;
        // R residencial, C comercial, P delegacia, H hospital, G posto, M mercado, I industrial, W oficina, K parque, F floresta
        static readonly string[] Map = { "FFFFFFF", "FRRCPRF", "FRHCCRF", "FIIGCMF", "FWIIRKF", "FRRCRKF", "FFFFFFF" };
        static readonly Dictionary<string, Vector3> anchors = new Dictionary<string, Vector3>();
        public BlockStreamer Streamer;

        public static Vector3 SpawnPos { get { return new Vector3(80f, 1f, 122f); } }
        public static Vector3 Node(int kx, int kz) { return new Vector3((kx - 3.5f) * Pitch, .05f, (3.5f - kz) * Pitch); }
        public static Vector3 BlockCenter(int i, int m) { return new Vector3((i - 3) * Pitch, 0, (3 - m) * Pitch); }
        public static Vector3 Anchor(string k) { Vector3 v; return anchors.TryGetValue(k, out v) ? v : Vector3.zero; }

        public void Generate()
        {
            Streamer = gameObject.AddComponent<BlockStreamer>();
            var g = new GameObject("Static"); g.transform.SetParent(transform, false);
            W.Box(g.transform, "Ground", new Vector3(0, -.5f, 0), new Vector3(1300, 1, 1300), Mats.Grass, 8f);
            float lim = 640f;
            for (int s = 0; s < 4; s++)
            {
                Vector3 pos = s < 2 ? new Vector3(s == 0 ? lim : -lim, 10, 0) : new Vector3(0, 10, s == 2 ? lim : -lim);
                Vector3 sz = s < 2 ? new Vector3(2, 20, 1300) : new Vector3(1300, 20, 2);
                var wl = W.Box(g.transform, "Bound", pos, sz, Mats.Concrete, 4f); wl.GetComponent<MeshRenderer>().enabled = false;
            }
            float len = N * Pitch + RoadW;
            for (int k = 0; k <= N; k++)
            {
                float x = (k - 3.5f) * Pitch;
                W.Box(g.transform, "RoadV" + k, new Vector3(x, 0, 0), new Vector3(RoadW, .1f, len), Mats.Road, 4f, true, true, null, true);
                W.Box(g.transform, "RoadH" + k, new Vector3(0, 0, x), new Vector3(RoadW, .1f, len), Mats.Road, 4f, true, true, new Vector3(0, 90, 0), true);
            }
            for (int kx = 0; kx <= N; kx++) for (int kz = 0; kz <= N; kz++)
                    W.Box(g.transform, "X", new Vector3((kx - 3.5f) * Pitch, .01f, (3.5f - kz) * Pitch), new Vector3(RoadW, .1f, RoadW), Mats.Asphalt, 6f);
            W.Box(g.transform, "RuralRoad", new Vector3(40, 0, -437), new Vector3(8, .1f, 300), Mats.Road, 4f, true, true, null, true);
            W.Flush(g);

            for (int m = 0; m < N; m++)
                for (int i = 0; i < N; i++)
                {
                    char t = Map[m][i]; int ii = i, mm = m; char tt = t;
                    var c = BlockCenter(i, m);
                    if (t != 'F' && !anchors.ContainsKey(t.ToString())) anchors[t.ToString()] = c;
                    Streamer.Register(c, 45f, b => BuildBlock(b, tt, ii, mm));
                }
            Streamer.Register(new Vector3(40, 0, -440), 160f, BuildRural);
        }

        // ---------- helpers de construcao ----------
        static void MakeTree(Transform b, Vector3 lp, System.Random rng)
        {
            float h = 4f + (float)rng.NextDouble() * 3f, s = .8f + (float)rng.NextDouble() * .6f;
            W.Box(b, "Trunk", lp + new Vector3(0, h / 2f, 0), new Vector3(.4f, h, .4f), Mats.Bark, 2f);
            var cr = W.Prim(PrimitiveType.Sphere, b, "Crown", lp + new Vector3(0, h + 1f, 0), new Vector3(3.4f * s, 3.8f * s, 3.4f * s), Mats.Leaf, false);
            cr.isStatic = false;
        }

        static void MakeLamp(Transform b, Vector3 lp)
        {
            W.Box(b, "Pole", lp + new Vector3(0, 3f, 0), new Vector3(.15f, 6f, .15f), Mats.Metal, 2f);
            W.Box(b, "Head", lp + new Vector3(0, 6.1f, 0), new Vector3(.6f, .25f, .6f), Mats.Lamp, 2f, false);
            if (Quality.StreetLights)
            {
                var lg = new GameObject("Light").AddComponent<Light>(); lg.transform.SetParent(b, false); lg.transform.localPosition = lp + new Vector3(0, 5.8f, 0);
                lg.type = LightType.Point; lg.range = 14f; lg.intensity = 2f; lg.color = new Color(1f, .85f, .6f); lg.shadows = LightShadows.None;
                lg.enabled = false; DayNightWeather.Lamps.Add(lg);
            }
        }

        static float FaceYaw(float cx, float cz)
        {
            if (Mathf.Abs(cz) >= Mathf.Abs(cx)) return cz < 0 ? 0f : 180f;
            return cx > 0 ? -90f : 90f;
        }

        static void Item(Transform r, string name, Vector3 lp, Vector3 size, Color col)
        {
            var g = W.Box(r, name, lp, size, Mats.Paint(col, .4f, 0), 1f, true, false);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 1f;
            g.AddComponent<Pickable>().itemName = name;
        }

        Transform Building(Transform blk, string name, Vector3 c, float w, float d, float h, float yaw, Kind kind, Material wall,
            string sign, Color signCol, System.Random rng, float dw = 1.6f, float dh = 2.3f, bool hollow = true)
        {
            var root = new GameObject(name).transform; root.SetParent(blk, false);
            root.localPosition = new Vector3(c.x, .17f, c.z); root.localRotation = Quaternion.Euler(0, yaw, 0);
            float t = .25f, fz = -d / 2f + t / 2f;
            if (!hollow)
            {
                W.Box(root, "Mass", new Vector3(0, h / 2f, 0), new Vector3(w, h, d), wall, 5f);
                W.Box(root, "Roof", new Vector3(0, h + .1f, 0), new Vector3(w + .4f, .2f, d + .4f), Mats.Roof, 4f);
                return root;
            }
            W.Box(root, "Floor", new Vector3(0, 0, 0), new Vector3(w, .1f, d), Mats.Concrete, 4f);
            W.Box(root, "WallB", new Vector3(0, h / 2f, d / 2f - t / 2f), new Vector3(w, h, t), wall, 4f);
            W.Box(root, "WallL", new Vector3(-w / 2f + t / 2f, h / 2f, 0), new Vector3(t, h, d), wall, 4f);
            W.Box(root, "WallR", new Vector3(w / 2f - t / 2f, h / 2f, 0), new Vector3(t, h, d), wall, 4f);
            float sx = (w - dw) / 2f;
            W.Box(root, "FrontL", new Vector3(-dw / 2f - sx / 2f, h / 2f, fz), new Vector3(sx, h, t), wall, 4f);
            W.Box(root, "FrontR", new Vector3(dw / 2f + sx / 2f, h / 2f, fz), new Vector3(sx, h, t), wall, 4f);
            W.Box(root, "Lintel", new Vector3(0, dh + (h - dh) / 2f, fz), new Vector3(dw, h - dh, t), wall, 4f);
            W.Box(root, "Roof", new Vector3(0, h + .1f, 0), new Vector3(w + .6f, .2f, d + .6f), Mats.Roof, 4f);
            // porta
            var pv = new GameObject("Door").transform; pv.SetParent(root, false); pv.localPosition = new Vector3(-dw / 2f, .05f, fz);
            pv.gameObject.AddComponent<Door>();
            W.Box(pv, "Leaf", new Vector3(dw / 2f, dh / 2f, 0), new Vector3(dw - .04f, dh - .1f, .08f), Mats.Wood, 2f, true, false);
            // janelas pintadas (emissivas a noite)
            int nz = Mathf.Max(1, (int)(d / 5f)), nx = Mathf.Max(1, (int)(w / 5f));
            for (int i = 0; i < nz; i++)
            {
                float z = -d / 2f + (i + .5f) * d / nz; if (z < -d / 2f + 1.5f) continue;
                W.Box(root, "Win", new Vector3(w / 2f + .01f, 1.7f, z), new Vector3(.05f, 1.1f, 1.5f), Mats.Window, 1f, false);
                W.Box(root, "Win", new Vector3(-w / 2f - .01f, 1.7f, z), new Vector3(.05f, 1.1f, 1.5f), Mats.Window, 1f, false);
            }
            for (int i = 0; i < nx; i++)
                W.Box(root, "Win", new Vector3(-w / 2f + (i + .5f) * w / nx, 1.7f, d / 2f + .01f), new Vector3(1.5f, 1.1f, .05f), Mats.Window, 1f, false);
            if (sx > 3f && h > 3f)
            {
                W.Box(root, "Win", new Vector3(-dw / 2f - sx / 2f, 1.6f, fz - t / 2f - .01f), new Vector3(1.5f, 1.1f, .05f), Mats.Window, 1f, false);
                W.Box(root, "Win", new Vector3(dw / 2f + sx / 2f, 1.6f, fz - t / 2f - .01f), new Vector3(1.5f, 1.1f, .05f), Mats.Window, 1f, false);
            }
            // placa
            if (!string.IsNullOrEmpty(sign))
            {
                W.Box(root, "SignBg", new Vector3(0, dh + .5f, -d / 2f - .03f), new Vector3(Mathf.Min(w - 1f, 6f), .8f, .05f), Mats.Paint(signCol, .3f, 0), 1f, false);
                var sg = new GameObject("Sign"); sg.transform.SetParent(root, false); sg.transform.localPosition = new Vector3(0, dh + .5f, -d / 2f - .08f);
                var tm = sg.AddComponent<TextMesh>(); tm.text = sign; tm.anchor = TextAnchor.MiddleCenter; tm.fontSize = 48; tm.characterSize = .1f; tm.color = Color.white;
                tm.font = Hud.F; sg.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            }
            if (Quality.InteriorLights)
            {
                var lg = new GameObject("Light").AddComponent<Light>(); lg.transform.SetParent(root, false); lg.transform.localPosition = new Vector3(0, h - .6f, 0);
                lg.type = LightType.Point; lg.range = Mathf.Max(w, d) * .8f; lg.intensity = 1.1f; lg.color = new Color(1f, .93f, .8f); lg.shadows = LightShadows.None;
            }
            Furnish(root, kind, w, d, rng);
            return root;
        }

        static void Furnish(Transform r, Kind k, float w, float d, System.Random rng)
        {
            Material wood = Mats.Wood, white = Mats.Paint(new Color(.85f, .85f, .85f), .3f, 0), metal = Mats.Metal;
            switch (k)
            {
                case Kind.House:
                    W.Box(r, "Bed", new Vector3(w / 2f - 1.5f, .35f, d / 2f - 1.5f), new Vector3(1.8f, .5f, 2.2f), Mats.Paint(new Color(.3f, .35f, .5f)));
                    W.Box(r, "Table", new Vector3(-w / 4f, .4f, d / 4f), new Vector3(1.4f, .8f, .9f), wood);
                    W.Box(r, "Sofa", new Vector3(-w / 2f + 1.2f, .4f, -d / 4f), new Vector3(.9f, .8f, 2.2f), Mats.Paint(new Color(.35f, .3f, .28f)));
                    W.Box(r, "Fridge", new Vector3(-w / 2f + .6f, .9f, d / 2f - .8f), new Vector3(.7f, 1.8f, .7f), white);
                    Item(r, "Radio", new Vector3(-w / 4f, .9f, d / 4f), new Vector3(.3f, .2f, .15f), new Color(.2f, .2f, .2f));
                    break;
                case Kind.Market: case Kind.Shop: case Kind.Gas:
                    for (int i = -1; i <= 1; i++) W.Box(r, "Shelf", new Vector3(i * (w / 4f), 1f, d / 6f), new Vector3(.6f, 2f, d / 2.2f), metal);
                    W.Box(r, "Counter", new Vector3(w / 2f - 2f, .55f, -d / 2f + 2.5f), new Vector3(3f, 1.1f, .8f), wood);
                    if (k == Kind.Market) Item(r, "Encomenda", new Vector3(w / 8f, .25f, -d / 6f), new Vector3(.45f, .4f, .45f), new Color(.7f, .5f, .25f));
                    if (k == Kind.Gas) Item(r, "Kit medico", new Vector3(w / 8f, .25f, -d / 6f), new Vector3(.4f, .25f, .3f), new Color(.9f, .1f, .1f));
                    break;
                case Kind.Hospital:
                    for (int i = 0; i < 4; i++) W.Box(r, "Bed", new Vector3(-w / 2f + 3f + i * (w - 6f) / 3f, .5f, d / 2f - 2.5f), new Vector3(1f, .6f, 2.1f), white);
                    W.Box(r, "Desk", new Vector3(0, .55f, -d / 2f + 3f), new Vector3(4f, 1.1f, .9f), white);
                    break;
                case Kind.Police:
                    W.Box(r, "Desk", new Vector3(-w / 4f, .5f, 1f), new Vector3(2f, 1f, .9f), wood);
                    W.Box(r, "Desk", new Vector3(w / 4f, .5f, 1f), new Vector3(2f, 1f, .9f), wood);
                    W.Box(r, "Counter", new Vector3(0, .55f, -d / 2f + 3f), new Vector3(5f, 1.1f, .8f), wood);
                    break;
                case Kind.Workshop:
                    W.Box(r, "Lift", new Vector3(-w / 4f, .3f, 0), new Vector3(2.2f, .6f, 4.5f), metal);
                    W.Box(r, "Bench", new Vector3(w / 2f - 1f, .5f, d / 4f), new Vector3(.9f, 1f, 4f), metal);
                    break;
                case Kind.Warehouse:
                    for (int i = 0; i < 8; i++)
                        W.Box(r, "Crate", new Vector3((float)(rng.NextDouble() - .5) * (w - 6f), .75f, (float)(rng.NextDouble() * .5) * (d - 6f) + 2f), new Vector3(1.5f, 1.5f, 1.5f), wood);
                    Item(r, "Chave inglesa", new Vector3(w / 4f, .25f, -d / 4f), new Vector3(.5f, .08f, .12f), new Color(.7f, .7f, .75f));
                    break;
            }
        }

        // ---------- blocos ----------
        void BuildBlock(Transform b, char type, int bi, int bm)
        {
            var rng = new System.Random(bi * 31 + bm * 7 + 13);
            Vector3 c = b.position;
            System.Func<float, float, float> R = (lo, hi) => (float)(lo + rng.NextDouble() * (hi - lo));
            Color[] pal = { new Color(.8f, .75f, .65f), new Color(.7f, .72f, .75f), new Color(.75f, .6f, .5f), new Color(.6f, .65f, .55f) };
            if (type == 'F')
            {
                for (int i = 0; i < Quality.TreesPerBlock; i++) MakeTree(b, new Vector3(R(-28, 28), .17f, R(-28, 28)), rng);
                return;
            }
            W.Box(b, "Walk", new Vector3(0, .05f, 0), new Vector3(BlockS, .2f, BlockS), Mats.Concrete, 6f);
            Material lot = (type == 'R' || type == 'K') ? Mats.Grass : (type == 'I' || type == 'G') ? Mats.Asphalt : Mats.Concrete;
            W.Box(b, "Lot", new Vector3(0, .151f, 0), new Vector3(60, .04f, 60), lot, 6f);
            foreach (float sx in new[] { -30f, 30f }) foreach (float sz in new[] { -30f, 30f }) MakeLamp(b, new Vector3(sx, .17f, sz));

            switch (type)
            {
                case 'R':
                    for (int i = 0; i < 4; i++)
                    {
                        float cx = (i % 2 == 0 ? -15f : 15f), cz = (i < 2 ? -15f : 15f);
                        Building(b, "House", new Vector3(cx, 0, cz), 14, 12, 3.2f, FaceYaw(cx, cz), Kind.House,
                            i % 2 == 0 ? Mats.Brick : Mats.Paint(pal[rng.Next(4)], .1f, 0), "", Color.white, rng);
                    }
                    for (int i = 0; i < 3; i++) MakeTree(b, new Vector3(R(-8, 8), .17f, R(-8, 8)), rng);
                    break;
                case 'K':
                    for (int i = 0; i < 14; i++) MakeTree(b, new Vector3(R(-26, 26), .17f, R(-26, 26)), rng);
                    for (int i = 0; i < 4; i++) W.Box(b, "Bench", new Vector3(-9f + i * 6f, .45f, 0), new Vector3(1.8f, .5f, .5f), Mats.Wood, 1f);
                    break;
                case 'C':
                    Building(b, "ShopA", new Vector3(-18, 0, -18), 20, 16, 4f, 0, Kind.Shop, Mats.Brick, "LOJA", new Color(.1f, .4f, .7f), rng);
                    Building(b, "ShopB", new Vector3(18, 0, -18), 20, 16, 4f, 0, Kind.Shop, Mats.Paint(pal[1], .1f, 0), "CAFE", new Color(.6f, .3f, .1f), rng);
                    Building(b, "Tower", new Vector3(0, 0, 16), 24, 20, R(14, 24), 180, Kind.Tower, Mats.Facade, "", Color.white, rng, 1.6f, 2.3f, false);
                    break;
                case 'P':
                    Building(b, "Police", new Vector3(0, 0, -6), 30, 18, 4.2f, 0, Kind.Police, Mats.Paint(new Color(.6f, .65f, .75f), .1f, 0), "DELEGACIA", new Color(.05f, .15f, .5f), rng, 2f);
                    VehicleBuilder.Build(VType.Police, new Color(.05f, .05f, .08f), c + new Vector3(-8, .2f, -23), 90, b);
                    VehicleBuilder.Build(VType.Police, new Color(.05f, .05f, .08f), c + new Vector3(8, .2f, -23), 90, b);
                    break;
                case 'H':
                    Building(b, "Hospital", new Vector3(0, 0, -4), 40, 22, 4.5f, 0, Kind.Hospital, Mats.Paint(new Color(.88f, .9f, .92f), .1f, 0), "HOSPITAL", new Color(.7f, .1f, .1f), rng, 2.4f);
                    VehicleBuilder.Build(VType.Pickup, Color.white, c + new Vector3(-12, .2f, -23), 90, b);
                    break;
                case 'G':
                    Building(b, "GasStore", new Vector3(0, 0, 18), 14, 9, 3.4f, 0, Kind.Gas, Mats.Paint(pal[0], .1f, 0), "POSTO", new Color(.1f, .5f, .2f), rng);
                    foreach (float px in new[] { -7f, 7f }) foreach (float pz in new[] { -8f, 0f }) W.Box(b, "Post", new Vector3(px, 2.3f, pz), new Vector3(.3f, 4.6f, .3f), Mats.Metal, 2f);
                    W.Box(b, "Canopy", new Vector3(0, 4.8f, -4), new Vector3(17, .4f, 11), Mats.Paint(new Color(.9f, .9f, .9f), .4f, 0), 4f);
                    W.Box(b, "Pump", new Vector3(-3, .9f, -4), new Vector3(.6f, 1.4f, .6f), Mats.Paint(Color.red), 1f);
                    W.Box(b, "Pump", new Vector3(3, .9f, -4), new Vector3(.6f, 1.4f, .6f), Mats.Paint(Color.red), 1f);
                    VehicleBuilder.Build(VType.Car, new Color(.7f, .1f, .1f), c + new Vector3(-3, .2f, -10), 0, b);
                    break;
                case 'M':
                    Building(b, "Market", new Vector3(0, 0, 8), 36, 24, 4.6f, 0, Kind.Market, Mats.Paint(new Color(.85f, .85f, .75f), .1f, 0), "MERCADO", new Color(.8f, .4f, 0f), rng, 2.4f);
                    VehicleBuilder.Build(VType.Car, new Color(.1f, .3f, .6f), c + new Vector3(-15, .2f, -22), 0, b);
                    VehicleBuilder.Build(VType.Car, new Color(.8f, .8f, .8f), c + new Vector3(15, .2f, -22), 0, b);
                    break;
                case 'I':
                    Building(b, "Warehouse", new Vector3(0, 0, 6), 40, 30, 8f, 0, Kind.Warehouse, Mats.Paint(new Color(.55f, .58f, .6f), .3f, .4f), "INDUSTRIAL", new Color(.3f, .3f, .3f), rng, 5f, 4f);
                    for (int i = 0; i < 3; i++) W.Box(b, "Container", new Vector3(-22f + i * 7f, 1.3f, -24), new Vector3(6, 2.6f, 2.4f), Mats.Paint(new Color(R(.2f, .8f), R(.2f, .6f), .3f), .4f, .5f), 3f);
                    W.Prim(PrimitiveType.Cylinder, b, "Chimney", new Vector3(24, 10, -22), new Vector3(2.2f, 10, 2.2f), Mats.Brick);
                    break;
                case 'W':
                    Building(b, "Workshop", new Vector3(0, 0, 8), 26, 16, 5f, 0, Kind.Workshop, Mats.Paint(new Color(.5f, .5f, .55f), .2f, .2f), "OFICINA", new Color(.8f, .6f, 0f), rng, 4.5f, 3.6f);
                    VehicleBuilder.Build(VType.Car, new Color(.9f, .85f, .1f), c + new Vector3(-8, .2f, -16), 0, b);
                    VehicleBuilder.Build(VType.Pickup, new Color(.2f, .4f, .3f), c + new Vector3(8, .2f, -16), 0, b);
                    break;
            }
            NpcFactory.Populate(b, type, rng);
        }

        void BuildRural(Transform b)
        {
            var rng = new System.Random(99);
            int n = Quality.TreesPerBlock * 3;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() < .5 ? rng.NextDouble() * 25 - 38 : rng.NextDouble() * 25 + 13);
                MakeTree(b, new Vector3(x, .0f, (float)(rng.NextDouble() * 280 - 140)), rng);
            }
        }

        /// <summary>Veiculos estacionados perto do ponto de partida para testar logo.</summary>
        public static void SpawnStarterVehicles()
        {
            float z = 116.5f;
            VehicleBuilder.Build(VType.Car, new Color(.15f, .25f, .6f), new Vector3(66, .2f, z), 90, null);
            VehicleBuilder.Build(VType.Moto, new Color(.7f, .1f, .1f), new Vector3(74, .2f, z), 90, null);
            VehicleBuilder.Build(VType.Bike, new Color(.1f, .5f, .2f), new Vector3(82, .2f, z), 90, null);
            VehicleBuilder.Build(VType.Pickup, new Color(.6f, .6f, .6f), new Vector3(92, .2f, z), 90, null);
            VehicleBuilder.Build(VType.Truck, new Color(.8f, .5f, .1f), new Vector3(106, .2f, z), 90, null);
        }
    }
}
