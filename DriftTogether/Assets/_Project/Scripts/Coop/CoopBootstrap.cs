using DriftTogether.Coop.Net;
using DriftTogether.Core;
using DriftTogether.Player;
using DriftTogether.UI;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftTogether.Coop
{
    /// <summary>
    /// Scene-side orchestrator of a co-op run. Every peer builds the level
    /// and UI locally; the host additionally spawns the raft and avatars and
    /// runs the rules. Static surface is what networked components reach for.
    /// </summary>
    public sealed class CoopBootstrap : MonoBehaviour
    {
        /// <summary>Set by the co-op menu before the River scene loads.</summary>
        public static bool CoopRequested;

        /// <summary>Host-process checkpoint for «Отчалить с чекпоинта».</summary>
        public static Vector3? SavedCheckpoint;
        public static Quaternion SavedCheckpointRotation = Quaternion.identity;
        public static bool StartFromCheckpoint;

        public static CoopBootstrap Active { get; private set; }
        public static RaftController Raft { get; private set; }
        public static RiverFlow Flow { get; private set; }
        public static CoopRunStats Stats { get; private set; }
        public static HUD Hud { get; private set; }

        LevelBuilder _level;
        CameraRig _cameraRig;
        PauseMenu _pauseMenu;
        DialogueQueue _dialogue;
        float _idleLineTimer = 30f;
        float _nervousLineTimer = 10f;
        bool _forkLineShown;
        bool _finished;
        bool _paused;

        public static void Begin(GameObject sceneRoot)
        {
            sceneRoot.AddComponent<CoopBootstrap>();
        }

        void Start()
        {
            Active = this;
            Time.timeScale = 1f;
            AudioManager.Ensure();
            AudioManager.Instance.SetWaterPresence(1f);
            GameSettings.ApplyToListener();

            // Shore colliders (layer 8) are for avatar feet only, not the raft.
            Physics.IgnoreLayerCollision(0, 8, true);

            // Deterministic decorations on every peer.
            Random.InitState(12321);
            _level = gameObject.AddComponent<LevelBuilder>();
            _level.CoopMode = true;
            _level.Build();
            Flow = _level.Flow;

            SetupCamera();
            SetupUI();

            var foam = gameObject.AddComponent<FoamDrifters>();
            foam.Flow = Flow;
            foam.FoamMaterial = GameMaterials.Get("Foam");

            _dialogue = new DialogueQueue(new GameClock());
            BuildScoutZones();
            BuildTouristCampDecor();

            var session = SessionManager.Ensure();
            session.ConnectionFailed += OnConnectionFailed;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsHost)
                HostBegin();
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
            Raft = null;
            Stats = null;
            Hud = null;
            Flow = null;
            if (SessionManager.Instance != null)
                SessionManager.Instance.ConnectionFailed -= OnConnectionFailed;
        }

        void OnConnectionFailed(string reason)
        {
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
            {
                SessionManager.Instance.Shutdown();
                CoopRequested = false;
                SceneManager.LoadScene("MainMenu");
            }
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.farClipPlane = 220f;
            _cameraRig = cam.GetComponent<CameraRig>();
            if (_cameraRig == null)
                _cameraRig = cam.gameObject.AddComponent<CameraRig>();
        }

        void SetupUI()
        {
            UIBuilder.EnsureEventSystem();
            Hud = HUD.Create();
            Hud.SetHullMax(RaftController.MaxHull);
            Hud.SetHull(RaftController.MaxHull);
            Hud.ShowCrewCounter(1);
            Hud.SetWet(false);

            CoopIntroHint.Create();

            _pauseMenu = PauseMenu.Create();
            _pauseMenu.OnContinue = TogglePause;
            _pauseMenu.OnRestart = HostRestart;
            _pauseMenu.OnMainMenu = LeaveToMenu;
        }

        public static void AttachCameraTo(Transform target)
        {
            if (Active == null || Active._cameraRig == null)
                return;
            Active._cameraRig.Target = target;
            Active._cameraRig.SnapBehindTarget();
        }

        // ---------- Host side ----------

        void HostBegin()
        {
            SessionManager.Instance.RaftLaunched = true;
            Stats = new CoopRunStats();

            Vector3 start = StartFromCheckpoint && SavedCheckpoint.HasValue
                ? SavedCheckpoint.Value
                : _level.KayakStartPosition;
            Quaternion rot = StartFromCheckpoint && SavedCheckpoint.HasValue
                ? SavedCheckpointRotation
                : _level.KayakStartRotation;

            var raftPrefab = SessionManager.Instance.LoadNetPrefab("Raft");
            var raftGo = Instantiate(raftPrefab, start, rot);
            raftGo.GetComponent<NetworkObject>().Spawn();
            var raft = raftGo.GetComponent<RaftController>();
            raft.HostAttach(Flow, Stats);

            int colorIndex = 0;
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                SpawnAvatarFor(clientId, colorIndex++);

            NetworkManager.Singleton.OnClientDisconnectCallback += HostOnClientLeft;

            foreach (var gate in _level.RouteGates)
                gate.Entered += route =>
                {
                    Stats.ChooseRoute(route);
                    AudioManager.Instance.SetWaterPresence(route == RiverRoute.NoisyStream ? 1.3f : 0.8f);
                };
            foreach (var zone in _level.CheckpointZones)
                zone.Reached += z =>
                {
                    SavedCheckpoint = z.RespawnPosition;
                    SavedCheckpointRotation = z.RespawnRotation;
                };
            _level.Finish.Finished += HostOnFinish;

            HostSay(LineCategory.Intro, 0f);
            HostSpawnShoreLife();

            if (SmokeAutopilot.CoopCommandLineRequested() || CoopSmokePilot.RequestedByTest)
            {
                var pilot = gameObject.AddComponent<CoopSmokePilot>();
                pilot.WriteReportAndQuit = SmokeAutopilot.CoopCommandLineRequested();
            }
        }

        void SpawnAvatarFor(ulong clientId, int colorIndex)
        {
            var prefab = SessionManager.Instance.LoadNetPrefab("PlayerAvatar");
            Vector3 local = new Vector3(-0.9f + (colorIndex % 2) * 1.8f, 0.55f,
                0.6f - (colorIndex / 2) * 1.2f);
            var go = Instantiate(prefab, Raft != null
                ? Raft.DeckPoint(local)
                : new Vector3(local.x, 0.6f, 4f + local.z), Quaternion.identity);
            var avatar = go.GetComponent<PlayerAvatar>();
            avatar.ColorIndex.Value = colorIndex;
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            Stats.GetOrAdd(clientId);
        }

        void HostOnClientLeft(ulong clientId)
        {
            if (Raft != null)
                Raft.Posts.ReleaseAll(clientId);
        }

        internal static void RegisterRaft(RaftController raft)
        {
            Raft = raft;
            if (Active != null && Active._finished == false && Raft != null &&
                NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost &&
                Stats != null)
            {
                Raft.HostAttach(Flow, Stats);
            }
        }

        internal static void OnRaftHit(float impulse)
        {
            if (Active == null || Raft == null)
                return;
            var flow = Raft.GetComponent<CoopFlow>();
            flow.RaftHitClientRpc(impulse);
            Active.HostSay(LineCategory.Collision, 10f);
        }

        internal static void OnRaftBump(float impulse)
        {
            if (Raft == null)
                return;
            Raft.GetComponent<CoopFlow>().RaftBumpClientRpc();
        }

        internal static void OnOverboard(ulong clientId)
        {
            if (Active == null || Raft == null)
                return;
            Raft.GetComponent<CoopFlow>().SplashClientRpc();
            Active.HostSay(LineCategory.NoisyNerves, 15f);
        }

        internal static bool NearShoreCampfire(Vector3 position)
        {
            if (Active == null || Active._level == null)
                return false;
            Vector3 fire = Active._level.CampfireRespawnPosition + new Vector3(7f, 0f, 0f);
            Vector3 d = position - fire;
            d.y = 0f;
            return d.magnitude < 7f;
        }

        internal static void HostSayFishing()
        {
            Active?.HostSay(LineCategory.Fishing, 20f);
        }

        internal static void HostSayCapsize()
        {
            Active?.HostSay(LineCategory.Capsize, 0f);
        }

        internal static void HostSayAnchorDragging()
        {
            Active?.HostSay(LineCategory.Anchor, 25f);
        }

        internal static void HostSayRaftLost()
        {
            Active?.HostSay(LineCategory.RaftLost, 0f);
        }

        internal static void HostSayRaftSnagged()
        {
            Active?.HostSay(LineCategory.RaftSnagged, 0f);
        }

        internal static void HostSayCamp()
        {
            Active?.HostSay(LineCategory.Camp, 0f);
        }

        internal static void HostSayBoar()
        {
            Active?.HostSay(LineCategory.Boar, 18f);
        }

        internal static void HostSayPortage()
        {
            Active?.HostSay(LineCategory.Portage, 15f);
        }

        void HostSpawnShoreLife()
        {
            var session = SessionManager.Instance;
            var nodePrefab = session.LoadNetPrefab("Gather");
            if (nodePrefab != null)
            {
                (Net.GatherKind kind, Vector3 pos)[] spots =
                {
                    (Net.GatherKind.Berries, new Vector3(-10.5f, 0f, 62f)),
                    (Net.GatherKind.Logs, new Vector3(11f, 0f, 104f)),
                    (Net.GatherKind.Berries, new Vector3(-11f, 0f, 148f)),
                    (Net.GatherKind.Logs, new Vector3(10.5f, 0f, 214f)),
                    (Net.GatherKind.Berries, new Vector3(-38f, 0f, 372f)),
                    (Net.GatherKind.Logs, new Vector3(9.5f, 0f, 500f)),
                    (Net.GatherKind.Berries, new Vector3(13f, 0f, 545f)),
                    (Net.GatherKind.Logs, new Vector3(-11f, 0f, 620f)),
                    (Net.GatherKind.TouristChest, new Vector3(-14.5f, 0f, 458f))
                };
                foreach (var (kind, pos) in spots)
                {
                    Vector3 p2 = pos;
                    if (Physics.Raycast(p2 + Vector3.up * 6f, Vector3.down, out RaycastHit hit,
                            12f, ~0, QueryTriggerInteraction.Ignore))
                        p2.y = hit.point.y;
                    var go = Instantiate(nodePrefab, p2, Quaternion.identity);
                    go.GetComponent<Net.GatherNode>().Kind.Value = (int)kind;
                    go.GetComponent<NetworkObject>().Spawn(true);
                }
            }

            var boarPrefab = session.LoadNetPrefab("Boar");
            if (boarPrefab != null)
            {
                Vector3 home = new Vector3(-17f, 1.2f, 452f);
                var boar = Instantiate(boarPrefab, home, Quaternion.identity);
                boar.GetComponent<Net.BoarController>().HomePoint = home;
                boar.GetComponent<NetworkObject>().Spawn(true);
            }

            // Палатка лагеря туристов (декорация, у всех строится детерминированно на хосте-споне не нужна — ставим локально у каждого).
        }

        // ---------- Mooring & scouting (UC-06) ----------

        static readonly Vector3[] MooringSpots =
        {
            new Vector3(5.5f, 0f, 534f),   // пристань с костром
            new Vector3(-6f, 0f, 186f),    // тихая заводь перед развилкой
            new Vector3(4f, 0f, 486f)      // слияние рукавов
        };

        public static bool InMooringZone(Vector3 position)
        {
            foreach (var spot in MooringSpots)
            {
                Vector3 d = position - spot;
                d.y = 0f;
                if (d.magnitude < 11f)
                    return true;
            }
            return false;
        }

        public ScoutSystem Scouts { get; private set; }

        void BuildScoutZones()
        {
            Scouts = new ScoutSystem();
            Scouts.Zones.Add(new ScoutZone
            {
                Name = "Камни Шумного ручья",
                StartZ = 262f,
                EndZ = 340f,
                SafeLine = SampleLine(_level.NoisySpline, 15f, 95f, 13f)
            });
            Scouts.Zones.Add(new ScoutZone
            {
                Name = "Финальные пороги",
                StartZ = 575f,
                EndZ = 705f,
                SafeLine = SampleLine(_level.LowerSpline, 95f, 235f, 13f)
            });
        }

        void BuildTouristCampDecor()
        {
            // Палатка и кострище — чисто декорация, строится у всех одинаково.
            var tent = new GameObject("TouristTent");
            tent.transform.position = new Vector3(-16.5f, 1.1f, 460f);
            var canvasFilter = tent.AddComponent<MeshFilter>();
            canvasFilter.mesh = MeshFactory.BuildCone(1.3f, 1.6f, 4);
            tent.AddComponent<MeshRenderer>().sharedMaterial = GameMaterials.Get("KayakHull");

            var firepit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            firepit.name = "CampFirepit";
            Destroy(firepit.GetComponent<Collider>());
            firepit.transform.position = new Vector3(-14f, 1.05f, 461.5f);
            firepit.transform.localScale = new Vector3(0.8f, 0.08f, 0.8f);
            GameMaterials.ApplyTo(firepit, "Rock");
        }

        Vector3[] SampleLine(RiverSpline spline, float from, float to, float step)
        {
            var points = new System.Collections.Generic.List<Vector3>();
            for (float d = from; d <= to; d += step)
                points.Add(spline.PointAtDistance(d));
            return points.ToArray();
        }

        internal void RevealZoneBuoys(int zoneIndex)
        {
            if (Scouts == null || zoneIndex < 0 || zoneIndex >= Scouts.Zones.Count)
                return;
            var zone = Scouts.Zones[zoneIndex];
            zone.Scouted = true;
            foreach (var point in zone.SafeLine)
            {
                var buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                buoy.name = "Buoy";
                Destroy(buoy.GetComponent<Collider>());
                buoy.transform.position = new Vector3(point.x, 0.22f, point.z);
                buoy.transform.localScale = Vector3.one * 0.34f;
                GameMaterials.ApplyTo(buoy, "MushroomCap");
            }
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.MushroomChime, 0.6f, 0.9f);
        }

        internal void ClientCapsized()
        {
            var am = AudioManager.Instance;
            if (am != null)
            {
                am.PlaySfx(am.Collision, 0.9f, 0.7f);
                am.PlaySfx(am.PaddleStroke, 1f, 0.6f);
            }
            _cameraRig?.Shake(1f);
        }

        internal void ClientRighted()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.CampfireRest, 0.8f, 1.2f);
        }

        internal static void HostCampfireRest()
        {
            if (Active == null || Raft == null || NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.IsHost)
                return;
            var campfire = Active._level.Campfire;
            if (campfire == null || campfire.HasRested)
                return;
            if (!campfire.PlayerInRange)
                return; // the raft must actually be moored at the pier
            campfire.Rest();
            Raft.HostRepairFull();
            SavedCheckpoint = Active._level.CampfireRespawnPosition;
            SavedCheckpointRotation = Active._level.CampfireRespawnRotation;
            Raft.GetComponent<CoopFlow>().CampfireRestClientRpc();
            Active.HostSay(LineCategory.Campfire, 0f);
        }

        void HostOnFinish()
        {
            if (_finished || Stats == null || Raft == null)
                return;
            _finished = true;
            Stats.HullAtFinish = Raft.Hull.Value;
            HostSay(LineCategory.Finish, 0f);
            Raft.GetComponent<CoopFlow>().FinishClientRpc(CoopReportPayload.From(Stats));
        }

        void HostSay(LineCategory category, float cooldown)
        {
            if (Raft == null || !TimLines.ByCategory.TryGetValue(category, out var variants))
                return;
            if (_dialogue.TryEnqueue(category, variants,
                    cooldown <= 0f ? 0.01f : cooldown) &&
                _dialogue.TryDequeue(out string line))
            {
                Raft.GetComponent<CoopFlow>().TimLineClientRpc(line);
            }
        }

        // ---------- Shared update ----------

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return;

            // Late camera attach (client avatars can spawn before our Start ran).
            if (_cameraRig != null && _cameraRig.Target == null)
            {
                var own = OwnAvatar();
                if (own != null)
                    AttachCameraTo(own.transform);
                else if (Raft != null)
                    _cameraRig.Target = Raft.transform;
            }

            if (Hud != null && Raft != null)
            {
                Hud.SetHull(Raft.Hull.Value);
                int crew = 0;
                foreach (var _ in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
                    crew++;
                Hud.ShowCrewCounter(Mathf.Max(crew, 1), Raft.Food.Value, Raft.Logs.Value);

                var own = OwnAvatar();
                Hud.SetWet(own != null && own.Wet.Value);
                UpdateHints(own);
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                TogglePause();

            if (nm.IsHost && !_finished && Stats != null)
            {
                Stats.ElapsedSeconds += Time.deltaTime;
                HostChatter();
                HostCheckScouting();
            }
        }

        void HostCheckScouting()
        {
            if (Scouts == null || Raft == null)
                return;
            float raftZ = Raft.transform.position.z;
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                var zone = Scouts.TryScout(avatar.OwnerClientId,
                    avatar.transform.position.z, raftZ);
                if (zone != null)
                {
                    int index = Scouts.Zones.IndexOf(zone);
                    var stats = Stats.GetOrAdd(avatar.OwnerClientId);
                    stats.ZonesScouted++;
                    Raft.GetComponent<Net.CoopFlow>().RevealZoneClientRpc(index);
                    HostSay(LineCategory.Scout, 0f);
                }
            }
        }

        PlayerAvatar OwnAvatar()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
                return null;
            return nm.LocalClient.PlayerObject.GetComponent<PlayerAvatar>();
        }

        void UpdateHints(PlayerAvatar own)
        {
            if (own == null || Raft == null)
            {
                Hud.SetHint("");
                return;
            }
            if (own.IsSwimming)
            {
                if (PlayerAvatar.NearestCrate(own.transform.position) != null)
                    Hud.SetHint("E — выловить ящик с припасами!");
                else if (Raft.Capsized.Value)
                    Hud.SetHint(Vector3.Distance(own.transform.position, Raft.transform.position) <
                                PlayerAvatar.BoardRange + 2.6f
                        ? "Плот перевёрнут! Жмите E все вместе — перевернуть обратно"
                        : "Плывите к перевёрнутому плоту");
                else
                    Hud.SetHint(Vector3.Distance(own.transform.position, Raft.transform.position) <
                                PlayerAvatar.BoardRange + 2.2f
                        ? "E — залезть на плот"
                        : "Плывите к плоту");
                return;
            }
            if (!Raft.Capsized.Value && Mathf.Abs(Raft.TiltSync.Value) > 0.62f)
            {
                Hud.SetHint("КРЕН! Разойдитесь по плоту!");
                return;
            }
            if (!own.IsAboard && !own.IsSwimming)
            {
                var portage = Raft.GetComponent<Net.PortageController>();
                if (portage != null)
                {
                    if (portage.Phase == PortagePhase.NotStarted &&
                        portage.NearPost(own.transform.position) &&
                        portage.CanStart(Raft.transform.position))
                    {
                        Hud.SetHint("E — начать волок (обход порогов по суше)");
                        return;
                    }
                    if (portage.Phase == PortagePhase.Clearing)
                    {
                        Hud.SetHint(portage.NearTree(own.transform.position) >= 0
                            ? "E — рубить просеку!"
                            : "Рубите деревья на просеке — они впереди по тропе");
                        return;
                    }
                    if (portage.Phase == PortagePhase.Hauling)
                    {
                        Hud.SetHint(own.PullingRope
                            ? $"Тянем! {(int)(portage.ProgressSync.Value * 100)}% (еда кончится — будет тяжелее)"
                            : "Держите E рядом с плотом — тащить все вместе");
                        return;
                    }
                }
                if (Net.GatherNode.Nearest(own.transform.position) != null)
                {
                    Hud.SetHint("E — собрать припасы");
                    return;
                }
            }
            var fishing = own.GetComponent<Net.AvatarFishing>();
            string fishHint = fishing != null ? fishing.HintText() : null;
            if (fishHint != null && (RaftPost)own.PostIndex.Value == RaftPost.None)
            {
                Hud.SetHint(fishHint);
                return;
            }
            if ((RaftPost)own.PostIndex.Value != RaftPost.None)
            {
                switch ((RaftPost)own.PostIndex.Value)
                {
                    case RaftPost.Rudder:
                        Hud.SetHint("A/D — руль · E — отойти");
                        break;
                    default:
                        Hud.SetHint("W — гребок, S — табань · E — отойти");
                        break;
                }
                return;
            }
            if (Raft.NearAnchor(own.transform.position, 1.3f))
            {
                var anchorState = (AnchorState)Raft.AnchorSync.Value;
                Hud.SetHint(anchorState == AnchorState.Raised
                    ? "E — бросить якорь"
                    : anchorState == AnchorState.Dragging
                        ? "Якорь ползёт! E — поднять"
                        : "E — поднять якорь");
                return;
            }
            if (Raft.NearestPost(own.transform.position, 1.4f) != RaftPost.None)
            {
                Hud.SetHint("E — встать к посту");
                return;
            }
            if (Vector3.Distance(own.transform.position, Raft.CampfireBowlPosition) < 1.6f &&
                Raft.Hull.Value < RaftController.MaxHull)
            {
                Hud.SetHint("Держите E — ремонт плота");
                return;
            }
            var campfire = _level.Campfire;
            if (campfire != null && campfire.PlayerInRange && !campfire.HasRested &&
                NearShoreCampfire(own.transform.position))
            {
                Hud.SetHint("E — отдохнуть у костра (чекпоинт)");
                return;
            }
            Hud.SetHint("");
        }

        void HostChatter()
        {
            if (Raft == null)
                return;
            float z = Raft.transform.position.z;
            if (!_forkLineShown && z > 190f && z < 230f)
            {
                _forkLineShown = true;
                HostSay(LineCategory.Fork, 0f);
            }

            if (_level.NoisyZone != null && _level.NoisyZone.PlayerInside)
            {
                _nervousLineTimer -= Time.deltaTime;
                if (_nervousLineTimer <= 0f)
                {
                    _nervousLineTimer = 12f;
                    HostSay(LineCategory.NoisyNerves, 10f);
                }
            }

            _idleLineTimer -= Time.deltaTime;
            if (_idleLineTimer <= 0f)
            {
                _idleLineTimer = 50f;
                HostSay(LineCategory.Idle, 45f);
            }
        }

        // ---------- Pause / navigation ----------

        void TogglePause()
        {
            _paused = !_paused;
            if (_paused)
                _pauseMenu.Open();
            else
                _pauseMenu.Close();
            // Network time keeps flowing; the menu is local-only.
        }

        void HostRestart()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsHost)
            {
                StartFromCheckpoint = false;
                nm.SceneManager.LoadScene("River", LoadSceneMode.Single);
            }
            else
            {
                TogglePause();
            }
        }

        public void LeaveToMenu()
        {
            CoopRequested = false;
            StartFromCheckpoint = false;
            if (SessionManager.Instance != null)
                SessionManager.Instance.Shutdown();
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        // ---------- Called by CoopFlow client RPCs ----------

        internal void ClientRaftHit(float impulse)
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.Collision, Mathf.Clamp01(impulse / 10f) * 0.6f + 0.35f);
            _cameraRig?.Shake(0.6f);
        }

        internal void ClientRaftBump()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.SoftBump, 0.35f);
        }

        internal void ClientSplash()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.PaddleStroke, 0.9f, 0.7f);
        }

        internal void ClientCampfireRest()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.CampfireRest, 0.9f);
        }

        internal void ClientTimLine(string line)
        {
            Hud?.EnqueueSubtitle(line);
        }

        internal void ClientFinish(CoopReportPayload payload)
        {
            _finished = true;
            CoopSmokePilot.ReportShown = true;
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.Finish, 0.9f);
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            CoopReportScreen.Show(payload, isHost, HostRestart, LeaveToMenu);
        }
    }
}
