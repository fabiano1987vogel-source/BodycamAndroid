using System.Collections;
using UnityEngine;

namespace Bodycam
{
    /// <summary>Monta o jogo inteiro em tempo de execucao a partir de uma cena vazia.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Auto() { if (FindObjectOfType<GameBootstrap>() == null) new GameObject("GameBootstrap").AddComponent<GameBootstrap>(); }
        void Start() { StartCoroutine(Run()); }

        IEnumerator Run()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            foreach (var c in FindObjectsOfType<Camera>()) Destroy(c.gameObject);
            var save = SaveSystem.Load();
            if (save != null) { Quality.Level = Mathf.Clamp(save.quality, 0, 3); Quality.Fps = save.fps > 0 ? save.fps : 30; } else Quality.Level = Quality.Auto();
            var hud = Hud.Create(); hud.ShowLoading(true, "CARREGANDO CIDADE...");
            yield return null;
            Mats.Init();
            var dn = new GameObject("World").AddComponent<DayNightWeather>();
            if (save != null) { dn.Hour = save.hour; dn.Weather = save.weather; }
            var city = new GameObject("City").AddComponent<CityGenerator>();
            city.Generate();
            yield return null;
            Vector3 spawn = CityGenerator.SpawnPos;
            if (save != null) spawn = new Vector3(save.px, save.py + .5f, save.pz);
            var player = PlayerController.Create(spawn);
            if (save != null) player.SetYaw(save.yaw);
            city.Streamer.Target = player.transform;
            Quality.Apply();
            city.Streamer.Tick(true);
            yield return null;
            CityGenerator.SpawnStarterVehicles();
            new GameObject("Touch").AddComponent<TouchControls>();
            new GameObject("Traffic").AddComponent<TrafficManager>();
            var mm = new GameObject("Missions").AddComponent<MissionManager>();
            if (save != null) mm.Restore(save.active, save.step, save.done, save.money);
            new GameObject("Events").AddComponent<RandomEvents>();
            new GameObject("Save").AddComponent<SaveSystem>();
            Quality.Apply();
            hud.ShowLoading(false);
            hud.Toast("Bem-vindo, oficial. Explore a cidade.", 4f);
        }
    }
}
