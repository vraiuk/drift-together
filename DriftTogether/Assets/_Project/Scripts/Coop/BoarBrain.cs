using UnityEngine;

namespace DriftTogether.Coop
{
    public enum BoarMood
    {
        Wandering,
        Charging,
        Fleeing
    }

    /// <summary>
    /// UC-10: a grumpy boar guards the tourist camp. Pure decision logic;
    /// movement is applied by the networked component on the host.
    /// </summary>
    public sealed class BoarBrain
    {
        public const float AggroRange = 5f;
        public const float HitRange = 1.1f;
        public const float ChargeTimeout = 6f;
        public const float FleeSeconds = 9f;

        readonly Core.IClock _clock;
        float _stateUntil;

        public BoarMood Mood { get; private set; } = BoarMood.Wandering;

        public BoarBrain(Core.IClock clock)
        {
            _clock = clock;
        }

        /// <summary>Returns true when this tick lands a hit on the target.</summary>
        public bool Tick(float distanceToNearestPlayer)
        {
            switch (Mood)
            {
                case BoarMood.Wandering:
                    if (distanceToNearestPlayer < AggroRange)
                    {
                        Mood = BoarMood.Charging;
                        _stateUntil = _clock.Now + ChargeTimeout;
                    }
                    return false;

                case BoarMood.Charging:
                    if (distanceToNearestPlayer <= HitRange)
                    {
                        StartFleeing();
                        return true; // боднул!
                    }
                    if (_clock.Now >= _stateUntil || distanceToNearestPlayer > AggroRange * 2.5f)
                        Mood = BoarMood.Wandering;
                    return false;

                case BoarMood.Fleeing:
                    if (_clock.Now >= _stateUntil)
                        Mood = BoarMood.Wandering;
                    return false;
            }
            return false;
        }

        void StartFleeing()
        {
            Mood = BoarMood.Fleeing;
            _stateUntil = _clock.Now + FleeSeconds;
        }
    }
}
