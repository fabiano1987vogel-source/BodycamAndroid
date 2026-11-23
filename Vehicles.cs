using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Bodycam
{
    public enum VType { Car, Moto, Pickup, Truck, Police, Bike }

    /// <summary>Fisica arcade: Rigidbody com rotacao X/Z travada, velocidade local controlada por codigo.</summary>
    public class SimpleVehicle : MonoBehaviour
    {
        public static readonly List<SimpleVehicle> All = new List<SimpleVehicle>();
        public VType type; public Rigidbody rb; public Transform seat;
        public bool Occupied, Enterable = true, IsTraffic, sirenOn;
        public Vector2 input; public bool brake;
        public float maxSpeed = 28f, accel = 12f, turn = 75f, grip = 7f, eyeHeight = .65f, halfWidth = .9f, wheelR = .35f, trafficSpeed;
        public Light[] head = new Light[0], siren = new Light[0];
        public Transform[] wheels = new Transform[0];
        AudioSource eng, sir;

        public float ForwardSpeed { get { return IsTraffic ? trafficSpeed : (rb ? Vector3.Dot(rb.velocity, transform.forward) : 0f); } }
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void Setup()
        {
            eng = ProcAudio.Source(gameObject, ProcAudio.Engine(), true, .4f, 40f);
            if (type == VType.Police) sir = ProcAudio.Source(gameObject, ProcAudio.Siren(), true, .6f, 120f);
        }
        public void SetDriver(bool b) { Occupied = b; if (!b) { input = Vector2.zero; } }
        public void ToggleSiren()
        {
            sirenOn = !sirenOn;
            if (sir != null) { if (sirenOn) { sir.Play(); NpcAgent.AlertAround(transform.position, 25f); } else sir.Stop(); }
            foreach (var l in siren) l.enabled = sirenOn;
        }

        void FixedUpdate()
        {
            if (rb == null || rb.isKinematic) return;
            Vector2 inp = Occupied ? input : Vector2.zero;
            bool br = brake || !Occupied;
            Vector3 lv = transform.InverseTransformDirection(rb.velocity);
            bool grounded = Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down, .9f);
            float dt = Time.fixedDeltaTime;
            if (grounded)
            {
                float target = inp.y * (inp.y > 0 ? maxSpeed : maxSpeed * .35f);
                float rate = Mathf.Abs(inp.y) > .05f ? accel : 5f;
                if (br) { rate = 30f; target = 0f; }
                lv.z = Mathf.MoveTowards(lv.z, target, rate * dt);
                lv.x = Mathf.Lerp(lv.x, 0f, Mathf.Clamp01(grip * dt));
                rb.angularVelocity = Vector3.up * (inp.x * turn * Mathf.Deg2Rad * Mathf.Clamp(lv.z / 5f, -1f, 1f));
            }
            rb.velocity = transform.TransformDirection(lv);
        }

        void Update()
        {
            float dt = Time.deltaTime, spd = ForwardSpeed;
            foreach (var w in wheels) if (w) w.Rotate(spd / wheelR * Mathf.Rad2Deg * dt, 0, 0, Space.Self);
            bool dark = DayNightWeather.I != null && DayNightWeather.I.IsNight;
            bool on = dark && (Occupied || (IsTraffic && Quality.Level >= 2));
            foreach (var h in head) if (h) h.enabled = on;
            if (sirenOn && siren.Length == 2)
            {
                bool f = Mathf.Sin(Time.time * 14f) > 0f;
                siren[0].intensity = f ? 5f : 0f; siren[1].intensity = f ? 0f : 5f;
            }
            if (eng != null)
            {
                var pl = PlayerController.I;
                bool near = pl != null && (pl.transform.position - transform.position).sqrMagnitude < 2500f;
                if (Occupied || (Mathf.Abs(spd) > .5f && near))
                {
                    if (!eng.isPlaying) eng.Play();
                    eng.pitch = .7f + Mathf.Abs(spd) / maxSpeed * 1.6f; eng.volume = Occupied ? .5f : .3f;
                }
                else if (eng.isPlaying) eng.Stop();
            }
        }
    }

    public static class VehicleBuilder
    {
        static PhysicMaterial slick;

        public static SimpleVehicle Build(VType t, Color col, Vector3 pos, float yaw, Transform parent, bool drivable = true, bool kinematic = false)
        {
            if (slick == null) slick = new PhysicMaterial("slick") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicMaterialCombine.Minimum };
            var root = new GameObject(t + "_Vehicle"); var rt = root.transform;
            if (parent != null) rt.SetParent(parent, true);
            rt.position = pos; rt.rotation = Quaternion.Euler(0, yaw, 0);
            var v = root.AddComponent<SimpleVehicle>(); v.type = t; v.Enterable = drivable;
            Material body = Mats.Paint(col, .75f, .4f), glass = Mats.Glass, dark = Mats.Paint(new Color(.1f, .1f, .1f), .4f, .2f);
            System.Action<string, Vector3, Vector3, Material> B = (n, p, s, m) => W.Box(rt, n, p, s, m, 1f, false, false);
            var wp = new List<Vector3>(); float wr = .35f; float mass = 1200f;
            Vector3 seatP = new Vector3(-.4f, .5f, .1f), boxC = new Vector3(0, .95f, 0), boxS = new Vector3(1.8f, .9f, 4.2f);
            Vector3[] heads = { new Vector3(-.6f, .7f, 2.1f), new Vector3(.6f, .7f, 2.1f) };
            switch (t)
            {
                case VType.Car: case VType.Police:
                    B("Body", new Vector3(0, .62f, 0), new Vector3(1.8f, .55f, 4.3f), body);
                    B("Cabin", new Vector3(0, 1.15f, -.2f), new Vector3(1.6f, .6f, 2.2f), glass);
                    B("Roof", new Vector3(0, 1.47f, -.2f), new Vector3(1.6f, .06f, 2.2f), body);
                    foreach (float x in new[] { -.85f, .85f }) { wp.Add(new Vector3(x, wr, 1.3f)); wp.Add(new Vector3(x, wr, -1.3f)); }
                    v.halfWidth = .9f;
                    break;
                case VType.Pickup:
                    B("Body", new Vector3(0, .7f, 0), new Vector3(1.9f, .6f, 5.2f), body);
                    B("Cabin", new Vector3(0, 1.25f, .9f), new Vector3(1.7f, .6f, 1.8f), glass);
                    B("Roof", new Vector3(0, 1.57f, .9f), new Vector3(1.7f, .06f, 1.8f), body);
                    B("Bed", new Vector3(0, 1f, -1.6f), new Vector3(1.9f, .3f, 1.9f), dark);
                    foreach (float x in new[] { -.9f, .9f }) { wp.Add(new Vector3(x, wr, 1.6f)); wp.Add(new Vector3(x, wr, -1.6f)); }
                    seatP = new Vector3(-.45f, .55f, .9f); boxS = new Vector3(1.9f, .9f, 5.1f); v.maxSpeed = 26f; mass = 1800f; heads = new[] { new Vector3(-.6f, .8f, 2.6f), new Vector3(.6f, .8f, 2.6f) };
                    break;
                case VType.Truck:
                    wr = .55f; mass = 5000f;
                    B("Chassis", new Vector3(0, .85f, 0), new Vector3(2.2f, .5f, 7.5f), dark);
                    B("Cab", new Vector3(0, 1.9f, 2.4f), new Vector3(2.3f, 1.9f, 2.2f), body);
                    B("CabGlass", new Vector3(0, 2.2f, 3.51f), new Vector3(2f, .8f, .05f), glass);
                    B("Cargo", new Vector3(0, 2.3f, -1.2f), new Vector3(2.4f, 2.4f, 5f), Mats.Paint(new Color(.85f, .85f, .85f), .3f, .1f));
                    foreach (float x in new[] { -1f, 1f }) { wp.Add(new Vector3(x, wr, 2.6f)); wp.Add(new Vector3(x, wr, -.5f)); wp.Add(new Vector3(x, wr, -2.6f)); }
                    seatP = new Vector3(-.5f, 1.2f, 2.4f); boxC = new Vector3(0, 2.1f, 0); boxS = new Vector3(2.3f, 2.5f, 7.3f);
                    v.maxSpeed = 20f; v.accel = 7f; v.turn = 50f; v.halfWidth = 1.3f; v.eyeHeight = .9f;
                    heads = new[] { new Vector3(-.8f, 1.2f, 3.5f), new Vector3(.8f, 1.2f, 3.5f) };
                    break;
                case VType.Moto:
                    wr = .32f; mass = 200f;
                    B("Frame", new Vector3(0, .7f, 0), new Vector3(.3f, .45f, 1.5f), body);
                    B("Tank", new Vector3(0, .98f, .3f), new Vector3(.32f, .25f, .5f), body);
                    B("Seat", new Vector3(0, .93f, -.35f), new Vector3(.28f, .1f, .6f), dark);
                    B("Bar", new Vector3(0, 1.18f, .7f), new Vector3(.8f, .05f, .05f), Mats.Metal);
                    wp.Add(new Vector3(0, wr, .75f)); wp.Add(new Vector3(0, wr, -.75f));
                    seatP = new Vector3(0, .55f, -.2f); boxC = new Vector3(0, .9f, 0); boxS = new Vector3(.5f, .6f, 1.5f);
                    v.maxSpeed = 38f; v.accel = 18f; v.turn = 100f; v.halfWidth = .4f; v.eyeHeight = .62f; heads = new[] { new Vector3(0, 1f, .9f) };
                    break;
                default: // Bike
                    wr = .34f; mass = 90f;
                    B("Frame", new Vector3(0, .75f, 0), new Vector3(.05f, .05f, 1f), Mats.Metal);
                    B("Fork", new Vector3(0, .85f, .5f), new Vector3(.05f, .6f, .05f), Mats.Metal);
                    B("Bar", new Vector3(0, 1.15f, .5f), new Vector3(.6f, .04f, .04f), Mats.Metal);
                    B("Seat", new Vector3(0, .95f, -.3f), new Vector3(.12f, .06f, .3f), dark);
                    wp.Add(new Vector3(0, wr, .55f)); wp.Add(new Vector3(0, wr, -.55f));
                    seatP = new Vector3(0, .6f, -.1f); boxC = new Vector3(0, .8f, 0); boxS = new Vector3(.3f, .5f, .8f);
                    v.maxSpeed = 8f; v.accel = 4f; v.turn = 90f; v.halfWidth = .3f; v.eyeHeight = .65f; heads = new[] { new Vector3(0, 1f, .6f) };
                    break;
            }
            v.wheelR = wr;
            var ws = new List<Transform>();
            foreach (var p in wp)
            {
                var pv = new GameObject("Wheel").transform; pv.SetParent(rt, false); pv.localPosition = p;
                var cyl = W.Prim(PrimitiveType.Cylinder, pv, "Tire", Vector3.zero, new Vector3(wr * 2f, .12f, wr * 2f), Mats.Rubber, false);
                cyl.transform.localRotation = Quaternion.Euler(0, 0, 90);
                ws.Add(pv);
                var sc = root.AddComponent<SphereCollider>(); sc.center = p; sc.radius = wr; sc.sharedMaterial = slick;
            }
            v.wheels = ws.ToArray();
            var bc = root.AddComponent<BoxCollider>(); bc.center = boxC; bc.size = boxS; bc.sharedMaterial = slick;
            v.seat = new GameObject("Seat").transform; v.seat.SetParent(rt, false); v.seat.localPosition = seatP;
            var rb = root.AddComponent<Rigidbody>(); v.rb = rb; rb.mass = mass; rb.angularDrag = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.centerOfMass = new Vector3(0, .3f, 0); rb.isKinematic = kinematic;
            var hl = new List<Light>();
            foreach (var h in heads)
            {
                var l = new GameObject("Head").AddComponent<Light>(); l.transform.SetParent(rt, false); l.transform.localPosition = h;
                l.type = LightType.Spot; l.range = 28f; l.spotAngle = 65f; l.intensity = 2.5f; l.shadows = LightShadows.None; l.enabled = false; hl.Add(l);
            }
            v.head = hl.ToArray();
            if (t == VType.Police)
            {
                B("Stripe", new Vector3(0, .62f, 0), new Vector3(1.82f, .3f, 2f), Mats.Paint(Color.white, .5f, .1f));
                W.Box(rt, "LightR", new Vector3(-.3f, 1.55f, -.2f), new Vector3(.5f, .12f, .25f), Mats.RedLed, 1f, false, false);
                W.Box(rt, "LightB", new Vector3(.3f, 1.55f, -.2f), new Vector3(.5f, .12f, .25f), Mats.BlueLed, 1f, false, false);
                var sl = new Light[2];
                for (int i = 0; i < 2; i++)
                {
                    var l = new GameObject("Siren").AddComponent<Light>(); l.transform.SetParent(rt, false); l.transform.localPosition = new Vector3(i == 0 ? -.3f : .3f, 1.8f, -.2f);
                    l.type = LightType.Point; l.range = 14f; l.intensity = 0f; l.color = i == 0 ? Color.red : new Color(.1f, .3f, 1f); l.shadows = LightShadows.None; l.enabled = false; sl[i] = l;
                }
                v.siren = sl;
            }
            v.Setup();
            return v;
        }
    }

    public class TrafficCar : MonoBehaviour
    {
        static readonly int[] DX = { 1, -1, 0, 0 }, DZ = { 0, 0, 1, -1 };
        int cx, cz, nx, nz; float speed, stuck, ignore, recycleT;
        Vector3 target; SimpleVehicle veh;
        public const float MaxSp = 11f;

        public void Init(int ax, int az)
        {
            veh = GetComponent<SimpleVehicle>();
            cx = ax; cz = az; nx = ax; nz = az; PickNext();
            Vector3 dir = (CityGenerator.Node(nx, nz) - CityGenerator.Node(cx, cz)).normalized;
            transform.position = CityGenerator.Node(cx, cz) + Vector3.Cross(Vector3.up, dir) * 3.5f;
            transform.rotation = Quaternion.LookRotation(dir); speed = 0;
        }

        void PickNext()
        {
            int px = cx, pz = cz; cx = nx; cz = nz;
            var opts = new List<int>();
            for (int d = 0; d < 4; d++)
            {
                int ax = cx + DX[d], az = cz + DZ[d];
                if (ax < 1 || ax > 6 || az < 1 || az > 6) continue;
                if (ax == px && az == pz && !(px == cx && pz == cz)) continue;
                opts.Add(d);
            }
            if (opts.Count == 0) for (int d = 0; d < 4; d++) { int ax = cx + DX[d], az = cz + DZ[d]; if (ax >= 1 && ax <= 6 && az >= 1 && az <= 6) opts.Add(d); }
            int dd = opts[Random.Range(0, opts.Count)];
            nx = cx + DX[dd]; nz = cz + DZ[dd];
            Vector3 dir = (CityGenerator.Node(nx, nz) - CityGenerator.Node(cx, cz)).normalized;
            target = CityGenerator.Node(nx, nz) + Vector3.Cross(Vector3.up, dir) * 3.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position, to = target - pos; to.y = 0;
            float dist = to.magnitude;
            Vector3 dir = (CityGenerator.Node(nx, nz) - CityGenerator.Node(cx, cz)).normalized;
            float dNode = Vector3.Distance(pos, CityGenerator.Node(nx, nz));
            bool vertical = Mathf.Abs(dir.z) > .5f;
            int st = TrafficManager.State(vertical);
            float want = MaxSp;
            if (st != 0 && dNode < 16f && dNode > 6f) want = 0f;
            ignore -= dt;
            RaycastHit hit;
            if (ignore <= 0f && Physics.Raycast(pos + Vector3.up * .8f, transform.forward, out hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                want = 0f; stuck += dt;
                if (stuck > 6f) { ignore = 2f; stuck = 0f; }
            }
            else stuck = 0f;
            speed = Mathf.MoveTowards(speed, want, (want < speed ? 10f : 4f) * dt);
            if (to.sqrMagnitude > .001f)
            {
                transform.position = pos + to.normalized * speed * dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 90f * dt);
            }
            veh.trafficSpeed = speed;
            if (dist < 1.5f) PickNext();
            recycleT -= dt;
            if (recycleT <= 0f) { recycleT = 3f; Recycle(); }
        }

        void Recycle()
        {
            var pl = PlayerController.I; if (pl == null) return;
            if ((transform.position - pl.transform.position).sqrMagnitude < 300f * 300f) return;
            for (int tries = 0; tries < 20; tries++)
            {
                int ax = Random.Range(1, 7), az = Random.Range(1, 7);
                float d = (CityGenerator.Node(ax, az) - pl.transform.position).magnitude;
                if (d > 100f && d < 250f) { Init(ax, az); return; }
            }
        }
    }

    public class TrafficManager : MonoBehaviour
    {
        readonly List<GameObject> cars = new List<GameObject>();
        /// <summary>0 verde, 1 amarelo, 2 vermelho (eixo vertical = carros andando em Z).</summary>
        public static int State(bool vertical)
        {
            float t = (Time.time + (vertical ? 0f : 12f)) % 24f;
            return t < 10f ? 0 : (t < 12f ? 1 : 2);
        }

        void Start()
        {
            for (int kx = 1; kx <= 6; kx++) for (int kz = 1; kz <= 6; kz++)
                {
                    Vector3 n = CityGenerator.Node(kx, kz) + new Vector3(8.5f, 0, 8.5f);
                    W.Box(transform, "TLPole", n + new Vector3(0, 2.5f, 0), new Vector3(.2f, 5f, .2f), Mats.Metal, 2f, true, false);
                    W.Box(transform, "TLV", n + new Vector3(0, 4.6f, -.2f), new Vector3(.4f, .4f, .15f), Mats.LightV, 1f, false, false);
                    W.Box(transform, "TLH", n + new Vector3(-.2f, 4.6f, 0), new Vector3(.15f, .4f, .4f), Mats.LightH, 1f, false, false);
                }
            VType[] types = { VType.Car, VType.Car, VType.Car, VType.Pickup, VType.Truck, VType.Car };
            for (int i = 0; i < 22; i++)
            {
                var c = Color.HSVToRGB(Random.value, Random.Range(.1f, .7f), Random.Range(.3f, .9f));
                var v = VehicleBuilder.Build(types[i % types.Length], c, Vector3.zero, 0, transform, false, true);
                v.IsTraffic = true;
                v.gameObject.AddComponent<TrafficCar>().Init(Random.Range(1, 7), Random.Range(1, 7));
                cars.Add(v.gameObject);
            }
            Quality.Changed += Apply; Apply();
        }
        void OnDestroy() { Quality.Changed -= Apply; }
        void Apply() { for (int i = 0; i < cars.Count; i++) cars[i].SetActive(i < Quality.TrafficCount); }

        static Color C(int s) { return s == 0 ? new Color(0, 2f, 0) : (s == 1 ? new Color(2f, 1.4f, 0) : new Color(2f, 0, 0)); }
        void Update()
        {
            Mats.Glow(Mats.LightV, C(State(true)));
            Mats.Glow(Mats.LightH, C(State(false)));
        }
    }
}
