using System.Collections.Generic;
using UnityEngine;

namespace Bodycam
{
    /// <summary>NPC procedural: caminha pela calcada, conversa, trabalha, da missoes e reage ao jogador/eventos.</summary>
    public class NpcAgent : MonoBehaviour
    {
        public enum Role { Walker, Talker, Worker, Giver }
        public static readonly List<NpcAgent> All = new List<NpcAgent>();
        public Role role;
        public Vector3[] route; public int idx;
        public Transform legL, legR, armL, armR;
        public GameObject visual;
        public NpcAgent partner; public Vector3 focus;
        public string missionId, displayName = "Cidadao"; public string[] customLines;
        public float seed;
        public bool IsGiver { get { return role == Role.Giver && missionId != null; } }

        float phase, idleT, fleeT, nextCheck;
        bool active = true;
        Vector3 fleeFrom;
        Collider col;

        static readonly string[] Lines = {
            "Bom dia, policial!", "Cuidado com o transito por ai.", "Essa chuva nao para, ne?",
            "Voce viu o acidente na avenida?", "Essa camera ai ta gravando tudo?", "Fica tranquilo, aqui esta calmo hoje.",
            "O mercado esta cheio hoje.", "Ja vi movimento estranho la na zona industrial." };

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Start() { col = GetComponent<Collider>(); }

        public static void AlertAround(Vector3 pos, float r)
        {
            foreach (var n in All) if ((n.transform.position - pos).sqrMagnitude < r * r) n.Alert(pos);
        }
        public void Alert(Vector3 src) { fleeT = 6f; fleeFrom = src; }

        public void Interact()
        {
            var p = PlayerController.I.transform.position; p.y = transform.position.y;
            transform.rotation = Quaternion.LookRotation(p - transform.position + Vector3.forward * .001f);
            if (IsGiver) { MissionManager.I.Offer(missionId, this); return; }
            var ls = customLines != null && customLines.Length > 0 ? customLines : Lines;
            Hud.I.Say(displayName + ": " + ls[Random.Range(0, ls.Length)], 4f);
        }

        bool ShouldBeActive(float h)
        {
            switch (role)
            {
                case Role.Walker: return h >= 6f + seed * .2f && h < 22.5f - seed * .2f;
                case Role.Talker: return h >= 8f && h < 21f;
                case Role.Worker: return h >= 8f && h < 18f;
                default: return true;
            }
        }

