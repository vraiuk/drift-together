using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.Core
{
    public enum LineCategory
    {
        Intro,
        Collision,
        Mushroom,
        Campfire,
        Fork,
        Finish,
        NoisyNerves,
        Idle,
        Respawn,
        Fishing,
        Capsize,
        Anchor,
        Scout
    }

    /// <summary>
    /// Queue of subtitle lines with a per-category cooldown, so the same
    /// situational joke does not repeat too often. Pure logic, unit-testable.
    /// </summary>
    public sealed class DialogueQueue
    {
        public const float DefaultCategoryCooldown = 18f;
        public const int MaxQueued = 3;

        readonly IClock _clock;
        readonly Dictionary<LineCategory, float> _lastShown = new Dictionary<LineCategory, float>();
        readonly Dictionary<LineCategory, int> _nextVariant = new Dictionary<LineCategory, int>();
        readonly Queue<string> _queue = new Queue<string>();

        public DialogueQueue(IClock clock)
        {
            _clock = clock;
        }

        public int QueuedCount => _queue.Count;

        public bool CanShow(LineCategory category, float cooldown = DefaultCategoryCooldown)
        {
            return !_lastShown.TryGetValue(category, out float last) || _clock.Now - last >= cooldown;
        }

        /// <summary>
        /// Tries to enqueue one of the category's line variants (rotating through them).
        /// Returns true if the line was accepted.
        /// </summary>
        public bool TryEnqueue(LineCategory category, IReadOnlyList<string> variants,
            float cooldown = DefaultCategoryCooldown)
        {
            if (variants == null || variants.Count == 0)
                return false;
            if (!CanShow(category, cooldown) || _queue.Count >= MaxQueued)
                return false;

            _nextVariant.TryGetValue(category, out int index);
            _queue.Enqueue(variants[index % variants.Count]);
            _nextVariant[category] = index + 1;
            _lastShown[category] = _clock.Now;
            return true;
        }

        public bool TryDequeue(out string line)
        {
            if (_queue.Count > 0)
            {
                line = _queue.Dequeue();
                return true;
            }
            line = null;
            return false;
        }

        /// <summary>Reading time for a subtitle, based on its length.</summary>
        public static float DisplayDuration(string line)
        {
            return Mathf.Clamp(1.8f + line.Length * 0.055f, 2.5f, 6f);
        }
    }
}
