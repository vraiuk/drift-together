using System;

namespace DriftTogether.Core
{
    /// <summary>
    /// Kayak hull durability: 3 points, one lost per hard hit, with a short
    /// invulnerability window so a single crash cannot drain everything at once.
    /// </summary>
    public sealed class HullIntegrity
    {
        public const int MaxPoints = 3;
        public const float InvulnerabilityDuration = 1.5f;

        readonly IClock _clock;
        float _lastDamageTime = float.NegativeInfinity;

        /// <summary>Configured maximum (3 for the kayak, 5 for the co-op raft).</summary>
        public int Max { get; }

        public int Current { get; private set; }
        public bool IsBroken => Current <= 0;
        public bool IsInvulnerable => _clock.Now - _lastDamageTime < InvulnerabilityDuration;

        public event Action<int> Changed;
        public event Action Broken;

        public HullIntegrity(IClock clock, int maxPoints = MaxPoints)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Max = maxPoints;
            Current = maxPoints;
        }

        /// <summary>Returns true if the hit actually removed a point.</summary>
        public bool ApplyHit()
        {
            if (IsBroken || IsInvulnerable)
                return false;

            _lastDamageTime = _clock.Now;
            Current--;
            Changed?.Invoke(Current);
            if (Current <= 0)
                Broken?.Invoke();
            return true;
        }

        public void RestoreFull()
        {
            if (Current == Max)
                return;
            Current = Max;
            Changed?.Invoke(Current);
        }
    }
}
