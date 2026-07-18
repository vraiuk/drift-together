using DriftTogether.Core;
using DriftTogether.Player;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// Co-op crew member. The owning client moves it (walking on the raft in
    /// raft-local space, swimming in the river); the host owns statuses:
    /// current post, wet state, overboard bookkeeping and auto-return.
    /// </summary>
    public sealed class PlayerAvatar : NetworkBehaviour
    {
        public const float WalkSpeed = 3.2f;
        public const float SwimSpeed = 1.6f;
        public const float PushRange = 1.7f;
        public const float BoardRange = 2.6f;
        public const float AutoReturnDistance = 40f;

        public NetworkVariable<int> PostIndex = new NetworkVariable<int>((int)RaftPost.None);
        public NetworkVariable<bool> Wet = new NetworkVariable<bool>(false);
        public NetworkVariable<int> ColorIndex = new NetworkVariable<int>(0);

        public bool IsSwimming { get; private set; }
        public bool IsAboard { get; private set; }

        Transform _visual;
        Vector3 _pushVelocity;
        float _pushCooldown;
        Vector3 _lastRaftPos;
        float _lastRaftYaw;
        bool _hadRaftFrame;

        // Host-side status.
        public readonly WetStatus WetTimer = new WetStatus();
        bool _hostWasAboard = true;

        RaftController Raft => CoopBootstrap.Raft;

        public override void OnNetworkSpawn()
        {
            _visual = CharacterFactory.BuildVisual(transform,
                CharacterFactory.TeamColors[Mathf.Clamp(ColorIndex.Value, 0, CharacterFactory.TeamColors.Length - 1)]);
            ColorIndex.OnValueChanged += (_, v) =>
            {
                if (_visual != null)
                    Destroy(_visual.gameObject);
                _visual = CharacterFactory.BuildVisual(transform,
                    CharacterFactory.TeamColors[Mathf.Clamp(v, 0, CharacterFactory.TeamColors.Length - 1)]);
            };

            if (IsOwner)
                CoopBootstrap.AttachCameraTo(transform);
        }

        void Update()
        {
            if (IsOwner)
                OwnerUpdate();
            if (IsServer)
                HostUpdate();
            UpdateVisual();
        }

        // ---------- Owner movement ----------

        void OwnerUpdate()
        {
            float dt = Time.deltaTime;
            _pushCooldown -= dt;
            _pushVelocity = Vector3.Lerp(_pushVelocity, Vector3.zero, dt * 3f);

            if (PostIndex.Value != (int)RaftPost.None && Raft != null)
            {
                OwnerAtPost();
                return;
            }
            _hadRaftFrame = false;

            var kb = Keyboard.current;
            Vector2 move = ReadMoveInput();

            float water = RiverFlow.WaterHeightAt(transform.position, Time.time);
            bool grounded = TryGetGround(out float groundY, out bool onRaft);
            IsSwimming = !grounded && transform.position.y < water + 0.25f;
            IsAboard = grounded && onRaft;

            Vector3 pos = transform.position;
            Vector3 dir = MoveDirection(move);

            if (IsSwimming)
            {
                pos += dir * (SwimSpeed * dt) + _pushVelocity * dt;
                if (CoopBootstrap.Flow != null)
                    pos += CoopBootstrap.Flow.CurrentAt(pos) * (dt * 0.5f);
                pos.y = Mathf.Lerp(pos.y, water - 0.45f, dt * 5f);

                if (Raft != null && kb != null && kb.eKey.wasPressedThisFrame &&
                    Vector3.Distance(pos, Raft.transform.position) < BoardRange + 2.2f)
                {
                    pos = Raft.DeckPoint(Vector3.zero);
                    IsSwimming = false;
                }
            }
            else
            {
                if (grounded)
                {
                    if (onRaft)
                    {
                        // Carry the avatar with the raft between frames.
                        if (_hadRaftFrame)
                        {
                            float yawDelta = Raft.transform.eulerAngles.y - _lastRaftYaw;
                            Vector3 local = pos - _lastRaftPos;
                            local = Quaternion.Euler(0f, yawDelta, 0f) * local;
                            pos = Raft.transform.position + local;
                        }
                        _lastRaftPos = Raft.transform.position;
                        _lastRaftYaw = Raft.transform.eulerAngles.y;
                        _hadRaftFrame = true;
                    }
                    else
                    {
                        _hadRaftFrame = false;
                    }

                    pos += dir * (WalkSpeed * WetSpeedMultiplier() * dt) + _pushVelocity * dt;
                    TryGetGroundAt(pos, out groundY, out _);
                    pos.y = Mathf.Lerp(pos.y, groundY, dt * 12f);
                }
                else
                {
                    pos += dir * (WalkSpeed * 0.4f * dt) + _pushVelocity * dt;
                    pos.y -= 4.5f * dt; // simple gravity into the water
                    _hadRaftFrame = false;
                }
            }

            transform.position = pos;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), dt * 10f);

            OwnerInteractions(kb);
        }

        float WetSpeedMultiplier() => Wet.Value ? WetStatus.WetSpeedMultiplier : 1f;

        void OwnerAtPost()
        {
            var post = (RaftPost)PostIndex.Value;
            Transform marker = Raft.PostMarker(post);
            if (marker != null)
            {
                transform.position = marker.position;
                transform.rotation = marker.rotation;
            }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            float steer = 0f;
            bool forward = false, reverse = false, leave = false;

            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                forward = kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                reverse = kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
                leave = kb.eKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                steer += Mathf.Abs(pad.leftStick.ReadValue().x) > 0.2f ? pad.leftStick.ReadValue().x : 0f;
                if (pad.leftStick.ReadValue().y > 0.6f && _oarRepeat <= 0f) { forward = true; _oarRepeat = 0.45f; }
                if (pad.leftStick.ReadValue().y < -0.6f && _oarRepeat <= 0f) { reverse = true; _oarRepeat = 0.45f; }
                leave |= pad.buttonSouth.wasPressedThisFrame;
            }
            _oarRepeat -= Time.deltaTime;

            switch (post)
            {
                case RaftPost.Rudder:
                    Raft.SetRudderServerRpc(Mathf.Clamp(steer, -1f, 1f));
                    break;
                case RaftPost.OarLeft:
                    if (forward) Raft.OarStrokeServerRpc(leftSide: true, reverse: false);
                    if (reverse) Raft.OarStrokeServerRpc(leftSide: true, reverse: true);
                    break;
                case RaftPost.OarRight:
                    if (forward) Raft.OarStrokeServerRpc(leftSide: false, reverse: false);
                    if (reverse) Raft.OarStrokeServerRpc(leftSide: false, reverse: true);
                    break;
            }

            if (leave)
                ReleasePostServerRpc();
            IsAboard = true;
            IsSwimming = false;
        }

        float _oarRepeat;

        Vector2 ReadMoveInput()
        {
            Vector2 move = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }
            var pad = Gamepad.current;
            if (pad != null && pad.leftStick.ReadValue().magnitude > 0.2f)
                move += pad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(move, 1f);
        }

        Vector3 MoveDirection(Vector2 move)
        {
            var cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            return (fwd * move.y + right * move.x).normalized * move.magnitude;
        }

        bool TryGetGround(out float groundY, out bool onRaft) =>
            TryGetGroundAt(transform.position, out groundY, out onRaft);

        bool TryGetGroundAt(Vector3 pos, out float groundY, out bool onRaft)
        {
            groundY = 0f;
            onRaft = false;
            if (Physics.Raycast(pos + Vector3.up * 1.4f, Vector3.down, out RaycastHit hit,
                    2.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerAvatar>() != null)
                    return false;
                groundY = hit.point.y;
                onRaft = hit.collider.GetComponentInParent<RaftController>() != null;
                return true;
            }
            return false;
        }

        void OwnerInteractions(Keyboard kb)
        {
            if (kb == null)
                return;

            // F — толкнуть ближайшего.
            if (kb.fKey.wasPressedThisFrame && _pushCooldown <= 0f && IsAboard)
            {
                var target = FindNearestOtherAvatar();
                if (target != null)
                {
                    _pushCooldown = 3f;
                    PushServerRpc(target.OwnerClientId,
                        (target.transform.position - transform.position).normalized);
                }
            }

            // E — занять пост / отдохнуть у берегового костра / ремонт.
            if (kb.eKey.wasPressedThisFrame && IsAboard && Raft != null)
            {
                RaftPost nearest = Raft.NearestPost(transform.position, 1.4f);
                if (nearest != RaftPost.None)
                {
                    RequestPostServerRpc(nearest);
                    return;
                }
                if (CoopBootstrap.NearShoreCampfire(transform.position))
                {
                    CampfireRestServerRpc();
                    return;
                }
            }

            // Удержание E у костровой чаши — ремонт.
            if (kb.eKey.isPressed && IsAboard && Raft != null &&
                Vector3.Distance(transform.position, Raft.CampfireBowlPosition) < 1.6f)
            {
                Raft.RepairHoldServerRpc();
            }
        }

        PlayerAvatar FindNearestOtherAvatar()
        {
            PlayerAvatar best = null;
            float bestDist = PushRange;
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                if (avatar == this)
                    continue;
                float d = Vector3.Distance(avatar.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = avatar;
                }
            }
            return best;
        }

        void UpdateVisual()
        {
            if (_visual == null)
                return;
            // Swimming: tilt forward and sink the body a bit.
            var targetRot = IsSwimming ? Quaternion.Euler(72f, 0f, 0f) : Quaternion.identity;
            _visual.localRotation = Quaternion.Slerp(_visual.localRotation, targetRot, Time.deltaTime * 6f);
            _visual.localPosition = new Vector3(0f,
                Mathf.Sin(Time.time * 2.2f + OwnerClientId) * 0.03f - (IsSwimming ? 0.35f : 0f), 0f);
        }

        // ---------- Host status ----------

        void HostUpdate()
        {
            if (Raft == null)
                return;
            float dt = Time.deltaTime;
            var stats = CoopBootstrap.Stats?.GetOrAdd(OwnerClientId);

            float water = RiverFlow.WaterHeightAt(transform.position, Time.time);
            bool inWater = transform.position.y < water - 0.15f;

            if (inWater && _hostWasAboard)
            {
                _hostWasAboard = false;
                WetTimer.Soak();
                Wet.Value = true;
                Raft.Posts.ReleaseAll(OwnerClientId);
                if (Raft.Posts.PostOf(OwnerClientId) == RaftPost.None && PostIndex.Value != (int)RaftPost.None)
                    PostIndex.Value = (int)RaftPost.None;
                if (stats != null)
                    stats.OverboardCount++;
                CoopBootstrap.OnOverboard(OwnerClientId);
            }
            else if (!inWater)
            {
                _hostWasAboard = true;
            }

            bool nearFire = Vector3.Distance(transform.position, Raft.CampfireBowlPosition) < 3f ||
                            CoopBootstrap.NearShoreCampfire(transform.position);
            WetTimer.Tick(dt, nearFire);
            if (stats != null && WetTimer.IsWet)
                stats.WetSeconds += dt;
            if (Wet.Value != WetTimer.IsWet)
                Wet.Value = WetTimer.IsWet;

            if (PostIndex.Value == (int)RaftPost.Rudder && stats != null)
                stats.RudderSeconds += dt;

            // Река возвращает: unreachable player teleports back to the raft.
            if (Vector3.Distance(transform.position, Raft.transform.position) > AutoReturnDistance)
            {
                Vector3 back = Raft.transform.position - Raft.transform.forward * 3f;
                back.y = RiverFlow.WaterHeightAt(back, Time.time) - 0.4f;
                TeleportClientRpc(back, RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            }
        }

        // ---------- RPCs ----------

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void RequestPostServerRpc(RaftPost post, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId || Raft == null)
                return;
            if (Raft.Posts.TryOccupy(post, sender))
                PostIndex.Value = (int)post;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void ReleasePostServerRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender != OwnerClientId || Raft == null)
                return;
            Raft.Posts.ReleaseAll(sender);
            PostIndex.Value = (int)RaftPost.None;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void PushServerRpc(ulong targetClientId, Vector3 direction, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var stats = CoopBootstrap.Stats?.GetOrAdd(sender);
            if (stats != null)
                stats.PushesGiven++;

            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                if (avatar.OwnerClientId == targetClientId)
                {
                    avatar.PushedClientRpc(direction,
                        RpcTarget.Single(targetClientId, RpcTargetUse.Temp));
                    break;
                }
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        void CampfireRestServerRpc(RpcParams rpcParams = default)
        {
            CoopBootstrap.HostCampfireRest();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void PushedClientRpc(Vector3 direction, RpcParams rpcParams = default)
        {
            direction.y = 0f;
            _pushVelocity += direction.normalized * 4.2f;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void TeleportClientRpc(Vector3 position, RpcParams rpcParams = default)
        {
            transform.position = position;
            _hadRaftFrame = false;
        }
    }
}
