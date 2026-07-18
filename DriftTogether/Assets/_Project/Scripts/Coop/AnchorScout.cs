using System;
using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.Coop
{
    public enum AnchorState
    {
        Raised,
        Holding,
        Dragging // сорвало течением
    }

    /// <summary>
    /// Bow anchor (UC-02/06/09). Holds the raft in calm water; in a strong
    /// current it holds only for a while and then starts dragging.
    /// </summary>
    public sealed class AnchorSystem
    {
        public const float HoldCurrentLimit = 2.1f;

        readonly Core.IClock _clock;
        readonly System.Random _rng;
        float _dragAt = float.PositiveInfinity;

        public AnchorState State { get; private set; } = AnchorState.Raised;
        public bool IsDown => State != AnchorState.Raised;

        public AnchorSystem(Core.IClock clock, int seed)
        {
            _clock = clock;
            _rng = new System.Random(seed);
        }

        public void Drop(float currentStrength, bool mooringZone)
        {
            State = AnchorState.Holding;
            ScheduleDragCheck(currentStrength, mooringZone);
        }

        public void Raise()
        {
            State = AnchorState.Raised;
            _dragAt = float.PositiveInfinity;
        }

        /// <summary>Call when conditions change (raft drifted into faster water).</summary>
        public void UpdateConditions(float currentStrength, bool mooringZone)
        {
            if (State == AnchorState.Holding)
                ScheduleDragCheck(currentStrength, mooringZone);
        }

        void ScheduleDragCheck(float currentStrength, bool mooringZone)
        {
            if (mooringZone || currentStrength <= HoldCurrentLimit)
                _dragAt = float.PositiveInfinity; // держит надёжно
            else if (float.IsPositiveInfinity(_dragAt))
                _dragAt = _clock.Now + 4f + (float)_rng.NextDouble() * 6f;
        }

        public void Tick()
        {
            if (State == AnchorState.Holding && _clock.Now >= _dragAt)
                State = AnchorState.Dragging;
        }
    }

    /// <summary>One dangerous stretch of the river that a scout can reveal (UC-06).</summary>
    public sealed class ScoutZone
    {
        public string Name;
        public float StartZ;
        public float EndZ;
        public bool Scouted;
        public Vector3[] SafeLine = Array.Empty<Vector3>();

        public bool Contains(float z) => z >= StartZ && z <= EndZ;
    }

    /// <summary>
    /// Scouting rules: a zone counts as разведанная when a player is inside it
    /// while the raft is still safely behind. Pure logic.
    /// </summary>
    public sealed class ScoutSystem
    {
        public const float RaftGap = 20f;

        public readonly List<ScoutZone> Zones = new List<ScoutZone>();

        public event Action<ScoutZone, ulong> ZoneScouted;

        /// <summary>Returns the zone that was just revealed, if any.</summary>
        public ScoutZone TryScout(ulong scoutClientId, float scoutZ, float raftZ)
        {
            foreach (var zone in Zones)
            {
                if (zone.Scouted || !zone.Contains(scoutZ))
                    continue;
                if (raftZ > zone.StartZ - RaftGap)
                    continue; // плот уже слишком близко — это не разведка, это проход
                zone.Scouted = true;
                ZoneScouted?.Invoke(zone, scoutClientId);
                return zone;
            }
            return null;
        }
    }
}
