using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Bodycam
{
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager I;
        class Obj { public string text; public System.Func<Vector3> pos; public System.Func<bool> done; public System.Action onDone; }
        class Mis
        {
            public string id, title, giverLine; public int reward; public List<Obj> steps = new List<Obj>();
            public Mis S(string text, System.Func<Vector3> pos, System.Func<bool> done, System.Action onDone = null)
            { steps.Add(new Obj { text = text, pos = pos, done = done, onDone = onDone }); return this; }
        }
        readonly Dictionary<string, Mis> all = new Dictionary<string, Mis>();
        public string ActiveId; public int Step; public HashSet<string> Done = new HashSet<string>(); public int Money;
        public string EventLine; public Vector3 EventPos; public bool HasEvent;
        GameObject beacon; float t;

        void Awake()
        {
            I = this;
            beacon = W.Prim(PrimitiveType.Cylinder, transform, "Beacon", Vector3.zero, new Vector3(1f, 30f, 1f), Mats.New(Color.yellow, .2f), false);
            Mats.Glow(beacon.GetComponent<Renderer>().sharedMaterial, new Color(2f, 1.6f, 0f));
            beacon.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beacon.SetActive(false);
            Define();
        }

        static float Flat(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
        static bool Near(string k, float r) { return Flat(PlayerController.I.transform.position, CityGenerator.Anchor(k)) < r; }
        static bool Holding(string item) { var h = PlayerController.I.Held; return h != null && h.itemName == item; }
        static bool InCar(VType t) { var v = PlayerController.I.Vehicle; return v != null && v.type == t; }
        static void Consume() { var p = PlayerController.I; if (p.Held != null) { var g = p.Held.gameObject; p.Held = null; Destroy(g); } }
        static System.Func<Vector3> A(string k) { return () => CityGenerator.Anchor(k); }

        Mis Add(string id, string title, int reward, string line)
        { var m = new Mis { id = id, title = title, reward = reward, giverLine = line }; all[id] = m; return m; }

        void Define()
        {
            Add("patrulha", "Patrulha urbana", 500, "Preciso de alguem pra patrulhar. Pegue uma viatura, passe no posto e na oficina e volte aqui.")
                .S("Pegue uma VIATURA e va ao Posto de Gasolina", A("G"), () => InCar(VType.Police) && Near("G", 40))
                .S("Patrulhe a Oficina (de viatura)", A("W"), () => InCar(VType.Police) && Near("W", 40))
                .S("Volte a Delegacia", A("P"), () => Near("P", 30));
            Add("entrega", "Entrega expressa", 400, "Tem uma encomenda no fundo do mercado. Leve ate uma casa do bairro residencial.")
                .S("Pegue a ENCOMENDA dentro do Mercado", A("M"), () => Holding("Encomenda"))
                .S("Entregue na area residencial", A("R"), () => Holding("Encomenda") && Near("R", 30), Consume);
            Add("peca", "Chave inglesa perdida", 350, "Esqueci a chave inglesa no galpao industrial. Busca pra mim?")
                .S("Encontre a CHAVE INGLESA no galpao industrial", A("I"), () => Holding("Chave inglesa"))
                .S("Leve a chave para a Oficina", A("W"), () => Holding("Chave inglesa") && Near("W", 25), Consume);
            Add("medico", "Suprimentos medicos", 450, "Faltam kits medicos aqui. Tem um na loja do posto de gasolina.")
                .S("Pegue o KIT MEDICO na loja do Posto", A("G"), () => Holding("Kit medico"))
                .S("Entregue no Hospital", A("H"), () => Holding("Kit medico") && Near("H", 25), Consume);
        }

        public void Offer(string id, NpcAgent n)
        {
            if (!all.ContainsKey(id)) return;
            var m = all[id];
            if (Done.Contains(id)) { Hud.I.Say(n.displayName + ": Obrigado de novo pela ajuda!"); return; }
            if (ActiveId == id) { Hud.I.Say(n.displayName + ": Ja combinamos, pode ir!"); return; }
            if (ActiveId != null) { Hud.I.Say(n.displayName + ": Termine sua missao atual primeiro."); return; }
            ActiveId = id; Step = 0;
            Hud.I.Say(n.displayName + ": " + m.giverLine, 7f); Hud.I.Toast("Nova missao: " + m.title, 3f);
        }

        public void Restore(string active, int step, string done, int money)
        {
            Money = money; Done.Clear();
            if (!string.IsNullOrEmpty(done)) foreach (var s in done.Split(',')) if (s.Length > 0) Done.Add(s);
            if (!string.IsNullOrEmpty(active) && all.ContainsKey(active) && step < all[active].steps.Count) { ActiveId = active; Step = step; }
        }

        void Update()
        {
            t -= Time.deltaTime; if (t > 0f || PlayerController.I == null) return; t = .25f;
            Vector3 pp = PlayerController.I.transform.position;
            string line = "Explore livremente. Fale com quem tem '!' para pegar missoes.";
            Vector3? bpos = null;
            if (ActiveId != null)
            {
                var m = all[ActiveId]; var s = m.steps[Step];
                if (s.done())
                {
                    if (s.onDone != null) s.onDone();
                    Step++;
                    if (Step >= m.steps.Count)
                    {
                        Money += m.reward; Done.Add(ActiveId);
                        Hud.I.Toast("MISSAO CONCLUIDA: " + m.title + "  +R$" + m.reward, 4f); ActiveId = null;
                    }
                    else Hud.I.Toast("Objetivo atualizado", 2.5f);
                }
                if (ActiveId != null)
                {
                    s = m.steps[Step]; Vector3 p = s.pos(); bpos = p;
                    line = "MISSAO: " + m.title + "\n> " + s.text + "  (" + (int)Flat(pp, p) + " m)";
                }
            }
            if (HasEvent)
            {
                line += "\n[!] " + EventLine + " (" + (int)Flat(pp, EventPos) + " m)";
                if (!bpos.HasValue) bpos = EventPos;
            }
            Hud.I.SetObjective(line);
            beacon.SetActive(bpos.HasValue);
            if (bpos.HasValue) beacon.transform.position = new Vector3(bpos.Value.x, 30f, bpos.Value.z);
        }
    }

    /// <summary>Acontecimentos aleatorios: acidente com curiosos e alarme em loja.</summary>
    public class RandomEvents : MonoBehaviour
    {
        float next = 60f, life; GameObject cur; Vector3 pos; bool done; AudioSource alarm;

        void Update()
        {
            var pl = PlayerController.I; var mm = MissionManager.I; if (pl == null || mm == null) return;
            float dt = Time.deltaTime;
            if (cur == null) { next -= dt; if (next <= 0f) Spawn(pl); return; }
            life -= dt;
            Vector3 d = pl.transform.position - pos; d.y = 0;
            if (!done && d.magnitude < 16f)
            {
                done = true; mm.Money += 150; Hud.I.Toast("Ocorrencia atendida! +R$150", 3.5f);
                if (alarm != null) alarm.Stop();
                mm.HasEvent = false; life = Mathf.Min(life, 20f);
            }
            if (life <= 0f) { Destroy(cur); cur = null; mm.HasEvent = false; next = Random.Range(70f, 140f); }
        }

        void Spawn(PlayerController pl)
        {
            var mm = MissionManager.I; var rng = new System.Random(Random.Range(0, 99999));
            cur = new GameObject("Event"); done = false; life = 150f;
            if (Random.value < .6f)
            {
                for (int i = 0; i < 20; i++)
                {
                    int kx = Random.Range(1, 7), kz = Random.Range(1, 7);
                    Vector3 p = CityGenerator.Node(kx, kz); float dd = (p - pl.transform.position).magnitude;
                    if (dd > 130f && dd < 350f) { pos = p; break; }
                    pos = p;
                }
                VehicleBuilder.Build(VType.Car, new Color(.6f, .1f, .1f), pos + new Vector3(-1.5f, .1f, 0), Random.Range(0f, 360f), cur.transform, false, true);
                VehicleBuilder.Build(VType.Car, new Color(.2f, .3f, .6f), pos + new Vector3(2f, .1f, 2f), Random.Range(0f, 360f), cur.transform, false, true);
                Fx.Smoke(cur.transform, pos + new Vector3(0, .8f, 0));
                for (int i = 0; i < 3; i++)
                {
                    var n = NpcFactory.Create(cur.transform, NpcAgent.Role.Talker, rng);
                    n.transform.position = pos + new Vector3(5f + i * 1.2f, .05f, -3f); n.focus = pos;
                    n.customLines = new[] { "Meu Deus, que acidente!", "Chama a ambulancia!", "Eu vi tudo, foi muito rapido." };
                }
                mm.EventLine = "Acidente reportado"; mm.EventPos = pos; mm.HasEvent = true;
            }
            else
            {
                pos = CityGenerator.Anchor("M");
                alarm = ProcAudio.Source(cur, ProcAudio.Alarm(), true, .8f, 120f); cur.transform.position = pos; alarm.Play();
                mm.EventLine = "Alarme disparado no Mercado"; mm.EventPos = pos; mm.HasEvent = true;
            }
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public float px, py, pz, yaw, hour; public int weather, quality, fps, money, step; public string active, done;
    }

    public class SaveSystem : MonoBehaviour
    {
        static string Path { get { return System.IO.Path.Combine(Application.persistentDataPath, "bodycam_save.json"); } }
        public static SaveData Load()
        {
            try { if (System.IO.File.Exists(Path)) return JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(Path)); } catch { }
            return null;
        }
        public static void Save()
        {
            var p = PlayerController.I; var dn = DayNightWeather.I; var mm = MissionManager.I;
            if (p == null || dn == null || mm == null) return;
            var d = new SaveData
            {
                px = p.transform.position.x, py = p.transform.position.y, pz = p.transform.position.z, yaw = p.transform.eulerAngles.y,
                hour = dn.Hour, weather = dn.Weather, quality = Quality.Level, fps = Quality.Fps, money = mm.Money,
                active = mm.ActiveId, step = mm.Step, done = string.Join(",", new List<string>(mm.Done).ToArray())
            };
            try { System.IO.File.WriteAllText(Path, JsonUtility.ToJson(d)); } catch { }
        }
        IEnumerator Start() { while (true) { yield return new WaitForSeconds(30f); Save(); if (Hud.I) Hud.I.Toast("Jogo salvo", 1.2f); } }
        void OnApplicationPause(bool p) { if (p) Save(); }
        void OnApplicationQuit() { Save(); }
    }
}
