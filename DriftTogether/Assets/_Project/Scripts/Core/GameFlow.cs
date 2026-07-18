using System.Collections;
using DriftTogether.Player;
using DriftTogether.UI;
using DriftTogether.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftTogether.Core
{
    /// <summary>
    /// Orchestrates the River scene: builds the level and kayak, wires all
    /// systems together and drives the play / pause / finish state machine.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public enum State
        {
            Playing,
            Paused,
            Respawning,
            Finished
        }

        public State CurrentState { get; private set; } = State.Playing;

        public HullIntegrity Hull { get; private set; }
        public CheckpointSystem Checkpoints { get; private set; }
        public MushroomTracker MushroomsCollected { get; private set; }
        public RunStats Stats { get; private set; }
        public DialogueQueue Dialogue { get; private set; }
        public KayakController Kayak { get; private set; }
        public LevelBuilder Level { get; private set; }

        HUD _hud;
        PauseMenu _pauseMenu;
        CameraRig _cameraRig;
        PassengerTim _tim;
        AudioSource _campfireAudio;

        float _idleLineTimer = 25f;
        float _nervousLineTimer = 8f;
        bool _forkLineShown;
        bool _finishedOnce;

        void Start()
        {
            // Co-op session in progress: hand the scene over to the raft flow.
            if (Coop.CoopBootstrap.CoopRequested)
            {
                enabled = false; // solo Update must not run without a kayak
                Coop.CoopBootstrap.Begin(gameObject);
                return;
            }

            Time.timeScale = 1f;
            AudioManager.Ensure();
            AudioManager.Instance.SetWaterPresence(1f);
            GameSettings.ApplyToListener();

            Hull = new HullIntegrity(new GameClock());
            Checkpoints = new CheckpointSystem();
            MushroomsCollected = new MushroomTracker();
            Stats = new RunStats();
            Dialogue = new DialogueQueue(new GameClock());

            Level = gameObject.AddComponent<LevelBuilder>();
            Level.Build();

            Kayak = KayakFactory.Create(Level.KayakStartPosition, Level.KayakStartRotation);
            Kayak.Initialize(Level.Flow, Hull, Checkpoints, Stats,
                Kayak.transform.Find("Visual"));
            _tim = Kayak.GetComponent<PassengerTim>();
            Checkpoints.Set(Level.KayakStartPosition, Level.KayakStartRotation);

            SetupCamera();
            SetupUI();
            SetupFoam();
            SetupCampfireAudio();
            WireEvents();

            Say(LineCategory.Intro, cooldown: 0f);

            if (SmokeAutopilot.CommandLineRequested() || SmokeAutopilot.RequestedByTest)
            {
                var autopilot = gameObject.AddComponent<SmokeAutopilot>();
                autopilot.WriteReportAndQuit = SmokeAutopilot.CommandLineRequested();
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

            _cameraRig = cam.gameObject.GetComponent<CameraRig>();
            if (_cameraRig == null)
                _cameraRig = cam.gameObject.AddComponent<CameraRig>();
            _cameraRig.Target = Kayak.transform;
            _cameraRig.SnapBehindTarget();
        }

        void SetupUI()
        {
            UIBuilder.EnsureEventSystem();
            _hud = HUD.Create();
            _hud.SetHull(Hull.Current);
            _hud.SetMushrooms(0);
            IntroHint.Create();

            _pauseMenu = PauseMenu.Create();
            _pauseMenu.OnContinue = TogglePause;
            _pauseMenu.OnRestart = () => { Time.timeScale = 1f; SceneManager.LoadScene("River"); };
            _pauseMenu.OnMainMenu = () => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); };
        }

        void SetupFoam()
        {
            var foam = gameObject.AddComponent<FoamDrifters>();
            foam.Flow = Level.Flow;
            foam.Player = Kayak.transform;
            foam.FoamMaterial = GameMaterials.Get("Foam");
        }

        void SetupCampfireAudio()
        {
            var go = new GameObject("CampfireAudio");
            go.transform.position = Level.CampfireRespawnPosition + new Vector3(7f, 1f, 0f);
            _campfireAudio = go.AddComponent<AudioSource>();
            _campfireAudio.clip = AudioManager.Instance.CampfireLoop;
            _campfireAudio.loop = true;
            _campfireAudio.spatialBlend = 1f;
            _campfireAudio.minDistance = 4f;
            _campfireAudio.maxDistance = 30f;
            _campfireAudio.Play();
        }

        void WireEvents()
        {
            Hull.Changed += current => _hud.SetHull(current);
            Hull.Broken += () => StartCoroutine(RespawnSequence());

            Kayak.CollisionOccurred += (impulse, hard) =>
            {
                if (hard)
                {
                    _cameraRig.Shake(0.7f);
                    _tim?.ReactToHit();
                    Say(LineCategory.Collision, cooldown: 10f);
                }
                else if (impulse > 1.5f)
                {
                    _cameraRig.Shake(0.2f);
                }
            };

            Kayak.Respawned += () =>
            {
                _cameraRig.SnapBehindTarget();
                Say(LineCategory.Respawn, cooldown: 12f);
            };

            foreach (var mushroom in Level.Mushrooms)
            {
                mushroom.PickedUp += m =>
                {
                    if (!MushroomsCollected.Collect(m.Id))
                        return;
                    Stats.Mushrooms = MushroomsCollected.Count;
                    _hud.SetMushrooms(MushroomsCollected.Count);
                    var am = AudioManager.Instance;
                    am.PlaySfx(am.MushroomChime, 0.8f);
                    Say(LineCategory.Mushroom, cooldown: 14f);
                    Destroy(m.gameObject);
                };
            }

            foreach (var gate in Level.RouteGates)
            {
                gate.Entered += route =>
                {
                    Stats.ChooseRoute(route);
                    AudioManager.Instance.SetWaterPresence(route == RiverRoute.NoisyStream ? 1.4f : 0.8f);
                };
            }

            foreach (var zone in Level.CheckpointZones)
            {
                zone.Reached += z => Checkpoints.Set(z.RespawnPosition, z.RespawnRotation);
            }

            Level.Finish.Finished += OnFinish;
        }

        void Update()
        {
            if (CurrentState == State.Finished)
                return;

            if (Kayak.Input.PausePressed)
                TogglePause();

            if (CurrentState == State.Paused)
                return;

            Stats.ElapsedSeconds += Time.deltaTime;
            UpdateCampfire();
            UpdateChatter();

            if (Level != null && Level.Flow != null && AudioManager.Instance != null)
            {
                float current = Level.Flow.CurrentAt(Kayak.transform.position).magnitude;
                AudioManager.Instance.SetRapidsMix((current - 1.3f) / 1.6f);
            }
        }

        void UpdateCampfire()
        {
            var campfire = Level.Campfire;
            if (campfire == null)
                return;

            if (campfire.PlayerInRange && !campfire.HasRested)
            {
                _hud.SetHint("E — отдохнуть у костра");
                if (Kayak.Input.InteractPressed)
                {
                    campfire.Rest();
                    Hull.RestoreFull();
                    Checkpoints.Set(Level.CampfireRespawnPosition, Level.CampfireRespawnRotation);
                    var am = AudioManager.Instance;
                    am.PlaySfx(am.CampfireRest, 0.9f);
                    Say(LineCategory.Campfire, cooldown: 0f);
                    _hud.SetHint("");
                }
            }
            else if (campfire.PlayerInRange && campfire.HasRested)
            {
                _hud.SetHint("Байдарка как новая. Можно плыть дальше");
            }
            else
            {
                _hud.SetHint("");
            }
        }

        void UpdateChatter()
        {
            // One-time fork warning while approaching the island.
            float z = Kayak.transform.position.z;
            if (!_forkLineShown && z > 190f && z < 230f)
            {
                _forkLineShown = true;
                Say(LineCategory.Fork, cooldown: 0f);
            }

            // Nervous chatter on the noisy stream.
            if (Level.NoisyZone != null && Level.NoisyZone.PlayerInside)
            {
                _nervousLineTimer -= Time.deltaTime;
                if (_nervousLineTimer <= 0f)
                {
                    _nervousLineTimer = 11f;
                    Say(LineCategory.NoisyNerves, cooldown: 9f);
                }
            }

            // Occasional idle musings elsewhere.
            _idleLineTimer -= Time.deltaTime;
            if (_idleLineTimer <= 0f)
            {
                _idleLineTimer = 45f;
                Say(LineCategory.Idle, cooldown: 40f);
            }
        }

        void Say(LineCategory category, float cooldown = DialogueQueue.DefaultCategoryCooldown)
        {
            if (!TimLines.ByCategory.TryGetValue(category, out var variants))
                return;
            if (Dialogue.TryEnqueue(category, variants, cooldown) &&
                Dialogue.TryDequeue(out string line))
            {
                _hud.EnqueueSubtitle(line);
            }
        }

        void TogglePause()
        {
            if (CurrentState == State.Finished)
                return;

            if (CurrentState == State.Paused)
            {
                CurrentState = State.Playing;
                Time.timeScale = 1f;
                _pauseMenu.Close();
            }
            else
            {
                CurrentState = State.Paused;
                Time.timeScale = 0f;
                _pauseMenu.Open();
            }
        }

        IEnumerator RespawnSequence()
        {
            if (CurrentState != State.Playing)
                yield break;
            CurrentState = State.Respawning;
            Kayak.ControlEnabled = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.8f)
            {
                _hud.FadeAlpha = t;
                yield return null;
            }
            _hud.FadeAlpha = 1f;

            Hull.RestoreFull();
            Kayak.RespawnAtCheckpoint(manual: false);
            yield return new WaitForSeconds(0.25f);

            for (float t = 1f; t > 0f; t -= Time.deltaTime * 2.2f)
            {
                _hud.FadeAlpha = t;
                yield return null;
            }
            _hud.FadeAlpha = 0f;

            Kayak.ControlEnabled = true;
            CurrentState = State.Playing;
        }

        void OnFinish()
        {
            if (_finishedOnce)
                return;
            _finishedOnce = true;
            CurrentState = State.Finished;
            Kayak.ControlEnabled = false;

            var am = AudioManager.Instance;
            am.PlaySfx(am.Finish, 0.9f);
            Say(LineCategory.Finish, cooldown: 0f);

            StartCoroutine(ShowResultsSoon());
        }

        IEnumerator ShowResultsSoon()
        {
            yield return new WaitForSeconds(2.2f);
            var results = ResultsScreen.Create(Stats);
            results.OnRestart = () => { Time.timeScale = 1f; SceneManager.LoadScene("River"); };
            results.OnMainMenu = () => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); };
        }
    }
}
