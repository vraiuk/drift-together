using System.Net;
using System.Net.Sockets;
using DriftTogether.Coop;
using DriftTogether.Coop.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DriftTogether.UI
{
    /// <summary>Co-op entry: create / join session, lobby, departure (UC-01).</summary>
    public sealed class CoopMenuScreen : MonoBehaviour
    {
        RectTransform _menuPanel;      // create/join
        RectTransform _lobbyPanel;
        Text _statusText;
        Text _lobbyTitle;
        Text _lobbyPlayers;
        Text _lobbyCode;
        InputField _codeInput;
        RectTransform _hostButtons;
        RectTransform _returnTarget;
        float _refreshTimer;

        public static void Show(Transform canvasRoot, RectTransform returnTo)
        {
            var existing = FindFirstObjectByType<CoopMenuScreen>();
            if (existing != null)
            {
                existing._returnTarget = returnTo;
                existing._menuPanel.gameObject.SetActive(true);
                returnTo.gameObject.SetActive(false);
                return;
            }
            var screen = canvasRoot.gameObject.AddComponent<CoopMenuScreen>();
            screen._returnTarget = returnTo;
            screen.Build(canvasRoot);
            returnTo.gameObject.SetActive(false);
        }

        void Build(Transform root)
        {
            _menuPanel = UIBuilder.CreatePanel(root, "CoopPanel", UIBuilder.Panel);
            UIBuilder.Anchor(_menuPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 560f));

            var title = UIBuilder.CreateText(_menuPanel, "Title", "Кооператив — Сплав", 40,
                UIBuilder.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 50f));

            var subtitle = UIBuilder.CreateText(_menuPanel, "Subtitle",
                "2–4 игрока на одном плоту. Создайте сплав и поделитесь кодом,\nили введите код друга.",
                22, UIBuilder.TextDim);
            UIBuilder.Anchor(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(620f, 60f));

            var createRelay = UIBuilder.CreateButton(_menuPanel, "CreateRelay",
                "Создать сплав (код для друзей)", new Vector2(480f, 60f), () => _ = CreateRelay());
            UIBuilder.Anchor((RectTransform)createRelay.transform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(480f, 60f));

            var createDirect = UIBuilder.CreateButton(_menuPanel, "CreateDirect",
                "Создать (локальная сеть / IP)", new Vector2(480f, 60f), CreateDirect);
            UIBuilder.Anchor((RectTransform)createDirect.transform, new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(480f, 60f));

            _codeInput = CreateInput(_menuPanel, "CodeInput", "Код сессии или IP-адрес",
                new Vector2(0f, -336f), new Vector2(480f, 54f));

            var join = UIBuilder.CreateButton(_menuPanel, "Join", "Войти", new Vector2(480f, 60f), () => _ = Join());
            UIBuilder.Anchor((RectTransform)join.transform, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(480f, 60f));

            _statusText = UIBuilder.CreateText(_menuPanel, "Status", "", 22, new Color(1f, 0.75f, 0.5f));
            UIBuilder.Anchor(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(620f, 52f));

            var back = UIBuilder.CreateButton(_menuPanel, "Back", "Назад", new Vector2(260f, 56f), CloseToMainMenu);
            UIBuilder.Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(260f, 56f));

            BuildLobby(root);

            SessionManager.Ensure().ConnectionFailed += OnFailed;
        }

        void BuildLobby(Transform root)
        {
            _lobbyPanel = UIBuilder.CreatePanel(root, "LobbyPanel", UIBuilder.Panel);
            UIBuilder.Anchor(_lobbyPanel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 520f));

            _lobbyTitle = UIBuilder.CreateText(_lobbyPanel, "Title", "Сбор команды", 40,
                UIBuilder.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(_lobbyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 50f));

            _lobbyCode = UIBuilder.CreateText(_lobbyPanel, "Code", "", 54, UIBuilder.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(_lobbyCode.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(600f, 66f));

            _lobbyPlayers = UIBuilder.CreateText(_lobbyPanel, "Players", "Игроков: 1 / 4", 30, UIBuilder.TextMain);
            UIBuilder.Anchor(_lobbyPlayers.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(600f, 40f));

            _hostButtons = UIBuilder.CreateInvisiblePanel(_lobbyPanel, "HostButtons");
            UIBuilder.Anchor(_hostButtons, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(520f, 200f));

            var start = UIBuilder.CreateButton(_hostButtons, "Start", "Отчалить (с начала)",
                new Vector2(460f, 58f), () => Depart(false));
            UIBuilder.Anchor((RectTransform)start.transform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(460f, 58f));

            var startCp = UIBuilder.CreateButton(_hostButtons, "StartCp", "Отчалить с чекпоинта",
                new Vector2(460f, 58f), () => Depart(true));
            UIBuilder.Anchor((RectTransform)startCp.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(460f, 58f));
            _checkpointButton = startCp.gameObject;

            var leave = UIBuilder.CreateButton(_lobbyPanel, "Leave", "Покинуть", new Vector2(260f, 56f), LeaveLobby);
            UIBuilder.Anchor((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(260f, 56f));

            _lobbyPanel.gameObject.SetActive(false);
        }

        GameObject _checkpointButton;

        InputField CreateInput(Transform parent, string name, string placeholder,
            Vector2 pos, Vector2 size)
        {
            var panel = UIBuilder.CreatePanel(parent, name, new Color(0.1f, 0.17f, 0.18f, 1f));
            UIBuilder.Anchor(panel, new Vector2(0.5f, 1f), pos, size);

            var text = UIBuilder.CreateText(panel, "Text", "", 28, UIBuilder.TextMain, TextAnchor.MiddleLeft);
            UIBuilder.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-16f, -4f);

            var ph = UIBuilder.CreateText(panel, "Placeholder", placeholder, 28,
                new Color(0.5f, 0.58f, 0.57f), TextAnchor.MiddleLeft, FontStyle.Italic);
            UIBuilder.Stretch(ph.rectTransform);
            ph.rectTransform.offsetMin = new Vector2(16f, 4f);
            ph.rectTransform.offsetMax = new Vector2(-16f, -4f);

            var input = panel.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = 24;
            return input;
        }

        // ---------- Actions ----------

        async System.Threading.Tasks.Task CreateRelay()
        {
            _statusText.text = "Создаём сессию…";
            bool ok = await SessionManager.Ensure().HostRelayAsync();
            if (ok)
                OpenLobby(asHost: true);
        }

        void CreateDirect()
        {
            bool ok = SessionManager.Ensure().HostDirect();
            if (ok)
                OpenLobby(asHost: true);
            else
                _statusText.text = "Не удалось открыть порт 7777";
        }

        async System.Threading.Tasks.Task Join()
        {
            string value = _codeInput.text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                _statusText.text = "Введите код сессии или IP-адрес хоста";
                return;
            }
            _statusText.text = "Подключаемся…";
            bool ok = value.Contains(".")
                ? SessionManager.Ensure().JoinDirect(value)
                : await SessionManager.Ensure().JoinRelayAsync(value);
            if (ok)
                OpenLobby(asHost: false);
        }

        void OpenLobby(bool asHost)
        {
            CoopBootstrap.CoopRequested = true;
            _statusText.text = "";
            _menuPanel.gameObject.SetActive(false);
            _lobbyPanel.gameObject.SetActive(true);
            _hostButtons.gameObject.SetActive(asHost);
            _lobbyTitle.text = asHost ? "Сбор команды" : "Ждём, пока хост отчалит…";

            var session = SessionManager.Instance;
            if (asHost)
            {
                _lobbyCode.text = string.IsNullOrEmpty(session.JoinCode)
                    ? "IP: " + LocalIp()
                    : session.JoinCode;
                _checkpointButton.SetActive(CoopBootstrap.TryRestoreFromDisk());
            }
            else
            {
                _lobbyCode.text = "";
            }
        }

        void Depart(bool fromCheckpoint)
        {
            CoopBootstrap.StartFromCheckpoint = fromCheckpoint;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsHost)
                nm.SceneManager.LoadScene("River", LoadSceneMode.Single);
        }

        void LeaveLobby()
        {
            SessionManager.Instance?.Shutdown();
            CoopBootstrap.CoopRequested = false;
            _lobbyPanel.gameObject.SetActive(false);
            _menuPanel.gameObject.SetActive(true);
        }

        void CloseToMainMenu()
        {
            _menuPanel.gameObject.SetActive(false);
            if (_returnTarget != null)
                _returnTarget.gameObject.SetActive(true);
        }

        void OnFailed(string reason)
        {
            if (this == null || _statusText == null)
                return;
            _statusText.text = reason;
            if (_lobbyPanel != null && _lobbyPanel.gameObject.activeSelf)
            {
                _lobbyPanel.gameObject.SetActive(false);
                _menuPanel.gameObject.SetActive(true);
            }
            CoopBootstrap.CoopRequested = false;
        }

        void Update()
        {
            if (_lobbyPanel == null || !_lobbyPanel.gameObject.activeSelf)
                return;
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f)
                return;
            _refreshTimer = 0.5f;

            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;
            int players = nm.IsServer
                ? nm.ConnectedClientsIds.Count
                : (nm.IsConnectedClient ? -1 : 0);
            _lobbyPlayers.text = players >= 0
                ? $"Игроков: {players} / {SessionManager.MaxPlayers}"
                : "Подключено. Ждём старта…";
        }

        static string LocalIp()
        {
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                        return ip.ToString();
            }
            catch { /* best effort */ }
            return "127.0.0.1";
        }

        void OnDestroy()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.ConnectionFailed -= OnFailed;
        }
    }

    /// <summary>Controls hint for the co-op run.</summary>
    public sealed class CoopIntroHint : MonoBehaviour
    {
        public static void Create()
        {
            var canvas = UIBuilder.CreateCanvas("CoopIntroHint", 40);
            var hint = canvas.gameObject.AddComponent<CoopIntroHint>();

            var panel = UIBuilder.CreatePanel(canvas.transform, "Panel", UIBuilder.PanelSoft);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(920f, 200f));

            var title = UIBuilder.CreateText(panel, "Title", "Сплав начался", 36, UIBuilder.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(700f, 44f));

            var text = UIBuilder.CreateText(panel, "Text",
                "WASD — ходить по плоту   E — встать к посту / действие   F — толкнуть\n" +
                "Руль: A/D — курс.  Вёсла: W — гребок, S — табань\n" +
                "Столкнулись? Держите E у костровой чаши — ремонт",
                24, UIBuilder.TextMain);
            UIBuilder.Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(860f, 120f));

            hint.StartCoroutine(hint.AutoHide(panel.gameObject));
        }

        System.Collections.IEnumerator AutoHide(GameObject panel)
        {
            yield return new WaitForSeconds(11f);
            var group = panel.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                group.alpha = 1f - t;
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    /// <summary>«Отчёт о сплаве» — stats and meme nominations (UC-15).</summary>
    public static class CoopReportScreen
    {
        public static void Show(CoopReportPayload payload, bool isHost,
            System.Action onRestart, System.Action onMenu)
        {
            var stats = payload.ToStats();
            var canvas = UIBuilder.CreateCanvas("CoopReport", 70);

            var dim = UIBuilder.CreatePanel(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
            UIBuilder.Stretch(dim);

            var panel = UIBuilder.CreatePanel(canvas.transform, "Panel", UIBuilder.Panel);
            UIBuilder.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 700f));

            var title = UIBuilder.CreateText(panel, "Title", "Отчёт о сплаве", 46, UIBuilder.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(650f, 56f));

            int minutes = Mathf.FloorToInt(stats.ElapsedSeconds / 60f);
            int seconds = Mathf.FloorToInt(stats.ElapsedSeconds % 60f);
            int overboardTotal = 0;
            foreach (var p in stats.Players)
                overboardTotal += p.OverboardCount;

            string[] lines =
            {
                $"Время: {minutes:00}:{seconds:00}   ·   Маршрут: {stats.RouteDisplayName()}",
                $"Состояние плота: {Coop.RaftCondition.Describe(stats.HullAtFinish, stats.ModulesBuilt)}",
                $"Столкновений плота: {stats.RaftCollisions}   ·   Прочность на финише: {stats.HullAtFinish}/{Coop.Net.RaftController.MaxHull}",
                $"Падений за борт: {overboardTotal}   ·   Переворотов: {stats.Capsizes}   ·   Плот уплывал: {stats.RaftLosses}" +
                    (stats.PortageUsed ? "   ·   Волок: да" : "") +
                    (stats.ModulesBuilt > 0 ? $"   ·   Модулей построено: {stats.ModulesBuilt}" : "")
            };
            for (int i = 0; i < lines.Length; i++)
            {
                var line = UIBuilder.CreateText(panel, $"Line{i}", lines[i], 26, UIBuilder.TextMain);
                UIBuilder.Anchor(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -130f - i * 42f), new Vector2(690f, 34f));
            }

            var nomTitle = UIBuilder.CreateText(panel, "NomTitle", "Номинации", 30, UIBuilder.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Anchor(nomTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -272f), new Vector2(650f, 40f));

            var noms = stats.Nominations();
            for (int i = 0; i < noms.Count && i < 6; i++)
            {
                var nom = UIBuilder.CreateText(panel, $"Nom{i}",
                    $"«{noms[i].title}» — {noms[i].playerName}", 26, new Color(1f, 0.9f, 0.6f));
                UIBuilder.Anchor(nom.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -316f - i * 40f), new Vector2(690f, 34f));
            }

            if (isHost)
            {
                var again = UIBuilder.CreateButton(panel, "Again", "Ещё сплав", new Vector2(380f, 60f), onRestart);
                UIBuilder.Anchor((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(380f, 60f));
            }
            var menu = UIBuilder.CreateButton(panel, "Menu", "В главное меню", new Vector2(380f, 60f), onMenu);
            UIBuilder.Anchor((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(380f, 60f));
        }
    }
}
