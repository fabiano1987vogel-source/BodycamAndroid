using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bodycam
{
    public static class GameInput
    {
        public static Vector2 Move, Look;
        public static bool Run, JumpHeld, JumpPressed, CrouchPressed, InteractPressed, VehiclePressed, TorchPressed;
        public static void ClearFrame()
        {
            Look = Vector2.zero;
            JumpPressed = CrouchPressed = InteractPressed = VehiclePressed = TorchPressed = false;
        }
    }

    /// <summary>Joystick esquerdo, area de camera na direita e botoes. No editor/PC usa teclado + mouse.</summary>
    [DefaultExecutionOrder(-100)]
    public class TouchControls : MonoBehaviour
    {
        class Btn { public string id; public Image img; public Text txt; public Rect r; }
        readonly List<Btn> btns = new List<Btn>();
        readonly Dictionary<int, string> owner = new Dictionary<int, string>();
        RectTransform stickBase, stickKnob;
        Vector2 stickOrigin;
        float u, R, lblT;
        bool runOn;
        static Sprite circle;

        static Sprite Circle()
        {
            if (circle == null) circle = Sprite.Create(ProcTex.Soft(64, 0), new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            return circle;
        }

        void Start()
        {
            u = Screen.height / 1080f; R = 130f * u;
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 10;
            stickBase = Img("StickBase", new Color(1, 1, 1, .12f), R * 2f);
            stickKnob = Img("StickKnob", new Color(1, 1, 1, .3f), R * .9f);
            float W = Screen.width, H = Screen.height;
            AddBtn("interact", "USAR", W - 170 * u, 230 * u, 150 * u);
            AddBtn("jump", "PULAR", W - 370 * u, 130 * u, 130 * u);
            AddBtn("run", "CORRER", W - 370 * u, 275 * u, 120 * u);
            AddBtn("crouch", "AGACH.", W - 170 * u, 70 * u, 110 * u);
            AddBtn("vehicle", "VEIC.", W - 545 * u, 140 * u, 120 * u);
            AddBtn("torch", "LUZ", W - 545 * u, 285 * u, 100 * u);
            AddBtn("gfx", "GFX", W - 110 * u, H - 70 * u, 90 * u);
            AddBtn("fps", "FPS", W - 220 * u, H - 70 * u, 90 * u);
            ResetStick();
        }

        RectTransform Img(string n, Color c, float size)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var im = go.GetComponent<Image>(); im.sprite = Circle(); im.color = c; im.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        void AddBtn(string id, string label, float cx, float cy, float s)
        {
            var rt = Img("B_" + id, new Color(1, 1, 1, .22f), s); rt.anchoredPosition = new Vector2(cx, cy);
            var b = new Btn { id = id, img = rt.GetComponent<Image>(), r = new Rect(cx - s / 2f, cy - s / 2f, s, s) };
            var tg = new GameObject("T", typeof(RectTransform)); tg.transform.SetParent(rt, false);
            var trt = (RectTransform)tg.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var t = tg.AddComponent<Text>(); t.font = Hud.F; t.fontSize = Mathf.RoundToInt(s * .22f);
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.text = label; t.raycastTarget = false;
            b.txt = t; btns.Add(b);
        }

        void ResetStick()
        {
            stickOrigin = new Vector2(210 * u, 210 * u);
            stickBase.anchoredPosition = stickOrigin; stickKnob.anchoredPosition = stickOrigin;
        }

        void Update()
        {
            GameInput.ClearFrame();
            if (Application.isMobilePlatform) Touch(); else Desktop();
            lblT -= Time.deltaTime;
            if (lblT <= 0f)
            {
                lblT = .5f;
                foreach (var b in btns)
                {
                    if (b.id == "gfx") b.txt.text = Quality.Names[Quality.Level];
                    else if (b.id == "fps") b.txt.text = Quality.Fps + " FPS";
                }
            }
        }

        void Touch()
        {
            bool stick = false;
            foreach (var t in Input.touches)
            {
                Vector2 p = t.position; int id = t.fingerId;
                if (t.phase == TouchPhase.Began)
                {
                    string o = null;
                    foreach (var b in btns) if (b.r.Contains(p)) { o = b.id; break; }
                    if (o == null) o = p.x < Screen.width * .45f ? "stick" : "look";
                    owner[id] = o;
                    if (o == "stick") stickOrigin = p; else if (o != "look") Press(o, true);
                }
                string ow;
                if (!owner.TryGetValue(id, out ow)) continue;
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    owner.Remove(id);
                    if (ow != "stick" && ow != "look") Press(ow, false);
                    continue;
                }
                if (ow == "stick")
                {
                    Vector2 d = (p - stickOrigin) / R;
                    if (d.magnitude > 1f) { d.Normalize(); stickOrigin = p - d * R; }
                    GameInput.Move = d; stick = true;
                    stickBase.anchoredPosition = stickOrigin; stickKnob.anchoredPosition = stickOrigin + d * R;
                }
                else if (ow == "look") GameInput.Look += t.deltaPosition * .09f * (1080f / Screen.height);
            }
            if (!stick) { GameInput.Move = Vector2.zero; ResetStick(); }
        }

        void Press(string id, bool down)
        {
            Btn b = btns.Find(x => x.id == id);
            if (b != null) b.img.color = new Color(1, 1, 1, down ? .5f : (id == "run" && runOn ? .45f : .22f));
            if (id == "jump") GameInput.JumpHeld = down;
            if (!down) return;
            switch (id)
            {
                case "jump": GameInput.JumpPressed = true; break;
                case "crouch": GameInput.CrouchPressed = true; break;
                case "interact": GameInput.InteractPressed = true; break;
                case "vehicle": GameInput.VehiclePressed = true; break;
                case "torch": GameInput.TorchPressed = true; break;
                case "run": runOn = !runOn; GameInput.Run = runOn; if (b != null) b.img.color = new Color(1, 1, 1, runOn ? .45f : .22f); break;
                case "gfx": Quality.Next(); break;
                case "fps": Quality.NextFps(); break;
            }
        }

        void Desktop()
        {
            GameInput.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            GameInput.Run = Input.GetKey(KeyCode.LeftShift);
            GameInput.JumpHeld = Input.GetKey(KeyCode.Space);
            GameInput.JumpPressed = Input.GetKeyDown(KeyCode.Space);
            GameInput.CrouchPressed = Input.GetKeyDown(KeyCode.C);
            GameInput.InteractPressed = Input.GetKeyDown(KeyCode.E);
            GameInput.VehiclePressed = Input.GetKeyDown(KeyCode.F);
            GameInput.TorchPressed = Input.GetKeyDown(KeyCode.T);
            if (Input.GetKeyDown(KeyCode.G)) Quality.Next();
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState == CursorLockMode.Locked)
                GameInput.Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
        }
    }
}
