using DriftTogether.Core;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>
    /// A food crate floating away after a capsize (UC-08). Host moves it with
    /// the current; any player close by grabs it with E. Unclaimed crates
    /// sink after a while — часть вещей уплывает безвозвратно.
    /// </summary>
    public sealed class FloatingCrate : NetworkBehaviour
    {
        public const float LifeSeconds = 45f;
        public const float GrabRange = 2.2f;

        public int FoodValue = 1;
        float _dieAt;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                _dieAt = Time.time + LifeSeconds;

            if (transform.childCount == 0)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "CrateVisual";
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(transform, false);
                box.transform.localScale = new Vector3(0.45f, 0.32f, 0.45f);
                box.transform.localRotation = Quaternion.Euler(4f, Random.Range(0f, 360f), -3f);
                GameMaterials.ApplyTo(box, "Wood");
            }
        }

        void Update()
        {
            if (!IsServer)
                return;

            Vector3 pos = transform.position;
            if (CoopBootstrap.Flow != null)
                pos += CoopBootstrap.Flow.CurrentAt(pos) * (Time.deltaTime * 0.8f);
            pos.y = RiverFlow.WaterHeightAt(pos, Time.time) + 0.06f;
            transform.position = pos;

            if (Time.time >= _dieAt)
                NetworkObject.Despawn(true); // уплыла безвозвратно
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void GrabServerRpc(RpcParams p = default)
        {
            if (!IsSpawned)
                return;
            var raft = CoopBootstrap.Raft;
            if (raft != null)
                raft.Food.Value += FoodValue;
            var am = AudioManager.Instance;
            GrabbedClientRpc();
            NetworkObject.Despawn(true);
        }

        [Rpc(SendTo.Everyone)]
        void GrabbedClientRpc()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.MushroomChime, 0.55f, 1.3f);
        }
    }
}
