#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class BodycamSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    static BodycamSetup()
    {
        EditorApplication.delayCall += () => { if (!File.Exists(ScenePath)) Setup(); };
    }

    [MenuItem("Bodycam/1 - Configurar Projeto")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Settings"); Directory.CreateDirectory("Assets/Scenes");

        // URP
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rd, "Assets/Settings/BodycamRenderer.asset");
            var urp = UniversalRenderPipelineAsset.Create(rd);
            urp.supportsHDR = true;
            AssetDatabase.CreateAsset(urp, "Assets/Settings/BodycamURP.asset");
            GraphicsSettings.renderPipelineAsset = urp;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }
        }

        // Shaders usados via Shader.Find em tempo de execucao precisam ir no build
        var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var arr = gs.FindProperty("m_AlwaysIncludedShaders");
        foreach (var n in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Skybox/Procedural", "Sprites/Default", "GUI/Text Shader", "UI/Default" })
        {
            var sh = Shader.Find(n); if (sh == null) continue;
            bool has = false;
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
            if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh; }
        }
        gs.ApplyModifiedProperties();

        // Android
        PlayerSettings.companyName = "BodycamStudio";
        PlayerSettings.productName = "BODYCAM Open World";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.bodycamstudio.openworld");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

        // Cena
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameBootstrap").AddComponent<Bodycam.GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[Bodycam] Projeto configurado. Abra Assets/Scenes/Main.unity e aperte Play.");
    }

    [MenuItem("Bodycam/2 - Gerar APK")]
    public static void BuildApk()
    {
        Setup();
        Directory.CreateDirectory("Builds");
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        var opt = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/BodycamOpenWorld.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        var rep = BuildPipeline.BuildPlayer(opt);
        Debug.Log("[Bodycam] Build: " + rep.summary.result + " -> " + rep.summary.outputPath);
        if (rep.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(rep.summary.outputPath);
    }
}
#endif
