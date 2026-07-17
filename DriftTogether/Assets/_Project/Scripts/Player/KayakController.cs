using System;
using DriftTogether.Core;
using DriftTogether.World;
using UnityEngine;

namespace DriftTogether.Player
{
    /// <summary>
    /// Arcade kayak movement: predictable forces, river current, soft bobbing,
    /// collision damage with an invulnerability window, manual and automatic
    /// respawn at the last checkpoint. Physically simple on purpose.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KayakController : MonoBehaviour
    {
        const float PaddlePower = 13f;
        const float ReversePower = 6f;
        const float TurnPower = 2.6f;
        const float MaxSpeed = 7.5f;
        const float HardHitImpulse = 3.5f;
        const float StuckSpeed = 0.25f;
        const float StuckTimeout = 6f;

        public Rigidbody Body { get; private set; }
        public BoatInput Input { get; private set; }

        public RiverFlow Flow;
        public HullIntegrity Hull;
        public CheckpointSystem Checkpoints;
        public RunStats Stats;

        /// <summary>impulse magnitude, was a hard hit</summary>
        public event Action<float, bool> CollisionOccurred;
        public event Action Respawned;

        public bool ControlEnabled = true;
        public float DistanceTravelled { get; private set; }

        Transform _visual;
        float _stuckTimer;
        float _paddleSfxTimer;
        float _bobPhase;

        public void Initialize(RiverFlow flow, HullIntegrity hull, CheckpointSystem checkpoints,
            RunStats stats, Transform visual)
        {
            Flow = flow;
            Hull = hull;
            Checkpoints = checkpoints;
            Stats = stats;
            _visual = visual;
        }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Input = GetComponent<BoatInput>();
            if (Input == null)
                Input = gameObject.AddComponent<BoatInput>();

            Body.useGravity = false;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Body.linearDamping = 0.9f;
            Body.angularDamping = 2.5f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void Update()
        {
            if (!ControlEnabled)
                return;

            if (Input.ResetPressed)
                RespawnAtCheckpoint(manual: true);

            // Paddle splash sound while actively thrusting.
            if (Mathf.Abs(Input.Thrust) > 0.25f)
            {
                _paddleSfxTimer -= Time.deltaTime;
                if (_paddleSfxTimer <= 0f)
                {
                    _paddleSfxTimer = 0.85f;
                    var am = AudioManager.Instance;
                    if (am != null)
                        am.PlaySfx(am.PaddleStroke, 0.5f, UnityEngine.Random.Range(0.9f, 1.1f));
                }
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (ControlEnabled)
            {
                float thrust = Input.Thrust;
                float power = thrust >= 0f ? PaddlePower : ReversePower;
                Body.AddForce(transform.forward * thrust * power, ForceMode.Acceleration);

                // Turning stays possible at low speed but strengthens with speed.
                float speedFactor = Mathf.Lerp(0.55f, 1f, Body.linearVelocity.magnitude / MaxSpeed);
                Body.AddTorque(Vector3.up * Input.Steer * TurnPower * speedFactor, ForceMode.Acceleration);
            }

            // River current always nudges downstream.
            if (Flow != null)
                Body.AddForce(Flow.CurrentAt(transform.position), ForceMode.Acceleration);

            // Clamp speed for predictability.
            Vector3 v = Body.linearVelocity;
            v.y = 0f;
            if (v.magnitude > MaxSpeed)
                v = v.normalized * MaxSpeed;
            Body.linearVelocity = new Vector3(v.x, 0f, v.z);

            // Follow the water surface and bob gently.
            _bobPhase += dt;
            float targetY = RiverFlow.WaterHeightAt(transform.position, Time.time);
            Vector3 pos = Body.position;
            pos.y = Mathf.Lerp(pos.y, targetY, dt * 6f);
            Body.MovePosition(pos);

            if (_visual != null)
            {
                float roll = Mathf.Sin(_bobPhase * 1.7f) * 2.2f - Input.Steer * 4f;
                float pitch = Mathf.Sin(_bobPhase * 1.3f + 1f) * 1.6f - Input.Thrust * 2f;
                _visual.localRotation = Quaternion.Slerp(_visual.localRotation,
                    Quaternion.Euler(pitch, 0f, roll), dt * 4f);
            }

            DistanceTravelled += Body.linearVelocity.magnitude * dt;
            DetectStuckOrLost(dt);
        }

        void DetectStuckOrLost(float dt)
        {
            if (!ControlEnabled)
            {
                _stuckTimer = 0f;
                return;
            }

            bool tryingToMove = Mathf.Abs(Input.Thrust) > 0.3f;
            bool barelyMoving = Body.linearVelocity.magnitude < StuckSpeed;
            if (tryingToMove && barelyMoving)
                _stuckTimer += dt;
            else
                _stuckTimer = 0f;

            bool outOfBounds = transform.position.y < -3f || transform.position.y > 8f;
            if (Flow != null)
            {
                var branch = Flow.ClosestBranch(transform.position, out int idx);
                if (branch != null)
                {
                    Vector3 d = branch.Spline.PointAt(idx) - transform.position;
                    d.y = 0f;
                    if (d.magnitude > branch.HalfWidth * 2.5f)
                        outOfBounds = true;
                }
            }

            if (_stuckTimer > StuckTimeout || outOfBounds)
            {
                _stuckTimer = 0f;
                RespawnAtCheckpoint(manual: false);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            float impulse = collision.impulse.magnitude / Mathf.Max(Body.mass, 0.01f);
            bool soft = collision.collider.GetComponent<SoftSurface>() != null;
            bool hard = !soft && impulse > HardHitImpulse;

            if (hard && Hull != null)
            {
                bool applied = Hull.ApplyHit();
                if (applied && Stats != null)
                    Stats.Collisions++;
                if (!applied)
                    hard = false; // invulnerability window: treat as a soft scrape
            }

            var am = AudioManager.Instance;
            if (am != null)
            {
                if (hard)
                    am.PlaySfx(am.Collision, Mathf.Clamp01(impulse / 10f) * 0.6f + 0.35f);
                else if (impulse > 0.8f)
                    am.PlaySfx(am.SoftBump, 0.4f);
            }

            CollisionOccurred?.Invoke(impulse, hard);
        }

        public void RespawnAtCheckpoint(bool manual)
        {
            if (Checkpoints == null || !Checkpoints.HasCheckpoint)
                return;

            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = Checkpoints.Position;
            Body.rotation = Checkpoints.Rotation;
            transform.SetPositionAndRotation(Checkpoints.Position, Checkpoints.Rotation);

            if (Stats != null)
                Stats.Respawns++;
            Respawned?.Invoke();
        }

        public void TeleportTo(Vector3 position, Quaternion rotation)
        {
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
