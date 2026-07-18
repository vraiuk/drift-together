using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DriftTogether.EditorTools
{
    /// <summary>
    /// One-shot project setup, executed headlessly via -executeMethod:
    /// URP assets, materials, scenes, build settings, player settings.
    /// </summary>
    public static class ProjectConfigurator
    {
        const string MaterialsDir = "Assets/_Project/Art/Resources/Materials";
        const string ScenesDir = "Assets/_Project/Scenes";
        const string SettingsDir = "Assets/_Project/Settings";

        public static void Configure()
        {
            try
            {
                Debug.Log("[Configurator] starting");
                SetupPlayerSettings();
                SetupInputHandler();
                SetupUrp();
                CreateMaterials();
                CreateScenes();
                CreateCoopPrefabs();
                SetupBuildScenes();
                AssetDatabase.SaveAssets();
                Debug.Log("[Configurator] done");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Configurator] FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "DriftTogether";
            PlayerSettings.productName = "Drift Together";
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.drifttogether.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static void SetupInputHandler()
        {
            // 0 = legacy, 1 = Input System, 2 = both. UI uses the legacy
            // StandaloneInputModule, gameplay reads the Input System, so: both.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0)
            {
                Debug.LogWarning("[Configurator] ProjectSettings.asset not loadable");
                return;
            }
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.Log("[Configurator] activeInputHandler = Both");
            }
        }

        static void SetupUrp()
        {
            EnsureFolder(SettingsDir);

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, SettingsDir + "/ForwardRenderer.asset");

            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 90f;
            AssetDatabase.CreateAsset(pipeline, SettingsDir + "/URP-Pipeline.asset");

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);
            Debug.Log("[Configurator] URP assigned");
        }

        static void CreateMaterials()
        {
            EnsureFolder(MaterialsDir);

            CreateLit("Water", new Color(0.09f, 0.28f, 0.32f, 0.88f), smoothness: 0.92f, transparent: true);
            CreateLit("Bank", new Color(0.16f, 0.29f, 0.2f), smoothness: 0.1f);
            CreateLit("ForestFloor", new Color(0.08f, 0.13f, 0.12f), smoothness: 0.05f);
            CreateLit("Rock", new Color(0.42f, 0.47f, 0.5f), smoothness: 0.25f);
            CreateLit("Wood", new Color(0.4f, 0.28f, 0.18f), smoothness: 0.15f);
            CreateLit("Sand", new Color(0.65f, 0.6f, 0.45f), smoothness: 0.1f);
            CreateLit("KayakHull", new Color(0.78f, 0.36f, 0.22f), smoothness: 0.55f);
            CreateLit("KayakTrim", new Color(0.9f, 0.85f, 0.68f), smoothness: 0.4f);
            CreateLit("Slipper", new Color(0.52f, 0.44f, 0.52f), smoothness: 0.2f);
            CreateLit("EyeWhite", new Color(0.95f, 0.95f, 0.92f), smoothness: 0.7f);
            CreateLit("EyePupil", new Color(0.05f, 0.05f, 0.06f), smoothness: 0.8f);
            CreateLit("TreeTrunk", new Color(0.23f, 0.17f, 0.13f), smoothness: 0.1f);
            CreateLit("Foliage", new Color(0.12f, 0.3f, 0.24f), smoothness: 0.1f);
            CreateLit("MushroomStem", new Color(0.8f, 0.85f, 0.78f), smoothness: 0.3f);
            CreateLit("MushroomCap", new Color(0.35f, 0.9f, 0.7f), smoothness: 0.5f,
                emission: new Color(0.3f, 1f, 0.7f) * 2.4f);
            CreateLit("FireGlow", new Color(1f, 0.55f, 0.15f), smoothness: 0.3f,
                emission: new Color(1f, 0.45f, 0.1f) * 3.2f);
            CreateLit("FinishGlow", new Color(1f, 0.85f, 0.45f), smoothness: 0.3f,
                emission: new Color(1f, 0.8f, 0.35f) * 2.6f);
            CreateUnlit("Foam", new Color(0.9f, 0.97f, 1f, 0.55f), transparent: true);
            CreateParticleMat("WaterSplash", new Color(0.75f, 0.9f, 0.98f, 0.65f));

            Debug.Log("[Configurator] materials created");
        }

        static void CreateLit(string name, Color color, float smoothness = 0.35f,
            float metallic = 0f, Color? emission = null, bool transparent = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (transparent)
                MakeTransparent(mat);
            SaveMaterial(mat, name);
        }

        static void CreateUnlit(string name, Color color, bool transparent = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            if (transparent)
                MakeTransparent(mat);
            SaveMaterial(mat, name);
        }

        static void CreateParticleMat(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            MakeTransparent(mat);
            SaveMaterial(mat, name);
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);   // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        static void SaveMaterial(Material mat, string name)
        {
            mat.name = name;
            AssetDatabase.CreateAsset(mat, $"{MaterialsDir}/{name}.mat");
        }

        static void CreateScenes()
        {
            EnsureFolder(ScenesDir);

            // Main menu scene: a camera and the controller that builds the UI.
            var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var menuCam = new GameObject("Main Camera") { tag = "MainCamera" };
            menuCam.AddComponent<Camera>();
            menuCam.AddComponent<AudioListener>();
            var menuRoot = new GameObject("MainMenuRoot");
            menuRoot.AddComponent<UI.MainMenuController>();
            EditorSceneManager.SaveScene(menuScene, ScenesDir + "/MainMenu.unity");

            // River scene: a camera and the GameFlow bootstrap.
            var riverScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameCam = new GameObject("Main Camera") { tag = "MainCamera" };
            gameCam.AddComponent<Camera>();
            gameCam.AddComponent<AudioListener>();
            var gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<Core.GameFlow>();
            EditorSceneManager.SaveScene(riverScene, ScenesDir + "/River.unity");

            Debug.Log("[Configurator] scenes created");
        }

        static void CreateCoopPrefabs()
        {
            const string netDir = "Assets/_Project/Art/Resources/Net";
            EnsureFolder(netDir);

            // Raft: host-driven physics body; visuals are built at runtime.
            var raft = new GameObject("Raft");
            var body = raft.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.useGravity = false;
            var box = raft.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(4f, 0.5f, 3.4f);
            raft.AddComponent<Unity.Netcode.NetworkObject>();
            raft.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            raft.AddComponent<DriftTogether.Coop.Net.RaftController>();
            raft.AddComponent<DriftTogether.Coop.Net.CoopFlow>();
            PrefabUtility.SaveAsPrefabAsset(raft, netDir + "/Raft.prefab");
            UnityEngine.Object.DestroyImmediate(raft);

            // Player avatar: owner-authoritative, no collider (raycast-based motor).
            var avatar = new GameObject("PlayerAvatar");
            avatar.AddComponent<Unity.Netcode.NetworkObject>();
            avatar.AddComponent<DriftTogether.Coop.Net.OwnerNetworkTransform>();
            avatar.AddComponent<DriftTogether.Coop.Net.PlayerAvatar>();
            avatar.AddComponent<DriftTogether.Coop.Net.AvatarFishing>();
            PrefabUtility.SaveAsPrefabAsset(avatar, netDir + "/PlayerAvatar.prefab");
            UnityEngine.Object.DestroyImmediate(avatar);

            // Floating supply crate (spawned on capsize).
            var crate = new GameObject("Crate");
            crate.AddComponent<Unity.Netcode.NetworkObject>();
            crate.AddComponent<Unity.Netcode.Components.NetworkTransform>();
            crate.AddComponent<DriftTogether.Coop.Net.FloatingCrate>();
            PrefabUtility.SaveAsPrefabAsset(crate, netDir + "/Crate.prefab");
            UnityEngine.Object.DestroyImmediate(crate);

            Debug.Log("[Configurator] co-op network prefabs created");
        }

        static void SetupBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesDir + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/River.unity", true)
            };
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ---------- Builds ----------

        static string[] BuildScenePaths() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = BuildScenePaths(),
                locationPathName = "../Builds/Windows/DriftTogether.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            FinishBuild(report, "Windows");
        }

        public static void BuildMac()
        {
            TrySetMacUniversal();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = BuildScenePaths(),
                locationPathName = "../Builds/macOS/DriftTogether.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            });
            FinishBuild(report, "macOS");
        }

        static void TrySetMacUniversal()
        {
            // UnityEditor.OSXStandalone.UserBuildSettings.architecture = x64ARM64,
            // reached via reflection to survive enum type renames between versions.
            try
            {
                var type = typeof(Editor).Assembly.GetType("UnityEditor.OSXStandalone.UserBuildSettings");
                var prop = type?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
                if (prop == null)
                {
                    Debug.LogWarning("[Build] mac architecture property not found; using default");
                    return;
                }
                var enumType = prop.PropertyType;
                var value = Enum.GetValues(enumType).Cast<object>()
                    .FirstOrDefault(v => v.ToString() == "x64ARM64");
                if (value != null)
                {
                    prop.SetValue(null, value);
                    Debug.Log("[Build] macOS architecture set to universal (x64ARM64)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Build] could not set universal mac build: {e.Message}");
            }
        }

        static void FinishBuild(UnityEditor.Build.Reporting.BuildReport report, string label)
        {
            var summary = report.summary;
            Debug.Log($"[Build:{label}] result={summary.result} size={summary.totalSize} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"output={summary.outputPath}");
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
