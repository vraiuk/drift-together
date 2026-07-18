using DriftTogether.Core;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    public enum GatherKind
    {
        Berries = 0,   // еда
        Logs = 1,      // брёвна
        TouristChest = 2
    }

    /// <summary>
    /// A shore gathering spot (UC-10): berries, a log pile or the tourist
    /// camp chest. Host-spawned; any player takes it with E.
    /// </summary>
    public sealed class GatherNode : NetworkBehaviour
    {
        public const float GatherRange = 2f;

        public NetworkVariable<int> Kind = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            BuildVisual((GatherKind)Kind.Value);
        }

        void BuildVisual(GatherKind kind)
        {
            if (transform.childCount > 0)
                return;
            switch (kind)
            {
                case GatherKind.Berries:
                    for (int i = 0; i < 3; i++)
                    {
                        var berry = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(berry.GetComponent<Collider>());
                        berry.transform.SetParent(transform, false);
                        berry.transform.localPosition = new Vector3((i - 1) * 0.28f, 0.22f + (i % 2) * 0.12f, 0f);
                        berry.transform.localScale = Vector3.one * 0.3f;
                        GameMaterials.ApplyTo(berry, "KayakHull");
                    }
                    var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(bush.GetComponent<Collider>());
                    bush.transform.SetParent(transform, false);
                    bush.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    bush.transform.localScale = new Vector3(1f, 0.6f, 1f);
                    GameMaterials.ApplyTo(bush, "Foliage");
                    break;

                case GatherKind.Logs:
                    for (int i = 0; i < 3; i++)
                    {
                        var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Destroy(log.GetComponent<Collider>());
                        log.transform.SetParent(transform, false);
                        log.transform.localPosition = new Vector3(0f, 0.16f + i * 0.16f, 0f);
                        log.transform.localRotation = Quaternion.Euler(90f, i * 25f, 0f);
                        log.transform.localScale = new Vector3(0.22f, 0.7f, 0.22f);
                        GameMaterials.ApplyTo(log, "Wood");
                    }
                    break;

                case GatherKind.TouristChest:
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(box.GetComponent<Collider>());
                    box.transform.SetParent(transform, false);
                    box.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                    box.transform.localScale = new Vector3(0.7f, 0.5f, 0.45f);
                    GameMaterials.ApplyTo(box, "KayakTrim");
                    var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(lid.GetComponent<Collider>());
                    lid.transform.SetParent(transform, false);
                    lid.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                    lid.transform.localScale = new Vector3(0.74f, 0.12f, 0.49f);
                    GameMaterials.ApplyTo(lid, "Wood");
                    break;
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void GatherServerRpc(RpcParams p = default)
        {
            if (!IsSpawned)
                return;
            var raft = CoopBootstrap.Raft;
            if (raft == null)
                return;

            var stats = CoopBootstrap.Stats?.GetOrAdd(p.Receive.SenderClientId);
            switch ((GatherKind)Kind.Value)
            {
                case GatherKind.Berries:
                    raft.Food.Value += 2;
                    if (stats != null) stats.Gathered += 2;
                    break;
                case GatherKind.Logs:
                    raft.Logs.Value += 2;
                    if (stats != null) stats.Gathered += 2;
                    break;
                case GatherKind.TouristChest:
                    raft.Food.Value += 3;
                    raft.Logs.Value += 2;
                    if (stats != null) stats.Gathered += 5;
                    CoopBootstrap.HostSayCamp();
                    break;
            }
            GatheredClientRpc();
            NetworkObject.Despawn(true);
        }

        [Rpc(SendTo.Everyone)]
        void GatheredClientRpc()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.MushroomChime, 0.6f, 1.2f);
        }

        public static GatherNode Nearest(Vector3 position)
        {
            GatherNode best = null;
            float bestDist = GatherRange;
            foreach (var node in FindObjectsByType<GatherNode>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(node.transform.position, position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = node;
                }
            }
            return best;
        }
    }
}
