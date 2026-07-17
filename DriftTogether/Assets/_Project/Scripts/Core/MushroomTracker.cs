using System;
using System.Collections.Generic;

namespace DriftTogether.Core
{
    /// <summary>
    /// Counts collected glowing mushrooms by unique id, so respawns and
    /// double triggers can never count the same mushroom twice.
    /// </summary>
    public sealed class MushroomTracker
    {
        public const int Goal = 5;

        readonly HashSet<int> _collected = new HashSet<int>();

        public int Count => _collected.Count;

        public event Action<int> Collected;

        public bool IsCollected(int id) => _collected.Contains(id);

        /// <summary>Returns true only the first time a given mushroom id is collected.</summary>
        public bool Collect(int id)
        {
            if (!_collected.Add(id))
                return false;
            Collected?.Invoke(Count);
            return true;
        }
    }
}
