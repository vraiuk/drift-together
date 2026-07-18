using DriftTogether.Core;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// Fishing (UC-04). The owner reads input and asks; the host owns the rod
    /// inventory, the FishingSession state machine and the food store.
    /// </summary>
    public sealed class AvatarFishing : NetworkBehaviour
    {
        public NetworkVariable<bool> HasRod = new NetworkVariable<bool>(false);
        public NetworkVariable<int> Phase = new NetworkVariable<int>((int)FishingState.Idle);

        // Host-side.
        FishingSession _session;
        int _rodStandIndex = -1;
        float _rockTimer;

        // Owner/visual side.
        Transform _bobber;
        Transform _rodVisual;
        PlayerAvatar _avatar;

        public FishingState CurrentPhase => (FishingState)Phase.Value;
        public bool Busy => CurrentPhase != FishingState.Idle || HasRod.Value;

        void Awake()
        {
            _avatar = GetComponent<PlayerAvatar>();
        }

        // ---------- Owner interaction (called by PlayerAvatar before posts) ----------

        /// <summary>Handles E. Returns true if fishing consumed the press.</summary>
        public bool TryHandleInteract()
        {
            var raft = CoopBootstrap.Raft;
            if (raft == null)
                return false;

            // Help a struggling friend nearby (works with or without a rod).
            foreach (var other in FindObjectsByType<AvatarFishing>(FindObjectsSortMode.None))
            {
                if (other != this && other.CurrentPhase == FishingState.Struggle &&
                    Vector3.Distance(other.transform.position, transform.position) < 2.2f)
                {
                    AssistServerRpc(other.OwnerClientId);
                    return true;
                }
            }

            int stand = raft.NearestRodStand(transform.position, 1.4f);
            if (!HasRod.Value)
            {
                if (stand >= 0)
                {
                    TakeRodServerRpc(stand);
                    return true;
                }
                return false;
            }

            // Holding the rod.
            if (stand >= 0 && CurrentPhase == FishingState.Idle)
            {
                ReturnRodServerRpc();
                return true;
            }
            switch (CurrentPhase)
            {
                case FishingState.Idle:
                    CastServerRpc(transform.position + transform.forward * 3.5f);
                    return true;
                case FishingState.Bite:
                case FishingState.Struggle:
                    HookServerRpc();
                    return true;
                case FishingState.Waiting:
                    CancelCastServerRpc(); // reel in early
                    return true;
            }
            return false;
        }

        public string HintText()
        {
            var raft = CoopBootstrap.Raft;
            if (raft == null)
                return null;
            switch (CurrentPhase)
            {
                case FishingState.Waiting: return "Ждём поклёвку… (E — смотать)";
                case FishingState.Bite: return "КЛЮЁТ! E — подсечь!";
                case FishingState.Struggle: return "Тянет! Жмите E! (друг рядом может помочь)";
            }
            if (HasRod.Value)
                return raft.NearestRodStand(transform.position, 1.4f) >= 0
                    ? "E — убрать удочку"
                    : "E — забросить удочку";
            if (raft.NearestRodStand(transform.position, 1.4f) >= 0)
                return "E — взять удочку";
            foreach (var other in FindObjectsByType<AvatarFishing>(FindObjectsSortMode.None))
                if (other != this && other.CurrentPhase == FishingState.Struggle &&
                    Vector3.Distance(other.transform.position, transform.position) < 2.2f)
                    return "E — помочь с рыбой!";
            return null;
        }

        // ---------- Host logic ----------

        void Update()
        {
            if (!IsServer || _session == null)
                return;

            _session.Tick();
            var newPhase = (int)_session.State;
            if (Phase.Value != newPhase)
            {
                Phase.Value = newPhase;
                OnHostPhaseChanged();
            }

            if (_session.State == FishingState.Struggle)
            {
                _rockTimer -= Time.deltaTime;
                if (_rockTimer <= 0f)
                {
                    _rockTimer = 0.7f;
                    var raftBody = CoopBootstrap.Raft.GetComponent<Rigidbody>();
                    raftBody.AddTorque(Vector3.up * Random.Range(-1.2f, 1.2f), ForceMode.Impulse);
                    CoopBootstrap.Raft.GetComponent<CoopFlow>().RaftBumpClientRpc();
                }
            }
        }

        void OnHostPhaseChanged()
        {
            var flow = CoopBootstrap.Raft.GetComponent<CoopFlow>();
            switch (_session.State)
            {
                case FishingState.Bite:
                    BiteClientRpc();
                    break;
                case FishingState.Landed:
                    CoopBootstrap.Raft.Food.Value += _session.FoodResult;
                    var stats = CoopBootstrap.Stats?.GetOrAdd(OwnerClientId);
                    if (stats != null)
                        stats.FishCaught += 1;
                    CaughtClientRpc(_session.IsBigFish);
                    CoopBootstrap.HostSayFishing();
                    _session = null;
                    Phase.Value = (int)FishingState.Idle;
                    break;
                case FishingState.Escaped:
                    EscapedClientRpc();
                    _session = null;
                    Phase.Value = (int)FishingState.Idle;
                    break;
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void TakeRodServerRpc(int stand, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId)
                return;
            var raft = CoopBootstrap.Raft;
            if (raft == null || HasRod.Value || !raft.TryTakeRod(stand))
                return;
            _rodStandIndex = stand;
            HasRod.Value = true;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ReturnRodServerRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || !HasRod.Value || _session != null)
                return;
            CoopBootstrap.Raft?.ReturnRod(_rodStandIndex);
            _rodStandIndex = -1;
            HasRod.Value = false;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void CastServerRpc(Vector3 bobberPos, RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || !HasRod.Value || _session != null)
                return;
            bobberPos.y = RiverFlow.WaterHeightAt(bobberPos, Time.time);
            bool calm = CoopBootstrap.Flow == null ||
                        CoopBootstrap.Flow.CurrentAt(bobberPos).magnitude < 1.0f;
            _session = new FishingSession(new GameClock(),
                unchecked((int)OwnerClientId * 7919 + Time.frameCount));
            _session.Cast(calm);
            Phase.Value = (int)FishingState.Waiting;
            BobberClientRpc(bobberPos, true);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void CancelCastServerRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || _session == null)
                return;
            if (_session.State == FishingState.Waiting)
            {
                _session = null;
                Phase.Value = (int)FishingState.Idle;
                BobberClientRpc(Vector3.zero, false);
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void HookServerRpc(RpcParams p = default)
        {
            if (p.Receive.SenderClientId != OwnerClientId || _session == null)
                return;
            _session.Hook();
            var newPhase = (int)_session.State;
            if (Phase.Value != newPhase)
            {
                Phase.Value = newPhase;
                OnHostPhaseChanged();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void AssistServerRpc(ulong strugglingOwner, RpcParams p = default)
        {
            foreach (var other in FindObjectsByType<AvatarFishing>(FindObjectsSortMode.None))
            {
                if (other.OwnerClientId != strugglingOwner || other._session == null)
                    continue;
                other._session.FriendAssist();
                int np = (int)other._session.State;
                if (other.Phase.Value != np)
                {
                    other.Phase.Value = np;
                    other.OnHostPhaseChanged();
                }
                var stats = CoopBootstrap.Stats?.GetOrAdd(p.Receive.SenderClientId);
                if (stats != null)
                    stats.FishCaught += 0; // помощь не крадёт улов, но мем засчитан репликой
                break;
            }
        }

        // ---------- Presentation ----------

        [Rpc(SendTo.Everyone)]
        void BobberClientRpc(Vector3 pos, bool show)
        {
            if (_bobber == null && show)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Bobber";
                Destroy(b.GetComponent<Collider>());
                b.transform.localScale = Vector3.one * 0.18f;
                GameMaterials.ApplyTo(b, "FireGlow");
                _bobber = b.transform;
            }
            if (_bobber != null)
            {
                _bobber.gameObject.SetActive(show);
                if (show)
                    _bobber.position = pos;
            }
            var amCast = AudioManager.Instance;
            if (show && amCast != null)
                amCast.PlaySfx(amCast.CastPlop, 0.6f);
            UpdateRodVisual(show || HasRod.Value);
        }

        [Rpc(SendTo.Everyone)]
        void BiteClientRpc()
        {
            if (_bobber != null)
                _bobber.position += Vector3.down * 0.12f;
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.BiteBlup, 0.85f);
        }

        [Rpc(SendTo.Everyone)]
        void CaughtClientRpc(bool big)
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.MushroomChime, big ? 1f : 0.7f, big ? 0.8f : 1.1f);
            if (_bobber != null)
                _bobber.gameObject.SetActive(false);
            UpdateRodVisual(HasRod.Value);
        }

        [Rpc(SendTo.Everyone)]
        void EscapedClientRpc()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.PaddleStroke, 0.8f, 0.7f);
            if (_bobber != null)
                _bobber.gameObject.SetActive(false);
            UpdateRodVisual(HasRod.Value);
        }

        void UpdateRodVisual(bool show)
        {
            if (_rodVisual == null && show)
            {
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rod.name = "RodInHands";
                Destroy(rod.GetComponent<Collider>());
                rod.transform.SetParent(transform, false);
                rod.transform.localPosition = new Vector3(0.22f, 0.85f, 0.4f);
                rod.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
                rod.transform.localScale = new Vector3(0.035f, 0.9f, 0.035f);
                GameMaterials.ApplyTo(rod, "Wood");
                _rodVisual = rod.transform;
            }
            if (_rodVisual != null)
                _rodVisual.gameObject.SetActive(show);
        }

        public override void OnNetworkSpawn()
        {
            HasRod.OnValueChanged += (_, has) => UpdateRodVisual(has);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _rodStandIndex >= 0)
                CoopBootstrap.Raft?.ReturnRod(_rodStandIndex);
            if (_bobber != null)
                Destroy(_bobber.gameObject);
        }
    }
}
