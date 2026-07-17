using UnityEngine;

namespace DriftTogether.Core
{
    /// <summary>Remembers the last safe pose the kayak can respawn at.</summary>
    public sealed class CheckpointSystem
    {
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public bool HasCheckpoint { get; private set; }

        public void Set(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            HasCheckpoint = true;
        }
    }
}
