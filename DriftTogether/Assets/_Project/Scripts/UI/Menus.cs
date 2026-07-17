using DriftTogether.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DriftTogether.UI
{
    /// <summary>Shared settings widgets (master volume, camera smoothing).</summary>
    public static class SettingsPanelBuilder
    {
        public static RectTransform Build(Transform parent, System.Action onBack)
        {
            var panel = UIBuilder.CreatePanel(parent, "SettingsPanel", UIBuilder.Panel);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 460f));

            var title = UIBuilder.CreateText(panel, "Title", "Настройки", 40, UIBuilder.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(500f, 50f));

            var volLabel = UIBuilder.CreateText(panel, "VolLabel", "Общая громкость", 26, UIBuilder.TextDim);
            UIBuilder.Anchor(volLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(500f, 32f));
            var volSlider = UIBuilder.CreateSlider(panel, "VolSlider", GameSettings.MasterVolume,
                v => GameSettings.MasterVolume = v);
            UIBuilder.Anchor((RectTransform)volSlider.transform, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(440f, 36f));

            var camLabel = UIBuilder.CreateText(panel, "CamLabel", "Отзывчивость камеры", 26, UIBuilder.TextDim);
            UIBuilder.Anchor(camLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(500f, 32f));
            var camSlider = UIBuilder.CreateSlider(panel, "CamSlider", GameSettings.CameraSmoothing,
                v => GameSettings.CameraSmoothing = v);
            UIBuilder.Anchor((RectTransform)camSlider.transform, new Vector2(0.5f, 1f), new Vector2(0f, -278f), new Vector2(440f, 36f));

            var back = UIBuilder.CreateButton(panel, "Back", "Назад", new Vector2(280f, 62f), onBack);
            UIBuilder.Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(280f, 62f));

            return panel;
        }
    }

    /// <summary>Pause menu overlay: continue, restart, main menu.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        RectTransform _mainPanel;
        RectTransform _settingsPanel;
        public bool IsOpen { get; private set; }

        public System.Action OnContinue;
        public System.Action OnRestart;
        public System.Action OnMainMenu;

        public static PauseMenu Create()
        {
            var canvas = UIBuilder.CreateCanvas("PauseMenu", 50);
            var menu = canvas.gameObject.AddComponent<PauseMenu>();
            menu.BuildWidgets(canvas.transform);
            return menu;
        }

        void BuildWidgets(Transform root)
        {
            var dim = UIBuilder.CreatePanel(root, "Dim", new Color(0f, 0f, 0f, 0.55f));
            UIBuilder.Stretch(dim);

            _mainPanel = UIBuilder.CreatePanel(root, "Panel", UIBuilder.Panel);
            UIBuilder.Anchor(_mainPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 420f));

            var title = UIBuilder.CreateText(_mainPanel, "Title", "Пауза", 44, UIBuilder.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(400f, 54f));

            CreateMenuButton("Продолжить", -140f, () => OnContinue?.Invoke());
            CreateMenuButton("Начать заново", -220f, () => OnRestart?.Invoke());
            CreateMenuButton("Настройки", -300f, OpenSettings);
            CreateMenuButton("В главное меню", -380f, () => OnMainMenu?.Invoke());

            _settingsPanel = SettingsPanelBuilder.Build(root, CloseSettings);
            _settingsPanel.gameObject.SetActive(false);

            gameObject.SetActive(false);
        }

        void CreateMenuButton(string label, float y, System.Action action)
        {
            var button = UIBuilder.CreateButton(_mainPanel, label, label, new Vector2(360f, 62f), action);
            UIBuilder.Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(360f, 62f));
        }

        void OpenSettings()
        {
            _mainPanel.gameObject.SetActive(false);
            _settingsPanel.gameObject.SetActive(true);
        }

        void CloseSettings()
        {
            _settingsPanel.gameObject.SetActive(false);
            _mainPanel.gameObject.SetActive(true);
        }

        public void Open()
        {
            IsOpen = true;
            gameObject.SetActive(true);
            _mainPanel.gameObject.SetActive(true);
            _settingsPanel.gameObject.SetActive(false);
        }

        public void Close()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }
    }

    /// <summary>End-of-run results: time, route, mushrooms, collisions, respawns.</summary>
    public sealed class ResultsScreen : MonoBehaviour
    {
        public System.Action OnRestart;
        public System.Action OnMainMenu;

        public static ResultsScreen Create(RunStats stats)
        {
            var canvas = UIBuilder.CreateCanvas("Results", 60);
            var screen = canvas.gameObject.AddComponent<ResultsScreen>();
            screen.BuildWidgets(canvas.transform, stats);
            return screen;
        }

        void BuildWidgets(Transform root, RunStats stats)
        {
            var dim = UIBuilder.CreatePanel(root, "Dim", new Color(0f, 0f, 0f, 0.6f));
            UIBuilder.Stretch(dim);

            var panel = UIBuilder.CreatePanel(root, "Panel", UIBuilder.Panel);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 560f));

            var title = UIBuilder.CreateText(panel, "Title", "Мы приплыли!", 46, UIBuilder.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(560f, 56f));

            int minutes = Mathf.FloorToInt(stats.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(stats.ElapsedSeconds % 60f);
            string[] lines =
            {
                $"Время: {minutes:00}:{seconds:00}",
                $"Маршрут: {stats.RouteDisplayName()}",
                $"Светящиеся грибы: {Mathf.Min(stats.Mushrooms, MushroomTracker.Goal)}/{MushroomTracker.Goal}",
                $"Столкновения: {stats.Collisions}",
                $"Восстановления: {stats.Respawns}"
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var line = UIBuilder.CreateText(panel, $"Line{i}", lines[i], 30, UIBuilder.TextMain);
                UIBuilder.Anchor(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f - i * 52f), new Vector2(560f, 40f));
            }

            var restart = UIBuilder.CreateButton(panel, "Restart", "Начать заново", new Vector2(360f, 62f), () => OnRestart?.Invoke());
            UIBuilder.Anchor((RectTransform)restart.transform, new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(360f, 62f));
            var menu = UIBuilder.CreateButton(panel, "Menu", "В главное меню", new Vector2(360f, 62f), () => OnMainMenu?.Invoke());
            UIBuilder.Anchor((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(360f, 62f));
        }
    }

    /// <summary>Short controls hint shown at level start.</summary>
    public sealed class IntroHint : MonoBehaviour
    {
        public static IntroHint Create()
        {
            var canvas = UIBuilder.CreateCanvas("IntroHint", 40);
            var hint = canvas.gameObject.AddComponent<IntroHint>();

            var panel = UIBuilder.CreatePanel(canvas.transform, "Panel", UIBuilder.PanelSoft);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(880f, 200f));

            var title = UIBuilder.CreateText(panel, "Title", "Туманный лес", 36, UIBuilder.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(700f, 44f));

            var text = UIBuilder.CreateText(panel, "Text",
                "W / ↑ — грести   S / ↓ — тормозить   A, D / ← → — поворачивать\n" +
                "E — взаимодействовать   R — вернуться на точку   Esc — пауза\n" +
                "Геймпад: левый стик — движение, нижняя кнопка — действие",
                24, UIBuilder.TextMain);
            UIBuilder.Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(820f, 120f));

            hint.StartCoroutine(hint.AutoHide(panel.gameObject));
            return hint;
        }

        System.Collections.IEnumerator AutoHide(GameObject panel)
        {
            yield return new WaitForSeconds(9f);
            var group = panel.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                group.alpha = 1f - t;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
