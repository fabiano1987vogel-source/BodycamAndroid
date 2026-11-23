using UnityEngine;
using UnityEngine.UI;

namespace Bodycam
{
    public class Hud : MonoBehaviour
    {
        public static Hud I;
        static Font font;
        public static Font F
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return font;
            }
        }
        Text osd, objective, prompt, subtitle, toast, money, gps, loadTxt;
        GameObject loading;
        float subT, toastT, osdT;
        string promptNow = "";
        float battery = 97f;

        public static Hud Create() { return new GameObject("HUD").AddComponent<Hud>(); }

        Text Label(string n, float x0, float y0, float x1, float y1, int size, TextAnchor a, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>(); t.font = F; t.fontSize = size; t.alignment = a; t.color = c; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            return t;
        }

        void Awake()
        {
            I = this;
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 5;
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = .5f;
            osd = Label("OSD", .015f, .88f, .6f, .985f, 32, TextAnchor.UpperLeft, new Color(1, 1, 1, .9f));
            objective = Label("Obj", .015f, .66f, .5f, .87f, 30, TextAnchor.UpperLeft, new Color(1f, .92f, .5f));
            money = Label("Money", .6f, .75f, .8f, .86f, 30, TextAnchor.UpperRight, new Color(.6f, 1f, .6f));
            gps = Label("GPS", .5f, .02f, .66f, .08f, 24, TextAnchor.LowerRight, new Color(1, 1, 1, .7f));
            prompt = Label("Prompt", .3f, .30f, .7f, .38f, 36, TextAnchor.MiddleCenter, Color.white);
            subtitle = Label("Sub", .2f, .1f, .8f, .22f, 34, TextAnchor.MiddleCenter, Color.white);
            toast = Label("Toast", .2f, .6f, .8f, .72f, 42, TextAnchor.MiddleCenter, new Color(1f, .9f, .3f));
            loading = new GameObject("Loading", typeof(RectTransform), typeof(Image));
            loading.transform.SetParent(transform, false);
            var rt = (RectTransform)loading.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            loading.GetComponent<Image>().color = Color.black;
            loadTxt = Label("LoadTxt", 0, 0, 1, 1, 48, TextAnchor.MiddleCenter, Color.white);
            loadTxt.transform.SetParent(loading.transform, false);
        }

        public void ShowLoading(bool on, string msg = "") { loading.SetActive(on); loadTxt.text = msg; }
        public void Say(string t, float sec = 4f) { subtitle.text = t; subT = sec; }
        public void Toast(string t, float sec = 3f) { toast.text = t; toastT = sec; }
        public void SetPrompt(string t) { promptNow = t; }
        public void SetObjective(string t) { objective.text = t; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (subT > 0 && (subT -= dt) <= 0) subtitle.text = "";
            if (toastT > 0 && (toastT -= dt) <= 0) toast.text = "";
            prompt.text = promptNow; promptNow = "";
            battery = Mathf.Max(5f, battery - dt * .01f);
            osdT -= dt;
            if (osdT <= 0f) { osdT = .25f; Refresh(); }
        }

        void Refresh()
        {
            var d = DayNightWeather.I;
            float hr = d != null ? d.Hour : 12f;
            int h = (int)hr, m = (int)((hr - h) * 60f), s = (int)(Time.time % 60f);
            bool blink = ((int)(Time.time * 2f)) % 2 == 0;
            osd.text = (blink ? "<color=#ff2020>REC</color>" : "   ") + "  BODYCAM-0427   BAT " + (int)battery + "%\n"
                + System.DateTime.Now.ToString("dd/MM/yyyy") + "  " + h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00")
                + (d != null ? "  " + DayNightWeather.WxNames[d.Weather].ToUpper() : "");
            var mm = MissionManager.I; if (mm != null) money.text = "R$ " + mm.Money;
            var p = PlayerController.I;
            if (p != null)
            {
                Vector3 q = p.transform.position;
                gps.text = "GPS " + (23.5505f - q.z * .00001f).ToString("0.0000") + "S " + (46.6333f - q.x * .00001f).ToString("0.0000") + "W";
            }
        }
    }
}