        bool Step(Vector3 tgt, float sp, float dt)
        {
            Vector3 d = tgt - transform.position; d.y = 0;
            if (d.magnitude < .4f) return true;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 300f * dt);
            transform.position += d.normalized * sp * dt;
            return false;
        }
        void Face(Vector3 p, float dt)
        {
            Vector3 d = p - transform.position; d.y = 0;
            if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 200f * dt);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time > nextCheck)
            {
                nextCheck = Time.time + 1f + seed * .01f;
                float h = DayNightWeather.I != null ? DayNightWeather.I.Hour : 12f;
                bool should = ShouldBeActive(h);
                if (should != active) { active = should; visual.SetActive(active); if (col) col.enabled = active; }
            }
            if (!active) return;
            bool moving = false;
            var pl = PlayerController.I;
            Vector3 pp = pl != null ? pl.transform.position : Vector3.zero;
            if (fleeT > 0f)
            {
                fleeT -= dt; Vector3 away = transform.position - fleeFrom; away.y = 0;
                Step(transform.position + away.normalized * 5f, 4.2f, dt); moving = true;
            }
            else if (role == Role.Walker && route != null)
            {
                if (idleT > 0f) { idleT -= dt; Face(pp, dt); }
                else
                {
                    if (pl != null && pl.Speed01 > .8f && (pp - transform.position).sqrMagnitude < 9f) { idleT = 1.5f; }
                    else
                    {
                        if (Step(route[idx], 1.4f, dt)) { idx = (idx + 1) % route.Length; if (Random.value < .15f) idleT = Random.Range(2f, 6f); }
                        moving = true;
                    }
                }
            }
            else if (role == Role.Talker) Face(partner != null ? partner.transform.position : focus, dt);
            else if (role == Role.Giver && (pp - transform.position).sqrMagnitude < 36f) Face(pp, dt);

            float sp01 = moving ? 1f : 0f;
            phase += dt * (fleeT > 0f ? 12f : 7f) * sp01;
            float sw = Mathf.Sin(phase) * 35f * sp01;
            legL.localRotation = Quaternion.Euler(sw, 0, 0); legR.localRotation = Quaternion.Euler(-sw, 0, 0);
            armL.localRotation = Quaternion.Euler(-sw, 0, 0); armR.localRotation = Quaternion.Euler(sw, 0, 0);
            if (!moving)
            {
                if (role == Role.Worker) armR.localRotation = Quaternion.Euler(-70f + Mathf.Sin(Time.time * 6f + seed) * 30f, 0, 0);
                else if (role == Role.Talker) armR.localRotation = Quaternion.Euler(-40f + Mathf.Sin(Time.time * 2f + seed) * 25f, 0, 0);
            }
        }
    }

    public static class NpcFactory
    {
        static readonly Color[] Shirts = { new Color(.7f, .2f, .2f), new Color(.2f, .4f, .7f), new Color(.8f, .8f, .3f), new Color(.3f, .6f, .35f), new Color(.85f, .85f, .85f), new Color(.2f, .2f, .2f) };
        static readonly Color[] Pants = { new Color(.15f, .2f, .35f), new Color(.2f, .2f, .2f), new Color(.4f, .35f, .25f) };
        static readonly Color[] Skins = { new Color(.85f, .68f, .55f), new Color(.6f, .42f, .3f), new Color(.4f, .27f, .2f), new Color(.93f, .78f, .65f) };
        static readonly string[] Names = { "Joao", "Maria", "Carlos", "Ana", "Pedro", "Lucia", "Marcos", "Rita", "Paulo", "Julia", "Bruno", "Elaine" };

        static Transform Pivot(Transform p, Vector3 lp) { var t = new GameObject("Pivot").transform; t.SetParent(p, false); t.localPosition = lp; return t; }

        public static NpcAgent Create(Transform parent, NpcAgent.Role role, System.Random rng)
        {
            var go = new GameObject("NPC_" + role); go.transform.SetParent(parent, false);
            var a = go.AddComponent<NpcAgent>(); a.role = role; a.seed = (float)rng.NextDouble() * 10f; a.displayName = Names[rng.Next(Names.Length)];
            var vis = new GameObject("Vis").transform; vis.SetParent(go.transform, false); a.visual = vis.gameObject;
            Color shirt = Shirts[rng.Next(Shirts.Length)], pants = Pants[rng.Next(Pants.Length)];
            if (role == NpcAgent.Role.Giver) shirt = new Color(.1f, .2f, .5f);
            var shirtM = Mats.Paint(shirt, .2f, 0); var pantM = Mats.Paint(pants, .2f, 0);
            var skin = Mats.Paint(Skins[rng.Next(Skins.Length)], .3f, 0);
            W.Box(vis, "Torso", new Vector3(0, 1.15f, 0), new Vector3(.46f, .62f, .26f), shirtM, 1f, false, false);
            W.Prim(PrimitiveType.Sphere, vis, "Head", new Vector3(0, 1.62f, 0), new Vector3(.24f, .26f, .24f), skin, false);
            W.Box(vis, "Hair", new Vector3(0, 1.74f, -.01f), new Vector3(.25f, .08f, .26f), Mats.Paint(new Color(.1f, .08f, .05f), .2f, 0), 1f, false, false);
            a.legL = Pivot(vis, new Vector3(-.12f, .85f, 0)); a.legR = Pivot(vis, new Vector3(.12f, .85f, 0));
            W.Box(a.legL, "Leg", new Vector3(0, -.4f, 0), new Vector3(.18f, .8f, .2f), pantM, 1f, false, false);
            W.Box(a.legR, "Leg", new Vector3(0, -.4f, 0), new Vector3(.18f, .8f, .2f), pantM, 1f, false, false);
            a.armL = Pivot(vis, new Vector3(-.3f, 1.42f, 0)); a.armR = Pivot(vis, new Vector3(.3f, 1.42f, 0));
            W.Box(a.armL, "Arm", new Vector3(0, -.3f, 0), new Vector3(.12f, .6f, .14f), shirtM, 1f, false, false);
            W.Box(a.armR, "Arm", new Vector3(0, -.3f, 0), new Vector3(.12f, .6f, .14f), shirtM, 1f, false, false);
            var cc = go.AddComponent<CapsuleCollider>(); cc.radius = .3f; cc.height = 1.8f; cc.center = new Vector3(0, .9f, 0);
            go.AddComponent<Rigidbody>().isKinematic = true;
            if (role == NpcAgent.Role.Giver)
            {
                var mk = new GameObject("Mark"); mk.transform.SetParent(vis, false); mk.transform.localPosition = new Vector3(0, 2.1f, 0);
                var tm = mk.AddComponent<TextMesh>(); tm.text = "!"; tm.fontSize = 64; tm.characterSize = .1f; tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.yellow; tm.font = Hud.F; mk.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            }
            return a;
        }

        public static void Populate(Transform b, char t, System.Random rng)
        {
            float f = Quality.NpcFactor; Vector3 c = b.position;
            int walkers = Mathf.RoundToInt((t == 'C' || t == 'M' ? 3 : (t == 'F' ? 0 : 2)) * f);
            float s = 31f;
            for (int i = 0; i < walkers; i++)
            {
                var a = Create(b, NpcAgent.Role.Walker, rng);
                float o = (float)(rng.NextDouble() * 2 - 1);
                Vector3[] cs = { new Vector3(-s + o, .17f, -s + o), new Vector3(-s + o, .17f, s - o), new Vector3(s - o, .17f, s - o), new Vector3(s - o, .17f, -s + o) };
                if (rng.Next(2) == 0) System.Array.Reverse(cs);
                for (int k = 0; k < 4; k++) cs[k] += c;
                a.route = cs; a.idx = rng.Next(4); a.transform.position = cs[a.idx];
            }
            if (t != 'F' && rng.NextDouble() < .7 * f)
            {
                Vector3 p = c + new Vector3(31f, .17f, (float)(rng.NextDouble() * 30 - 15));
                var a1 = Create(b, NpcAgent.Role.Talker, rng); var a2 = Create(b, NpcAgent.Role.Talker, rng);
                a1.transform.position = p; a2.transform.position = p + new Vector3(0, 0, 1.2f); a1.partner = a2; a2.partner = a1;
            }
            Vector3 w = Vector3.zero; string mission = null; bool has = true;
            switch (t)
            {
                case 'P': w = new Vector3(0, .22f, -4); mission = "patrulha"; break;
                case 'M': w = new Vector3(0, .22f, 8); mission = "entrega"; break;
                case 'W': w = new Vector3(0, .22f, 8); mission = "peca"; break;
                case 'H': w = new Vector3(0, .22f, -4); mission = "medico"; break;
                case 'G': w = new Vector3(0, .22f, 18); break;
                case 'I': w = new Vector3(0, .22f, 6); break;
                case 'C': w = new Vector3(-18, .22f, -18); break;
                default: has = false; break;
            }
            if (has)
            {
                if (f > .5f) { var wk = Create(b, NpcAgent.Role.Worker, rng); wk.transform.position = c + w + new Vector3(2, 0, 2); }
                if (mission != null)
                {
                    var g = Create(b, NpcAgent.Role.Giver, rng); g.missionId = mission; g.transform.position = c + w + new Vector3(-3, 0, 1);
                }
            }
        }
    }
}
