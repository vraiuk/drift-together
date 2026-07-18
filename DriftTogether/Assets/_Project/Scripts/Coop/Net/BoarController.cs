using DriftTogether.Core;
using DriftTogether.World;
using Unity.Netcode;
using UnityEngine;

namespace DriftTogether.Coop.Net
{
    /// <summary>Grumpy boar near the tourist camp (UC-10). Host-driven.</summary>
    public sealed class BoarController : NetworkBehaviour
    {
        public Vector3 HomePoint;
        public BoarBrain Brain;

        float _wanderAngle;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Brain = new BoarBrain(new GameClock());
                if (HomePoint == Vector3.zero)
                    HomePoint = transform.position;
            }

            if (transform.childCount == 0)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(transform, false);
                body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.55f, 0.5f, 0.55f);
                GameMaterials.ApplyTo(body, "TreeTrunk");

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(transform, false);
                head.transform.localPosition = new Vector3(0f, 0.45f, 0.55f);
                head.transform.localScale = Vector3.one * 0.42f;
                GameMaterials.ApplyTo(head, "TreeTrunk");

                var snout = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(snout.GetComponent<Collider>());
                snout.transform.SetParent(transform, false);
                snout.transform.localPosition = new Vector3(0f, 0.4f, 0.78f);
                snout.transform.localScale = new Vector3(0.2f, 0.16f, 0.16f);
                GameMaterials.ApplyTo(snout, "Slipper");
            }
        }

        void Update()
        {
            if (!IsServer || Brain == null)
                return;

            PlayerAvatar nearest = null;
            float bestDist = float.MaxValue;
            foreach (var avatar in FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(avatar.transform.position, transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearest = avatar;
                }
            }

            bool hit = Brain.Tick(bestDist);
            float dt = Time.deltaTime;

            switch (Brain.Mood)
            {
                case BoarMood.Wandering:
                    _wanderAngle += dt * 0.6f;
                    Vector3 target = HomePoint + new Vector3(
                        Mathf.Cos(_wanderAngle) * 4f, 0f, Mathf.Sin(_wanderAngle) * 4f);
                    MoveToward(target, 1.1f, dt);
                    break;
                case BoarMood.Charging when nearest != null:
                    MoveToward(nearest.transform.position, 3.6f, dt);
                    break;
                case BoarMood.Fleeing:
                    MoveToward(HomePoint + (transform.position - HomePoint).normalized * 10f, 3f, dt);
                    break;
            }

            if (hit && nearest != null)
            {
                Vector3 dir = (nearest.transform.position - transform.position).normalized;
                nearest.ServerPush(dir * 6f + Vector3.up * 0.5f);
                var raft = CoopBootstrap.Raft;
                if (raft != null && raft.Food.Value > 0)
                    raft.Food.Value -= 1; // кабан отбирает перекус
                SnortClientRpc();
                CoopBootstrap.HostSayBoar();
            }
        }

        void MoveToward(Vector3 target, float speed, float dt)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.3f)
                return;
            Vector3 step = to.normalized * speed * dt;
            Vector3 pos = transform.position + step;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit,
                    4f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = groundHit.point.y;
            transform.position = pos;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(to.normalized), dt * 6f);
        }

        [Rpc(SendTo.Everyone)]
        void SnortClientRpc()
        {
            var am = AudioManager.Instance;
            if (am != null)
                am.PlaySfx(am.Collision, 0.5f, 1.8f);
        }
    }
}
