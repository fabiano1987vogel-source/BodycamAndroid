using UnityEngine;

namespace Bodycam
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I;
        public Transform camRoot, hand;
        public Camera cam;
        public SimpleVehicle Vehicle;
        public Pickable Held;
        public float Speed01;
        public bool InVehicle { get { return Vehicle != null; } }

        CharacterController cc;
        float yaw, pitch, vy, stepDist, reach;
        int stance;
        Light torch;
        AudioSource foot;
        Transform armL, armR;
        GameObject armsRoot;
        Vector3 spawn;
        const float WalkS = 2.2f, RunS = 4.6f;
        static readonly float[] camH = { 1.3f, .85f, .3f }, ccH = { 1.8f, 1.1f, .6f }, stSpeed = { 0f, 1.4f, .7f };

        void Awake() { I = this; }

        public static PlayerController Create(Vector3 pos)
        {
            var go = new GameObject("Player"); go.tag = "Player"; go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .3f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .35f; cc.skinWidth = .05f; cc.slopeLimit = 50f;
            var p = go.AddComponent<PlayerController>(); p.cc = cc; p.spawn = pos;
            p.camRoot = new GameObject("CamRoot").transform; p.camRoot.SetParent(go.transform, false); p.camRoot.localPosition = new Vector3(0, 1.3f, .15f);
            var cg = new GameObject("BodyCam"); cg.tag = "MainCamera"; cg.transform.SetParent(p.camRoot, false);
            p.cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>();
            var bc = cg.AddComponent<BodycamCamera>(); bc.player = p;
            p.BuildArms();
            p.foot = ProcAudio.Source(go, ProcAudio.Step(), false, .5f, 20f, false);
            return p;
        }

        public void SetYaw(float y) { yaw = y; }

        Transform MakeArm(float side)
        {
            var arm = new GameObject("Arm").transform; arm.SetParent(armsRoot.transform, false);
            arm.localPosition = new Vector3(.24f * side, -.34f, .3f); arm.localRotation = Quaternion.Euler(8f, -7f * side, 0f);
            W.Box(arm, "Sleeve", Vector3.zero, new Vector3(.11f, .11f, .34f), Mats.Paint(new Color(.08f, .12f, .25f), .3f, 0), 1f, false, false);
            W.Box(arm, "Hand", new Vector3(0, 0, .26f), new Vector3(.08f, .07f, .16f), Mats.Skin, 1f, false, false);
            return arm;
        }

        void BuildArms()
        {
            armsRoot = new GameObject("Arms"); armsRoot.transform.SetParent(cam.transform, false);
            armR = MakeArm(1f); armL = MakeArm(-1f);
            W.NoShadow(armsRoot);
            hand = new GameObject("HoldPoint").transform; hand.SetParent(cam.transform, false); hand.localPosition = new Vector3(.12f, -.18f, .6f);
            var tg = new GameObject("Torch"); tg.transform.SetParent(cam.transform, false); tg.transform.localPosition = new Vector3(.2f, -.15f, .5f);
            torch = tg.AddComponent<Light>(); torch.type = LightType.Spot; torch.range = 30f; torch.spotAngle = 55f; torch.intensity = 3f;
            torch.shadows = LightShadows.None; torch.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            yaw += GameInput.Look.x; pitch = Mathf.Clamp(pitch - GameInput.Look.y, -80f, 80f);
            if (InVehicle) yaw = Mathf.Clamp(yaw, -110f, 110f);
            transform.localRotation = Quaternion.Euler(0, yaw, 0);
            camRoot.localRotation = Quaternion.Euler(pitch, 0, 0);
            if (GameInput.TorchPressed) torch.enabled = !torch.enabled;
            if (GameInput.VehiclePressed) { if (InVehicle) Exit(); else TryEnter(); }
            if (InVehicle) DriveUpdate(dt); else { Walk(dt); Interact(); }
            AnimArms(dt);
            if (!InVehicle && transform.position.y < -30f) { transform.position = spawn; vy = 0; }
        }

        void Walk(float dt)
        {
            if (GameInput.CrouchPressed) stance = (stance + 1) % 3;
            Vector2 m = GameInput.Move;
            bool run = GameInput.Run && stance == 0 && m.y > .1f;
            float sp = stance == 0 ? (run ? RunS : WalkS) : stSpeed[stance];
            Vector3 dir = transform.right * m.x + transform.forward * m.y;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            if (cc.isGrounded) { if (vy < 0) vy = -2f; if (GameInput.JumpPressed && stance == 0) vy = 6.2f; }
            vy -= 20f * dt;
            cc.Move((dir * sp + Vector3.up * vy) * dt);
            float h = Mathf.Lerp(cc.height, ccH[stance], 10f * dt); cc.height = h; cc.center = new Vector3(0, h / 2f, 0);
            Vector3 hv = cc.velocity; hv.y = 0; float spd = hv.magnitude;
            Speed01 = Mathf.Clamp01(spd / RunS);
            camRoot.localPosition = Vector3.Lerp(camRoot.localPosition, new Vector3(0, camH[stance], .15f), 10f * dt);
            stepDist += spd * dt;
            if (stepDist > (run ? 1.6f : 1.2f) && cc.isGrounded) { stepDist = 0; foot.PlayOneShot(ProcAudio.Step(), stance == 0 ? .6f : .3f); }
        }

        void DriveUpdate(float dt)
        {
            Vehicle.input = GameInput.Move; Vehicle.brake = GameInput.JumpHeld;
            if (GameInput.InteractPressed && Vehicle.type == VType.Police) Vehicle.ToggleSiren();
            Speed01 = 0;
            camRoot.localPosition = Vector3.Lerp(camRoot.localPosition, new Vector3(0, Vehicle.eyeHeight, .1f), 8f * dt);
            Hud.I.SetPrompt("F: Sair do veiculo" + (Vehicle.type == VType.Police ? "   USAR: Sirene" : ""));
        }

        void Interact()
        {
            string pr = null; Door door = null; Pickable pk = null; NpcAgent npc = null;
            RaycastHit hit;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 2.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                door = hit.collider.GetComponentInParent<Door>();
                pk = hit.collider.GetComponentInParent<Pickable>();
                npc = hit.collider.GetComponentInParent<NpcAgent>();
            }
            if (door) pr = door.open ? "USAR: Fechar porta" : "USAR: Abrir porta";
            else if (pk && Held == null) pr = "USAR: Pegar " + pk.itemName;
            else if (npc) pr = "USAR: Falar" + (npc.IsGiver ? " (missao)" : "");
            else if (Held) pr = "USAR: Soltar " + Held.itemName;
            else if (NearestVehicle(4.5f) != null) pr = "VEIC.: Entrar no veiculo";
            if (pr != null) Hud.I.SetPrompt(pr);
            if (GameInput.InteractPressed)
            {
                reach = .3f;
                if (door) door.Toggle();
                else if (pk && Held == null) { Held = pk; pk.Carry(hand); }
                else if (npc) npc.Interact();
                else if (Held) Drop();
            }
        }

        void Drop() { Held.Drop(cam.transform.forward); Held = null; }

        SimpleVehicle NearestVehicle(float r)
        {
            SimpleVehicle best = null; float bd = r * r;
            foreach (var v in SimpleVehicle.All)
            {
                if (v == null || !v.Enterable || v.Occupied) continue;
                float d = (v.transform.position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = v; }
            }
            return best;
        }

        void TryEnter()
        {
            var v = NearestVehicle(4.5f); if (v == null) return;
            if (Held) Drop();
            Vehicle = v; v.SetDriver(true);
            cc.enabled = false; stance = 0;
            v.transform.SetParent(null, true);
            transform.SetParent(v.seat, false); transform.localPosition = Vector3.zero;
            yaw = 0; pitch = 0; armsRoot.SetActive(false);
        }

        void Exit()
        {
            var v = Vehicle; v.SetDriver(false);
            transform.SetParent(null, true);
            transform.position = v.transform.position - v.transform.right * (v.halfWidth + .8f) + Vector3.up * .3f;
            yaw = v.transform.eulerAngles.y; vy = 0; Vehicle = null; cc.enabled = true; armsRoot.SetActive(true);
        }

        void AnimArms(float dt)
        {
            reach = Mathf.MoveTowards(reach, 0f, dt);
            float f = Mathf.Sin(reach / .3f * Mathf.PI) * .25f;
            Vector3 rb = Held ? new Vector3(.2f, -.2f, .35f) : new Vector3(.24f, -.34f, .3f);
            armR.localPosition = Vector3.Lerp(armR.localPosition, rb + Vector3.forward * f, 12f * dt);
            armL.localPosition = new Vector3(-.24f, -.34f + Mathf.Sin(Time.time * 1.6f) * .004f, .3f);
        }
    }
}
