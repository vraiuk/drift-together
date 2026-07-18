using DriftTogether.Core;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// The crew raft. Physics and rules run on the host only; clients receive
    /// the transform through NetworkTransform and mirror state via
    /// NetworkVariables. Deck visuals are built in code on every peer.
    /// </summary>
    public sealed class RaftController : NetworkBehaviour
    {
        public const int MaxHull = 5;
        public const float MaxSpeed = 5.5f;
        public const float OarImpulse = 7.5f;
        public const float OarYaw = 1.1f;
        public const float RudderTorque = 1.9f;
        public const float RepairHoldSeconds = 3f;
        public const float RepairCooldown = 5f;
        public const float HardHitImpulse = 4f;

        public NetworkVariable<int> Hull = new NetworkVariable<int>(MaxHull);
        public NetworkVariable<float> RudderAngle = new NetworkVariable<float>(0f);
        /// <summary>Shared food store (fish and foraged supplies).</summary>
        public NetworkVariable<int> Food = new NetworkVariable<int>(0);
        /// <summary>Shared timber store (shore logging, UC-10/13).</summary>
        public NetworkVariable<int> Logs = new NetworkVariable<int>(0);
        public NetworkVariable<bool> Capsized = new NetworkVariable<bool>(false);
        public NetworkVariable<float> TiltSync = new NetworkVariable<float>(0f);

        /// <summary>Host-side balance model (UC-08).</summary>
        public CapsizeSystem Balance { get; } = new CapsizeSystem();

        public NetworkVariable<int> AnchorSync = new NetworkVariable<int>((int)AnchorState.Raised);
        /// <summary>Host-side anchor model (UC-06).</summary>
        public AnchorSystem Anchor { get; private set; }
        Transform _anchorMarker;

        /// <summary>UC-13: модули плота (бит-маска RaftSlot).</summary>
        public NetworkVariable<int> ModulesMask = new NetworkVariable<int>(0);
        public ModuleSystem Modules { get; } = new ModuleSystem();

        /// <summary>UC-09: покинутый плот уплывает и цепляется ниже по течению.</summary>
        public NetworkVariable<bool> Snagged = new NetworkVariable<bool>(false);
        public readonly AdriftRule Adrift = new AdriftRule();
        float _snagLineZ = float.NaN;

        public PostSystem Posts { get; } = new PostSystem();
        public HullIntegrity Integrity { get; private set; }

        public Vector3 CampfireBowlPosition => transform.TransformPoint(new Vector3(0f, 0.45f, 0.2f));

        Rigidbody _body;
        RiverFlow _flow;
        CoopRunStats _stats;
        Transform _visual;
        Transform _postRudder;
        Transform _postOarLeft;
        Transform _postOarRight;
        float _repairHold;
        float _repairCooldown;
        float _rudderInput;

        public override void OnNetworkSpawn()
        {
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.mass = 4f;
            _body.linearDamping = 1.1f;
            _body.angularDamping = 2.8f;
            _body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.isKinematic = !IsServer;

            // Slick hull: rocks should deflect the raft, not glue it in place.
            var collider = GetComponent<BoxCollider>();
            if (collider != null)
                collider.material = new PhysicsMaterial("RaftSlick")
                {
                    dynamicFriction = 0.05f,
                    staticFriction = 0.05f,
                    bounciness = 0.2f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };

            if (IsServer)
            {
                Integrity = new HullIntegrity(new GameClock(), MaxHull);
                Anchor = new AnchorSystem(new GameClock(), unchecked((int)Time.frameCount * 31 + 7));
            }

            BuildVisuals();
            CoopBootstrap.RegisterRaft(this);

            ModulesMask.OnValueChanged += (_, mask) =>
            {
                Modules.LoadMask(mask);
                Balance.ExtraTiltFactor = Modules.TiltFactor;
                RefreshModuleVisuals();
            };
            Modules.LoadMask(ModulesMask.Value);
            RefreshModuleVisuals();
        }

        // ---------- UC-13: модули ----------

        static readonly UnityEngine.Vector3[] SlotLocal =
        {
            new Vector3(0f, 0.4f, 1.05f),    // Bow — парус
            new Vector3(-0.9f, 0.4f, -0.5f), // Midship — тент
            new Vector3(1.1f, 0.4f, 0.7f)    // Stern-corner — верстак
        };

        readonly GameObject[] _moduleVisuals = new GameObject[3];

        public RaftSlot? NearSlot(Vector3 position, float radius)
        {
            for (int i = 0; i < SlotLocal.Length; i++)
            {
                if (Modules.Has((RaftSlot)i))
                    continue;
                if (Vector3.Distance(position, transform.TransformPoint(SlotLocal[i])) < radius)
                    return (RaftSlot)i;
            }
            return null;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void InstallModuleServerRpc(int slot, RpcParams p = default)
        {
            if (Capsized.Value || slot < 0 || slot > 2)
                return;
            int cost = Modules.TryInstall((RaftSlot)slot, Logs.Value);
            if (cost < 0)
                return;
            Logs.Value -= cost;
            ModulesMask.Value = Modules.Mask;
            if (_stats != null)
                _stats.ModulesBuilt++;
            Balance.ExtraTiltFactor = Modules.TiltFactor;
            GetComponent<CoopFlow>().CampfireRestClientRpc();
            CoopBootstrap.HostSayModules();
        }

        void RefreshModuleVisuals()
        {
            for (int i = 0; i < 3; i++)
            {
                bool has = Modules.Has((RaftSlot)i);
                if (has && _moduleVisuals[i] == null)
                    _moduleVisuals[i] = BuildModuleVisual((RaftSlot)i);
                else if (_moduleVisuals[i] != null)
                    _moduleVisuals[i].SetActive(has);
            }
        }

        GameObject BuildModuleVisual(RaftSlot slot)
        {
            var root = new GameObject($"Module_{slot}");
            root.transform.SetParent(_visual != null ? _visual : transform, false);
            root.transform.localPosition = SlotLocal[(int)slot];
            switch (slot)
            {
                case RaftSlot.Bow: // Парус
                    var sailMast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(sailMast.GetComponent<Collider>());
                    sailMast.transform.SetParent(root.transform, false);
                    sailMast.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    sailMast.transform.localScale = new Vector3(0.1f, 1.1f, 0.1f);
                    GameMaterials.ApplyTo(sailMast, "Wood");
                    var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(cloth.GetComponent<Collider>());
                    cloth.transform.SetParent(root.transform, false);
                    cloth.transform.localPosition = new Vector3(0f, 1.35f, -0.05f);
                    cloth.transform.localScale = new Vector3(1.9f, 1.3f, 0.04f);
                    GameMaterials.ApplyTo(cloth, "KayakTrim");
                    break;
                case RaftSlot.Midship: // Тент
                    for (int px = -1; px <= 1; px += 2)
                        for (int pz = -1; pz <= 1; pz += 2)
                        {
                            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            Destroy(pole.GetComponent<Collider>());
                            pole.transform.SetParent(root.transform, false);
                            pole.transform.localPosition = new Vector3(px * 0.6f, 0.75f, pz * 0.6f);
                            pole.transform.localScale = new Vector3(0.06f, 0.75f, 0.06f);
                            GameMaterials.ApplyTo(pole, "Wood");
                        }
                    var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(roof.GetComponent<Collider>());
                    roof.transform.SetParent(root.transform, false);
                    roof.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                    roof.transform.localScale = new Vector3(1.5f, 0.07f, 1.5f);
                    GameMaterials.ApplyTo(roof, "Foliage");
                    break;
                case RaftSlot.Stern: // Верстак
                    var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(table.GetComponent<Collider>());
                    table.transform.SetParent(root.transform, false);
                    table.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                    table.transform.localScale = new Vector3(0.8f, 0.1f, 0.5f);
                    GameMaterials.ApplyTo(table, "KayakTrim");
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(leg.GetComponent<Collider>());
                    leg.transform.SetParent(root.transform, false);
                    leg.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                    leg.transform.localScale = new Vector3(0.15f, 0.35f, 0.15f);
                    GameMaterials.ApplyTo(leg, "Wood");
                    var tool = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(tool.GetComponent<Collider>());
                    tool.transform.SetParent(root.transform, false);
                    tool.transform.localPosition = new Vector3(0.2f, 0.48f, 0f);
                    tool.transform.localScale = Vector3.one * 0.16f;
                    GameMaterials.ApplyTo(tool, "Rock");
                    break;
            }
            return root;
        }

        public void HostAttach(RiverFlow flow, CoopRunStats stats)
        {
            _flow = flow;
            _stats = stats;
        }

        void BuildVisuals()
        {
            if (_visual != null)
                return;
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            // Deck of logs.
            for (int i = 0; i < 7; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "DeckLog";
                log.transform.SetParent(_visual, false);
                log.transform.localPosition = new Vector3(-1.65f + i * 0.55f, 0.1f, 0f);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                log.transform.localScale = new Vector3(0.52f, 1.55f, 0.52f);
                GameMaterials.ApplyTo(log, "Wood");
                Destroy(log.GetComponent<Collider>());
            }

            // Cross beams.
            foreach (float z in new[] { -1.25f, 1.25f })
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Beam";
                beam.transform.SetParent(_visual, false);
                beam.transform.localPosition = new Vector3(0f, 0.34f, z);
                beam.transform.localScale = new Vector3(3.9f, 0.12f, 0.24f);
                GameMaterials.ApplyTo(beam, "TreeTrunk");
                Destroy(beam.GetComponent<Collider>());
            }

            // Campfire bowl in the middle.
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "FireBowl";
            bowl.transform.SetParent(_visual, false);
            bowl.transform.localPosition = new Vector3(0f, 0.36f, 0.2f);
            bowl.transform.localScale = new Vector3(0.8f, 0.12f, 0.8f);
            GameMaterials.ApplyTo(bowl, "Rock");
            Destroy(bowl.GetComponent<Collider>());

            var flame = new GameObject("BowlFlame");
            flame.transform.SetParent(_visual, false);
            flame.transform.localPosition = new Vector3(0f, 0.45f, 0.2f);
            var flameFilter = flame.AddComponent<MeshFilter>();
            flameFilter.mesh = MeshFactory.BuildCone(0.22f, 0.55f, 8);
            flame.AddComponent<MeshRenderer>().sharedMaterial = GameMaterials.Get("FireGlow");

            var fireLightGo = new GameObject("BowlLight");
            fireLightGo.transform.SetParent(_visual, false);
            fireLightGo.transform.localPosition = new Vector3(0f, 1f, 0.2f);
            var fireLight = fireLightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.62f, 0.25f);
            fireLight.intensity = 1.8f;
            fireLight.range = 9f;
            fireLightGo.AddComponent<FlickerLight>();

            // Lantern mast at the bow.
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(_visual, false);
            mast.transform.localPosition = new Vector3(0f, 1.05f, 1.35f);
            mast.transform.localScale = new Vector3(0.09f, 0.75f, 0.09f);
            GameMaterials.ApplyTo(mast, "Wood");
            Destroy(mast.GetComponent<Collider>());

            var lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lantern.name = "Lantern";
            lantern.transform.SetParent(_visual, false);
            lantern.transform.localPosition = new Vector3(0f, 1.85f, 1.35f);
            lantern.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            GameMaterials.ApplyTo(lantern, "FinishGlow");
            Destroy(lantern.GetComponent<Collider>());

            // Тапок-Тим on a barrel at the stern corner.
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "TimBarrel";
            barrel.transform.SetParent(_visual, false);
            barrel.transform.localPosition = new Vector3(1.2f, 0.45f, -1.1f);
            barrel.transform.localScale = new Vector3(0.45f, 0.28f, 0.45f);
            GameMaterials.ApplyTo(barrel, "Wood");
            Destroy(barrel.GetComponent<Collider>());
            BuildTimOnBarrel(barrel.transform);

            // Rudder blade at the stern.
            var rudder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rudder.name = "RudderBlade";
            rudder.transform.SetParent(_visual, false);
            rudder.transform.localPosition = new Vector3(0f, 0.15f, -1.85f);
            rudder.transform.localScale = new Vector3(0.1f, 0.5f, 0.7f);
            GameMaterials.ApplyTo(rudder, "KayakTrim");
            Destroy(rudder.GetComponent<Collider>());
            _rudderBlade = rudder.transform;

            // Post markers.
            _postRudder = CreatePostMarker("Post_Rudder", new Vector3(0f, 0.42f, -1.35f), 180f);
            _postOarLeft = CreatePostMarker("Post_OarLeft", new Vector3(-1.55f, 0.42f, 0.35f), -90f);
            _postOarRight = CreatePostMarker("Post_OarRight", new Vector3(1.55f, 0.42f, 0.35f), 90f);

            // Fishing rod stands at the bow corners (UC-04).
            _rodStands[0] = CreateRodStand(new Vector3(-1.4f, 0.35f, 1.25f));
            _rodStands[1] = CreateRodStand(new Vector3(1.4f, 0.35f, 1.25f));

            // Anchor winch at the bow (UC-06).
            _anchorMarker = new GameObject("AnchorSpot").transform;
            _anchorMarker.SetParent(transform, false);
            _anchorMarker.localPosition = new Vector3(0f, 0.4f, 1.7f);
            var winch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            winch.name = "AnchorWinch";
            winch.transform.SetParent(_anchorMarker, false);
            winch.transform.localScale = new Vector3(0.4f, 0.3f, 0.3f);
            GameMaterials.ApplyTo(winch, "Rock");
            Destroy(winch.GetComponent<Collider>());
            _anchorRope = new GameObject("AnchorRope").transform;
            var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.transform.SetParent(_anchorRope, false);
            rope.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
            rope.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            GameMaterials.ApplyTo(rope, "TreeTrunk");
            Destroy(rope.GetComponent<Collider>());
            _anchorRope.SetParent(_anchorMarker, false);
            _anchorRope.gameObject.SetActive(false);
        }

        Transform _anchorRope;

        public bool NearAnchor(Vector3 position, float radius) =>
            _anchorMarker != null && Vector3.Distance(position, _anchorMarker.position) < radius;

        readonly Transform[] _rodStands = new Transform[2];
        readonly bool[] _rodTaken = new bool[2];

        Transform CreateRodStand(Vector3 localPos)
        {
            var stand = new GameObject("RodStand").transform;
            stand.SetParent(transform, false);
            stand.localPosition = localPos;

            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Rod";
            rod.transform.SetParent(stand, false);
            rod.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            rod.transform.localRotation = Quaternion.Euler(-24f, 0f, 0f);
            rod.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
            GameMaterials.ApplyTo(rod, "Wood");
            Destroy(rod.GetComponent<Collider>());
            return stand;
        }

        /// <summary>Nearest stand with a rod still on it (owner-side query).</summary>
        public int NearestRodStand(Vector3 position, float radius)
        {
            for (int i = 0; i < _rodStands.Length; i++)
            {
                if (_rodStands[i] == null)
                    continue;
                if (Vector3.Distance(position, _rodStands[i].position) < radius)
                    return i;
            }
            return -1;
        }

        public bool TryTakeRod(int stand)
        {
            if (stand < 0 || stand >= _rodTaken.Length || _rodTaken[stand])
                return false;
            _rodTaken[stand] = true;
            RodVisibleClientRpc(stand, false);
            return true;
        }

        public void ReturnRod(int stand)
        {
            if (stand < 0 || stand >= _rodTaken.Length)
                return;
            _rodTaken[stand] = false;
            RodVisibleClientRpc(stand, true);
        }

        [Rpc(SendTo.Everyone)]
        void RodVisibleClientRpc(int stand, bool visible)
        {
            if (stand >= 0 && stand < _rodStands.Length && _rodStands[stand] != null)
            {
                var rod = _rodStands[stand].Find("Rod");
                if (rod != null)
                    rod.gameObject.SetActive(visible);
            }
        }

        Transform _rudderBlade;

        void BuildTimOnBarrel(Transform barrel)
        {
            var tim = new GameObject("Tim");
            tim.transform.SetParent(barrel, false);
            tim.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            tim.transform.localScale = new Vector3(2.2f, 3.6f, 2.2f);

            var sole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sole.transform.SetParent(tim.transform, false);
            sole.transform.localScale = new Vector3(0.28f, 0.09f, 0.62f);
            GameMaterials.ApplyTo(sole, "Slipper");
            Destroy(sole.GetComponent<Collider>());

            var toe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            toe.transform.SetParent(tim.transform, false);
            toe.transform.localPosition = new Vector3(0f, 0.1f, 0.18f);
            toe.transform.localScale = new Vector3(0.26f, 0.2f, 0.34f);
            GameMaterials.ApplyTo(toe, "Slipper");
            Destroy(toe.GetComponent<Collider>());

            foreach (float side in new[] { -1f, 1f })
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(tim.transform, false);
                eye.transform.localPosition = new Vector3(side * 0.07f, 0.24f, 0.16f);
                eye.transform.localScale = new Vector3(0.09f, 0.11f, 0.09f);
                GameMaterials.ApplyTo(eye, "EyeWhite");
                Destroy(eye.GetComponent<Collider>());

                var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pupil.transform.SetParent(eye.transform, false);
                pupil.transform.localPosition = new Vector3(0f, 0.05f, 0.35f);
                pupil.transform.localScale = new Vector3(0.42f, 0.38f, 0.42f);
                GameMaterials.ApplyTo(pupil, "EyePupil");
                Destroy(pupil.GetComponent<Collider>());
            }
        }

        Transform CreatePostMarker(string name, Vector3 localPos, float yaw)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(transform, false);
            marker.localPosition = localPos;
            marker.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plate.name = "Plate";
            plate.transform.SetParent(marker, false);
            plate.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            plate.transform.localScale = new Vector3(0.7f, 0.03f, 0.7f);
            GameMaterials.ApplyTo(plate, "KayakTrim");
            Destroy(plate.GetComponent<Collider>());
            return marker;
        }

        public Transform PostMarker(RaftPost post)
        {
            switch (post)
            {
                case RaftPost.Rudder: return _postRudder;
                case RaftPost.OarLeft: return _postOarLeft;
                case RaftPost.OarRight: return _postOarRight;
                default: return null;
            }
        }

        public RaftPost NearestPost(Vector3 position, float radius)
        {
            RaftPost best = RaftPost.None;
            float bestDist = radius;
            foreach (RaftPost post in new[] { RaftPost.Rudder, RaftPost.OarLeft, RaftPost.OarRight })
            {
                Transform marker = PostMarker(post);
                if (marker == null)
                    continue;
                float d = Vector3.Distance(position, marker.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = post;
                }
            }
            return best;
        }

        public Vector3 DeckPoint(Vector3 localOffset) =>
            transform.TransformPoint(localOffset + new Vector3(0f, 0.55f, 0f));

        // ---------- Host physics ----------

        /// <summary>Host-only direct control used by the co-op smoke autopilot.</summary>
        public float AutoThrust;
        public float AutoSteer;

        /// <summary>UC-11: во время волока плотом управляет PortageController.</summary>
        public bool PortageActive;

        void FixedUpdate()
        {
            if (!IsServer || _body == null)
                return;
            float dt = Time.fixedDeltaTime;

            if (PortageActive)
                return;

            UpdateBalance(dt);
            UpdateAnchor(dt);
            UpdateAdrift(dt);

            if (Snagged.Value)
            {
                // Застрял в камнях: стоит на месте, ждёт экипаж.
                _body.linearVelocity = Vector3.Lerp(_body.linearVelocity, Vector3.zero, dt * 6f);
                Vector3 spos = _body.position;
                spos.y = Mathf.Lerp(spos.y, RiverFlow.WaterHeightAt(spos, Time.time) + 0.12f, dt * 5f);
                _body.MovePosition(spos);
                _repairCooldown -= dt;
                return;
            }

            if (Anchor != null && Anchor.State == AnchorState.Holding && !Capsized.Value)
            {
                // Held in place: bleed velocity, keep only the water bob.
                _body.linearVelocity = Vector3.Lerp(_body.linearVelocity, Vector3.zero, dt * 4f);
                Vector3 apos = _body.position;
                apos.y = Mathf.Lerp(apos.y, RiverFlow.WaterHeightAt(apos, Time.time) + 0.12f, dt * 5f);
                _body.MovePosition(apos);
                _repairCooldown -= dt;
                return;
            }

            if (_flow != null)
            {
                float anchorFactor = (Anchor != null && Anchor.State == AnchorState.Dragging ? 0.45f : 1.05f) *
                    Modules.SpeedMultiplier;
                _body.AddForce(_flow.CurrentAt(transform.position) * anchorFactor, ForceMode.Acceleration);
            }

            if (Capsized.Value)
            {
                // Flipped raft only drifts; no propulsion, no steering.
                Vector3 cv = _body.linearVelocity;
                cv.y = 0f;
                if (cv.magnitude > 1.6f)
                    _body.linearVelocity = cv.normalized * 1.6f;
                Vector3 cpos = _body.position;
                cpos.y = Mathf.Lerp(cpos.y, RiverFlow.WaterHeightAt(cpos, Time.time) + 0.05f, dt * 5f);
                _body.MovePosition(cpos);
                _repairCooldown -= dt;
                return;
            }

            if (Mathf.Abs(AutoThrust) > 0.01f)
            {
                _body.AddForce(transform.forward * AutoThrust * 9f, ForceMode.Acceleration);
                // Direct yaw assist: the autopilot has no crew to row the turn.
                _body.AddTorque(Vector3.up * AutoSteer * 2.6f, ForceMode.Acceleration);
                _rudderInput = AutoSteer;
            }

            // Rudder: torque grows with speed.
            RudderAngle.Value = Mathf.MoveTowards(RudderAngle.Value, _rudderInput * 30f, 70f * dt);
            float speedFactor = Mathf.Clamp01(_body.linearVelocity.magnitude / MaxSpeed) + 0.2f;
            _body.AddTorque(Vector3.up * (RudderAngle.Value / 30f) * RudderTorque * speedFactor *
                Modules.TurnMultiplier, ForceMode.Acceleration);

            float maxSpeed = (Hull.Value <= 0 ? MaxSpeed * 0.5f : MaxSpeed) * Modules.SpeedMultiplier;
            Vector3 v = _body.linearVelocity;
            v.y = 0f;
            if (v.magnitude > maxSpeed)
                v = v.normalized * maxSpeed;
            _body.linearVelocity = new Vector3(v.x, 0f, v.z);

            // Follow the water.
            Vector3 pos = _body.position;
            pos.y = Mathf.Lerp(pos.y, RiverFlow.WaterHeightAt(pos, Time.time) + 0.12f, dt * 5f);
            _body.MovePosition(pos);

            _repairCooldown -= dt;
        }

        void Update()
        {
            if (_rudderBlade != null)
                _rudderBlade.localRotation = Quaternion.Euler(0f, -RudderAngle.Value, 0f);

            // Balance presentation on every peer: heel with the tilt, flip when capsized.
            if (_visual != null)
            {
                Quaternion target = Capsized.Value
                    ? Quaternion.Euler(0f, 0f, 180f)
                    : Quaternion.Euler(0f, 0f, -TiltSync.Value * 26f);
                _visual.localRotation = Quaternion.Slerp(_visual.localRotation, target,
                    Time.deltaTime * (Capsized.Value ? 3.5f : 6f));
            }
        }

        readonly System.Collections.Generic.List<float> _crewX =
            new System.Collections.Generic.List<float>(4);

        void UpdateBalance(float dt)
        {
            _crewX.Clear();
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                Vector3 local = transform.InverseTransformPoint(avatar.transform.position);
                bool aboard = Mathf.Abs(local.x) < 2.4f && Mathf.Abs(local.z) < 2.1f &&
                              local.y > -0.2f && local.y < 1.6f;
                if (aboard)
                    _crewX.Add(local.x);
            }

            bool fastWater = _flow != null &&
                             _flow.CurrentAt(transform.position).magnitude > 2f;
            bool wasCapsized = Balance.Capsized;
            Balance.Tick(dt, _crewX, fastWater);
            TiltSync.Value = Balance.Tilt;

            if (Balance.Capsized && !wasCapsized)
                HostDoCapsize();
            if (!Balance.Capsized && Capsized.Value)
                HostDoRighted();
        }

        void HostDoCapsize()
        {
            Capsized.Value = true;
            _rudderInput = 0f;
            foreach (RaftPost post in new[] { RaftPost.Rudder, RaftPost.OarLeft, RaftPost.OarRight })
            {
                ulong? occupant = Posts.OccupantOf(post);
                if (occupant.HasValue)
                    Posts.Release(post, occupant.Value);
            }

            if (_stats != null)
                _stats.Capsizes++;

            // Часть еды уплывает ящиками — какие успеете выловить.
            int lost = CapsizeSystem.FoodLost(Food.Value);
            Food.Value -= lost;
            var cratePrefab = SessionManager.Instance != null
                ? SessionManager.Instance.LoadNetPrefab("Crate")
                : null;
            for (int i = 0; i < lost && cratePrefab != null; i++)
            {
                Vector3 pos = transform.position + new Vector3(
                    Random.Range(-3f, 3f), 0f, Random.Range(1f, 4f));
                var crate = Instantiate(cratePrefab, pos, Quaternion.identity);
                crate.GetComponent<NetworkObject>().Spawn(true);
            }

            // Экипаж — в воду, по обе стороны от плота.
            int side = 0;
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                Vector3 local = transform.InverseTransformPoint(avatar.transform.position);
                if (Mathf.Abs(local.x) < 2.6f && Mathf.Abs(local.z) < 2.4f)
                {
                    Vector3 splash = transform.position +
                                     transform.right * (side % 2 == 0 ? 3.4f : -3.4f) +
                                     transform.forward * Random.Range(-1f, 1f);
                    splash.y = RiverFlow.WaterHeightAt(splash, Time.time) - 0.4f;
                    avatar.ForceIntoWater(splash);
                    side++;
                }
            }

            GetComponent<CoopFlow>().CapsizedClientRpc();
            CoopBootstrap.HostSayCapsize();
        }

        void HostDoRighted()
        {
            Capsized.Value = false;
            GetComponent<CoopFlow>().RightedClientRpc();
        }

        void UpdateAdrift(float dt)
        {
            // The smoke autopilot is a ghost helmsman: the raft is never abandoned.
            bool anyoneAboard = _crewX.Count > 0 || Mathf.Abs(AutoThrust) > 0.01f;
            bool anchorHolding = Anchor != null && Anchor.State == AnchorState.Holding;

            if (Snagged.Value)
            {
                // Экипаж вернулся на борт — плот освобождается.
                if (anyoneAboard)
                {
                    Snagged.Value = false;
                    Adrift.Reset();
                    _snagLineZ = float.NaN;
                    GetComponent<CoopFlow>().RaftBumpClientRpc();
                }
                return;
            }

            bool justAdrift = Adrift.Tick(dt, anyoneAboard, anchorHolding, Capsized.Value);
            if (justAdrift)
            {
                _snagLineZ = DriftCatch.SnagLineFor(transform.position.z);
                if (_stats != null)
                    _stats.RaftLosses++;
                CoopBootstrap.HostSayRaftLost();
            }

            if (Adrift.IsAdrift && anyoneAboard)
            {
                Adrift.Reset();
                _snagLineZ = float.NaN;
            }

            if (Adrift.IsAdrift && !float.IsNaN(_snagLineZ) &&
                transform.position.z >= _snagLineZ)
            {
                // Река вернула плот: зацепился за камни, ждёт команду.
                Snagged.Value = true;
                Adrift.Reset();
                _snagLineZ = float.NaN;
                if (Integrity != null && Integrity.Current > 1 && Integrity.ApplyHit())
                    Hull.Value = Integrity.Current; // лёгкая цена за побег
                GetComponent<CoopFlow>().RaftHitClientRpc(3f);
                CoopBootstrap.HostSayRaftSnagged();
            }
        }

        void UpdateAnchor(float dt)
        {
            if (Anchor == null)
                return;
            float current = _flow != null ? _flow.CurrentAt(transform.position).magnitude : 0f;
            Anchor.UpdateConditions(current, CoopBootstrap.InMooringZone(transform.position));
            var before = Anchor.State;
            Anchor.Tick();
            if (Anchor.State != before && Anchor.State == AnchorState.Dragging)
            {
                GetComponent<CoopFlow>().RaftBumpClientRpc();
                CoopBootstrap.HostSayAnchorDragging();
            }
            if (AnchorSync.Value != (int)Anchor.State)
                AnchorSync.Value = (int)Anchor.State;
            if (_anchorRope != null)
                _anchorRope.gameObject.SetActive(Anchor.IsDown);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ToggleAnchorServerRpc(RpcParams p = default)
        {
            if (Anchor == null || Capsized.Value)
                return;
            if (Anchor.IsDown)
            {
                Anchor.Raise();
            }
            else
            {
                float current = _flow != null ? _flow.CurrentAt(transform.position).magnitude : 0f;
                Anchor.Drop(current, CoopBootstrap.InMooringZone(transform.position));
            }
            AnchorSync.Value = (int)Anchor.State;
            GetComponent<CoopFlow>().ChainClientRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RightingEffortServerRpc(RpcParams p = default)
        {
            if (!Capsized.Value)
                return;
            Balance.AddRightingEffort(1f);
            if (!Balance.Capsized)
                HostDoRighted();
            else
                GetComponent<CoopFlow>().RaftBumpClientRpc();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || Integrity == null)
                return;
            if (collision.collider.GetComponent<SoftSurface>() != null)
                return;
            float impulse = collision.impulse.magnitude / Mathf.Max(_body.mass, 0.01f);
            if (impulse > HardHitImpulse)
            {
                float sideSign = collision.contactCount > 0
                    ? Mathf.Sign(transform.InverseTransformPoint(collision.GetContact(0).point).x)
                    : 1f;
                Balance.ApplyHitKick(sideSign);
            }
            if (impulse > HardHitImpulse && Integrity.ApplyHit())
            {
                Hull.Value = Integrity.Current;
                if (_stats != null)
                    _stats.RaftCollisions++;
                CoopBootstrap.OnRaftHit(impulse);
            }
            else if (impulse > 1.2f)
            {
                CoopBootstrap.OnRaftBump(impulse);
            }
        }

        /// <summary>UC-15: последствия прыжка с водопада — прочность «в хлам», груз за борт.</summary>
        public void HostWaterfallLanding()
        {
            if (!IsServer || Integrity == null)
                return;
            while (Integrity.Current > 1)
                if (!ForceHullDown())
                    break;
            Hull.Value = Integrity.Current;
            Food.Value = Mathf.CeilToInt(Food.Value * 0.5f);
            Logs.Value = Mathf.CeilToInt(Logs.Value * 0.5f);
        }

        bool ForceHullDown()
        {
            // ApplyHit уважает неуязвимость — при посадке с водопада она не защищает.
            var field = typeof(HullIntegrity).GetField("_lastDamageTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(Integrity, float.NegativeInfinity);
            return Integrity.ApplyHit();
        }

        public void HostRepairFull()
        {
            if (!IsServer || Integrity == null)
                return;
            Integrity.RestoreFull();
            Hull.Value = Integrity.Current;
        }

        public void HostTeleport(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
                return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        // ---------- Post input RPCs ----------

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void SetRudderServerRpc(float steer, RpcParams rpcParams = default)
        {
            if (Capsized.Value)
                return;
            if (Posts.OccupantOf(RaftPost.Rudder) == rpcParams.Receive.SenderClientId)
                _rudderInput = Mathf.Clamp(steer, -1f, 1f);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void OarStrokeServerRpc(bool leftSide, bool reverse, RpcParams rpcParams = default)
        {
            if (Capsized.Value)
                return;
            ulong sender = rpcParams.Receive.SenderClientId;
            RaftPost required = leftSide ? RaftPost.OarLeft : RaftPost.OarRight;
            if (Posts.OccupantOf(required) != sender)
                return;

            float sign = reverse ? -0.55f : 1f;
            _body.AddForce(transform.forward * OarImpulse * sign, ForceMode.Impulse);
            _body.AddTorque(Vector3.up * (leftSide ? OarYaw : -OarYaw) * sign, ForceMode.Impulse);

            var stats = _stats?.GetOrAdd(sender);
            if (stats != null)
                stats.OarStrokes++;

            OarSplashClientRpc(leftSide);
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RepairHoldServerRpc(RpcParams rpcParams = default)
        {
            if (Integrity == null || _repairCooldown > 0f || Integrity.Current >= Integrity.Max)
                return;
            _repairHold += Time.deltaTime * 1.5f * Modules.RepairSpeedMultiplier;
            if (_repairHold >= RepairHoldSeconds)
            {
                _repairHold = 0f;
                _repairCooldown = RepairCooldown;
                if (Integrity.RepairOne())
                    Hull.Value = Integrity.Current;
            }
        }

        [Rpc(SendTo.Everyone)]
        void OarSplashClientRpc(bool leftSide)
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.PaddleStroke, 0.55f, Random.Range(0.85f, 1.05f));
        }
    }
}
