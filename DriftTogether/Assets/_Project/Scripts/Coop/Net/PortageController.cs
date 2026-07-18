using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// UC-11: волок вокруг финальных порогов. Sits on the raft prefab.
    /// The host owns the PortageSystem; peers get state via NetworkVariables
    /// and build the corridor visuals locally.
    /// </summary>
    public sealed class PortageController : NetworkBehaviour
    {
        public static readonly Vector3 StartPost = new Vector3(9.5f, 0f, 566f);
        public static readonly Vector3[] LandPath =
        {
            new Vector3(10f, 0f, 570f), new Vector3(14f, 0f, 600f),
            new Vector3(15.5f, 0f, 650f), new Vector3(12f, 0f, 695f),
            new Vector3(4f, 0f, 712f)
        };
        public static readonly Vector3[] TreeSpots =
        {
            new Vector3(14f, 0f, 604f), new Vector3(15f, 0f, 644f), new Vector3(13f, 0f, 688f)
        };
        public const float InteractRange = 2.2f;
        public const float PullRange = 5f;

        public NetworkVariable<int> PhaseSync = new NetworkVariable<int>((int)PortagePhase.NotStarted);
        public NetworkVariable<float> ProgressSync = new NetworkVariable<float>(0f);
        public NetworkVariable<int> TreeChops = new NetworkVariable<int>(0); // packed: 3 nibbles

        public PortageSystem Portage { get; } = new PortageSystem();

        RaftController _raft;
        readonly Transform[] _treeVisuals = new Transform[PortageSystem.TreeCount];
        Transform _postVisual;

        public PortagePhase Phase => (PortagePhase)PhaseSync.Value;

        public override void OnNetworkSpawn()
        {
            _raft = GetComponent<RaftController>();
            BuildShoreVisuals();
            PhaseSync.OnValueChanged += (_, _) => RefreshTreeVisuals();
            TreeChops.OnValueChanged += (_, _) => RefreshTreeVisuals();
        }

        void BuildShoreVisuals()
        {
            if (_postVisual != null)
                return;

            // Волоковый столб с канатом на берегу.
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "PortagePost";
            Destroy(post.GetComponent<Collider>());
            post.transform.position = GroundAt(StartPost) + Vector3.up * 0.7f;
            post.transform.localScale = new Vector3(0.22f, 0.7f, 0.22f);
            GameMaterials.ApplyTo(post, "Wood");
            _postVisual = post.transform;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ring.name = "PortageRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(post.transform, false);
            ring.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            ring.transform.localScale = new Vector3(1.4f, 0.35f, 1.4f);
            GameMaterials.ApplyTo(ring, "FinishGlow");

            // Просечные деревья (толстые, отличаются от декоративных).
            for (int i = 0; i < PortageSystem.TreeCount; i++)
            {
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"PortageTree_{i}";
                Destroy(trunk.GetComponent<Collider>());
                Vector3 pos = GroundAt(TreeSpots[i]);
                trunk.transform.position = pos + Vector3.up * 1.4f;
                trunk.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
                GameMaterials.ApplyTo(trunk, "TreeTrunk");

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(crown.GetComponent<Collider>());
                crown.transform.SetParent(trunk.transform, false);
                crown.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                crown.transform.localScale = new Vector3(4.5f, 2.2f, 4.5f);
                GameMaterials.ApplyTo(crown, "Foliage");

                _treeVisuals[i] = trunk.transform;
            }
        }

        static Vector3 GroundAt(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 8f, Vector3.down, out RaycastHit hit,
                    16f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            return pos;
        }

        void RefreshTreeVisuals()
        {
            for (int i = 0; i < PortageSystem.TreeCount; i++)
            {
                if (_treeVisuals[i] == null)
                    continue;
                int left = (TreeChops.Value >> (i * 4)) & 0xF;
                bool standing = Phase == PortagePhase.NotStarted || left > 0;
                _treeVisuals[i].gameObject.SetActive(standing);
                // Подрубленное дерево кренится.
                if (standing && Phase == PortagePhase.Clearing)
                {
                    float lean = (PortageSystem.ChopsPerTree - left) * 6f;
                    _treeVisuals[i].rotation = Quaternion.Euler(0f, 0f, lean);
                }
            }
        }

        // ---------- Owner-side queries ----------

        public bool NearPost(Vector3 pos) =>
            Vector3.Distance(pos, GroundAt(StartPost)) < InteractRange + 0.8f;

        public int NearTree(Vector3 pos)
        {
            for (int i = 0; i < PortageSystem.TreeCount; i++)
            {
                int left = (TreeChops.Value >> (i * 4)) & 0xF;
                if (left > 0 && Vector3.Distance(pos, GroundAt(TreeSpots[i])) < InteractRange)
                    return i;
            }
            return -1;
        }

        public bool CanStart(Vector3 raftPos) =>
            Phase == PortagePhase.NotStarted &&
            Vector3.Distance(raftPos, new Vector3(4f, 0f, 566f)) < 12f;

        // ---------- RPCs ----------

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void BeginServerRpc(RpcParams p = default)
        {
            if (Portage.Phase != PortagePhase.NotStarted || !CanStart(transform.position))
                return;
            Portage.Begin();
            SyncFromModel();
            CoopBootstrap.HostSayPortage();
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void ChopServerRpc(int tree, RpcParams p = default)
        {
            if (Portage.Chop(tree))
                ChopFxClientRpc(true);
            else
                ChopFxClientRpc(false);
            SyncFromModel();
        }

        [Rpc(SendTo.Everyone)]
        void ChopFxClientRpc(bool felled)
        {
            var am = Core.AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.Collision, felled ? 0.8f : 0.45f, felled ? 0.9f : 1.4f);
        }

        void SyncFromModel()
        {
            PhaseSync.Value = (int)Portage.Phase;
            int packed = 0;
            for (int i = 0; i < PortageSystem.TreeCount; i++)
                packed |= Portage.ChopsLeft(i) << (i * 4);
            TreeChops.Value = packed;
        }

        // ---------- Host hauling ----------

        void Update()
        {
            if (!IsServer || Portage.Phase != PortagePhase.Hauling)
            {
                if (IsServer && Portage.Phase == PortagePhase.Done && _raft.PortageActive)
                    FinishPortage();
                return;
            }

            _raft.PortageActive = true;

            // Кто тащит: игроки на суше рядом с плотом, держащие E.
            int pullers = 0;
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                if (avatar.PullingRope &&
                    Vector3.Distance(avatar.transform.position, transform.position) < PullRange)
                    pullers++;
            }

            bool hasFood = _raft.Food.Value > 0;
            int eat = Portage.Tick(Time.deltaTime, pullers, hasFood);
            if (eat > 0)
                _raft.Food.Value = Mathf.Max(0, _raft.Food.Value - eat);

            ProgressSync.Value = Portage.HaulProgress;
            SyncFromModel();

            // Тащим плот вдоль сухопутного пути.
            Vector3 target = EvaluatePath(Portage.HaulProgress);
            var body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.MovePosition(Vector3.Lerp(body.position, target, Time.deltaTime * 4f));
            if (Portage.HaulProgress >= 1f)
                FinishPortage();
        }

        Vector3 EvaluatePath(float t)
        {
            var path = LandPath;
            float total = 0f;
            for (int i = 1; i < path.Length; i++)
                total += Vector3.Distance(path[i - 1], path[i]);
            float d = t * total;
            for (int i = 1; i < path.Length; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (d <= seg)
                {
                    Vector3 p = Vector3.Lerp(path[i - 1], path[i], seg > 0f ? d / seg : 0f);
                    return GroundAt(p) + Vector3.up * 0.3f;
                }
                d -= seg;
            }
            return GroundAt(path[path.Length - 1]) + Vector3.up * 0.3f;
        }

        void FinishPortage()
        {
            _raft.PortageActive = false;
            var body = GetComponent<Rigidbody>();
            Vector3 splash = new Vector3(4f, 0.12f, 712f);
            body.position = splash;
            transform.position = splash;
            body.linearVelocity = Vector3.zero;
            PhaseSync.Value = (int)PortagePhase.Done;
            if (CoopBootstrap.Stats != null)
                CoopBootstrap.Stats.PortageUsed = true;
            GetComponent<CoopFlow>().CampfireRestClientRpc(); // тёплый звук спуска на воду
        }
    }
}
