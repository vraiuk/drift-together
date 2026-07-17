using System;
using DriftTogether.Core;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>Glowing mushroom pickup. Ids are unique per level.</summary>
    public sealed class Collectible : MonoBehaviour
    {
        public int Id;
        public event Action<Collectible> PickedUp;

        float _spin;

        void Update()
        {
            _spin += Time.deltaTime;
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            Vector3 p = transform.position;
            p.y = 0.55f + Mathf.Sin(_spin * 1.6f + Id) * 0.12f;
            transform.position = p;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null ||
                other.attachedRigidbody.GetComponent<Player.KayakController>() == null)
                return;
            PickedUp?.Invoke(this);
        }
    }

    /// <summary>Campfire pier: mandatory rest stop, checkpoint + full repair.</summary>
    public sealed class Campfire : MonoBehaviour
    {
        public event Action Rested;
        public bool PlayerInRange { get; private set; }
        public bool HasRested { get; private set; }

        void OnTriggerEnter(Collider other)
        {
            if (IsKayak(other))
                PlayerInRange = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (IsKayak(other))
                PlayerInRange = false;
        }

        internal static bool IsKayak(Collider other)
        {
            return other.attachedRigidbody != null &&
                   other.attachedRigidbody.GetComponent<Player.KayakController>() != null;
        }

        public void Rest()
        {
            HasRested = true;
            Rested?.Invoke();
        }
    }

    /// <summary>Marks which fork branch the player entered.</summary>
    public sealed class RouteGate : MonoBehaviour
    {
        public RiverRoute Route;
        public event Action<RiverRoute> Entered;
        bool _fired;

        void OnTriggerEnter(Collider other)
        {
            if (_fired || other.attachedRigidbody == null ||
                other.attachedRigidbody.GetComponent<Player.KayakController>() == null)
                return;
            _fired = true;
            Entered?.Invoke(Route);
        }
    }

    /// <summary>Finish line trigger.</summary>
    public sealed class FinishZone : MonoBehaviour
    {
        public event Action Finished;
        bool _fired;

        void OnTriggerEnter(Collider other)
        {
            if (_fired || other.attachedRigidbody == null ||
                other.attachedRigidbody.GetComponent<Player.KayakController>() == null)
                return;
            _fired = true;
            Finished?.Invoke();
        }
    }

    /// <summary>Generic checkpoint trigger along the river.</summary>
    public sealed class CheckpointZone : MonoBehaviour
    {
        public Vector3 RespawnPosition;
        public Quaternion RespawnRotation;
        public event Action<CheckpointZone> Reached;
        bool _fired;

        void OnTriggerEnter(Collider other)
        {
            if (_fired || other.attachedRigidbody == null ||
                other.attachedRigidbody.GetComponent<Player.KayakController>() == null)
                return;
            _fired = true;
            Reached?.Invoke(this);
        }
    }

    /// <summary>Zone that makes Тапок-Тим nervous on the noisy stream.</summary>
    public sealed class NervousZone : MonoBehaviour
    {
        public bool PlayerInside { get; private set; }

        void OnTriggerEnter(Collider other)
        {
            if (Campfire.IsKayak(other))
                PlayerInside = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (Campfire.IsKayak(other))
                PlayerInside = false;
        }
    }
}
