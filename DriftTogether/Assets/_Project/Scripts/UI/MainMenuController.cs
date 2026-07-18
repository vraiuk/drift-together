using DriftTogether.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftTogether.UI
{
    /// <summary>
    /// Builds and runs the main menu scene UI. Attached to the scene root by
    /// the editor bootstrap; everything else is created in code.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        RectTransform _menuPanel;
        RectTransform _settingsPanel;
        Canvas _canvas;

        void Start()
        {
            Time.timeScale = 1f;
            AudioManager.Ensure();
            AudioManager.Instance.SetWaterPresence(0.25f);
            GameSettings.ApplyToListener();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = new Color(0.13f, 0.2f, 0.22f);

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.16f, 0.18f);

            UIBuilder.EnsureEventSystem();
            BuildUI();

            // Automated smoke run: behave like a player pressing «Играть».
            if (Core.SmokeAutopilot.CommandLineRequested())
                Invoke(nameof(StartGame), 1.5f);
        }

        void BuildUI()
        {
            var canvas = UIBuilder.CreateCanvas("MenuCanvas");
            _canvas = canvas;

            var bg = UIBuilder.CreatePanel(canvas.transform, "Backdrop", new Color(0.07f, 0.12f, 0.14f, 1f));
            UIBuilder.Stretch(bg);

            // Soft "fog bands" for a bit of depth.
            for (int i = 0; i < 3; i++)
            {
                var band = UIBuilder.CreatePanel(canvas.transform, $"FogBand{i}",
                    new Color(0.2f, 0.32f, 0.33f, 0.10f + i * 0.05f));
                UIBuilder.Anchor(band, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f + i * 90f), new Vector2(2200f, 120f + i * 60f));
                band.localRotation = Quaternion.Euler(0f, 0f, -3f + i * 2.5f);
            }

            var title = UIBuilder.CreateText(canvas.transform, "Title", "Drift Together", 96, UIBuilder.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 110f));

            var subtitle = UIBuilder.CreateText(canvas.transform, "Subtitle", "Туманный лес · путешествие с Тапком-Тимом", 30, UIBuilder.TextDim);
            UIBuilder.Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 44f));

            _menuPanel = UIBuilder.CreateInvisiblePanel(canvas.transform, "Buttons");
            UIBuilder.Anchor(_menuPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(400f, 320f));

            CreateMenuButton("Играть", 140f, StartGame);
            CreateMenuButton("Кооператив — Сплав", 60f, OpenCoop);
            CreateMenuButton("Настройки", -20f, OpenSettings);
            CreateMenuButton("Выход", -100f, QuitGame);

            _settingsPanel = SettingsPanelBuilder.Build(canvas.transform, CloseSettings);
            _settingsPanel.gameObject.SetActive(false);

            var hint = UIBuilder.CreateText(canvas.transform, "Hint", "Спокойное аркадное приключение на 10 минут", 22, UIBuilder.TextDim);
            UIBuilder.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 30f));
        }

        void CreateMenuButton(string label, float y, System.Action action)
        {
            var button = UIBuilder.CreateButton(_menuPanel, label, label, new Vector2(360f, 68f), action);
            UIBuilder.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(360f, 68f));
        }

        void StartGame()
        {
            Coop.CoopBootstrap.CoopRequested = false;
            SceneManager.LoadScene("River");
        }

        void OpenCoop()
        {
            CoopMenuScreen.Show(_canvas != null ? _canvas.transform : transform, _menuPanel);
        }

        void OpenSettings()
        {
            _menuPanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(true);
        }

        void CloseSettings()
        {
            _settingsPanel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(true);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
