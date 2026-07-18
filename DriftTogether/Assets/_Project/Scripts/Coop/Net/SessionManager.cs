using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// Owns the NGO lifecycle for co-op sessions (UC-01). Two connection
    /// paths: Unity Relay with a share-able join code (needs the project to
    /// be linked to Unity Cloud) and direct IP (always available). A local
    /// loopback mode backs automated tests and the co-op smoke run.
    /// </summary>
    public sealed class SessionManager : MonoBehaviour
    {
        public const int MaxPlayers = 4;

        public static SessionManager Instance { get; private set; }

        public string JoinCode { get; private set; } = "";
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsConnected => NetworkManager.Singleton != null &&
                                   (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient);

        /// <summary>Set once the raft has launched; new connections are rejected.</summary>
        public bool RaftLaunched;

        public event Action<string> ConnectionFailed;
        public event Action PlayersChanged;

        NetworkManager _networkManager;

        public static SessionManager Ensure()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("SessionManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<SessionManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        NetworkManager EnsureNetworkManager()
        {
            if (_networkManager != null)
                return _networkManager;

            var go = new GameObject("NetworkManager");
            DontDestroyOnLoad(go); // must stay a root object: NGO forbids nesting
            _networkManager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            _networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = true,
                ConnectionApproval = true,
                TickRate = 60
            };

            RegisterNetPrefabs();

            _networkManager.ConnectionApprovalCallback = (request, response) =>
            {
                bool full = _networkManager.ConnectedClientsIds.Count >= MaxPlayers;
                response.Approved = !full && !RaftLaunched;
                response.CreatePlayerObject = false;
                response.Reason = RaftLaunched ? "Сплав уже начался" : "Плот полон (4 игрока)";
            };
            _networkManager.OnClientConnectedCallback += _ => PlayersChanged?.Invoke();
            _networkManager.OnClientDisconnectCallback += OnClientDisconnect;
            return _networkManager;
        }

        void RegisterNetPrefabs()
        {
            foreach (string name in new[] { "Net/Raft", "Net/PlayerAvatar" })
            {
                var prefab = Resources.Load<GameObject>(name);
                if (prefab != null)
                    _networkManager.AddNetworkPrefab(prefab);
                else
                    Debug.LogWarning($"[Session] network prefab missing: {name}");
            }
        }

        void OnClientDisconnect(ulong clientId)
        {
            PlayersChanged?.Invoke();
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsHost && clientId == nm.LocalClientId)
            {
                // We were disconnected (host left or rejected).
                string reason = string.IsNullOrEmpty(nm.DisconnectReason)
                    ? "Соединение потеряно"
                    : nm.DisconnectReason;
                ConnectionFailed?.Invoke(reason);
            }
        }

        public GameObject LoadNetPrefab(string name) => Resources.Load<GameObject>("Net/" + name);

        // ---------- Relay path ----------

        public async Task<bool> HostRelayAsync()
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                ConnectionFailed?.Invoke(
                    "Relay не настроен: проект не привязан к Unity Cloud. Используйте прямое подключение по IP.");
                return false;
            }
            try
            {
                var nm = EnsureNetworkManager();
                await EnsureSignedInAsync();
                var allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
                RaftLaunched = false;
                return nm.StartHost();
            }
            catch (Exception e)
            {
                ConnectionFailed?.Invoke("Не удалось создать сессию: " + e.Message);
                return false;
            }
        }

        public async Task<bool> JoinRelayAsync(string code)
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                ConnectionFailed?.Invoke(
                    "Relay не настроен: проект не привязан к Unity Cloud. Используйте прямое подключение по IP.");
                return false;
            }
            try
            {
                var nm = EnsureNetworkManager();
                await EnsureSignedInAsync();
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
                transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
                return nm.StartClient();
            }
            catch (Exception e)
            {
                ConnectionFailed?.Invoke("Не удалось войти по коду: " + e.Message);
                return false;
            }
        }

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ---------- Direct / local path ----------

        public bool HostDirect(ushort port = 7777)
        {
            var nm = EnsureNetworkManager();
            var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
            transport.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            JoinCode = "";
            RaftLaunched = false;
            return nm.StartHost();
        }

        public bool JoinDirect(string ip, ushort port = 7777)
        {
            var nm = EnsureNetworkManager();
            var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
            transport.SetConnectionData(ip.Trim(), port);
            return nm.StartClient();
        }

        /// <summary>Loopback host for PlayMode tests and the co-op smoke run.</summary>
        public bool HostLocal()
        {
            var nm = EnsureNetworkManager();
            var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
            transport.SetConnectionData("127.0.0.1", 7788, "127.0.0.1");
            RaftLaunched = false;
            return nm.StartHost();
        }

        public void Shutdown()
        {
            RaftLaunched = false;
            JoinCode = "";
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
        }

        public int PlayerCount =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                ? NetworkManager.Singleton.ConnectedClientsIds.Count
                : 0;
    }
}
