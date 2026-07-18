using System;

namespace DriftTogether.Coop
{
    public enum FishingState
    {
        Idle,       // rod in hand, not cast
        Waiting,    // bobber in water, waiting for a bite
        Bite,       // fish is on — hook now!
        Struggle,   // big fish fights: hook again or get a friend to help
        Landed,     // fish in the food store (terminal, read result)
        Escaped     // missed the window (terminal)
    }

    /// <summary>
    /// One cast of a fishing rod (UC-04). Pure, deterministic with an
    /// injected RNG; the host owns an instance per fishing player.
    /// </summary>
    public sealed class FishingSession
    {
        public const float BiteWindow = 1.3f;
        public const float StruggleWindow = 4.5f;
        public const int SmallFishFood = 1;
        public const int BigFishFood = 3;
        public const float BigFishChance = 0.28f;

        readonly Core.IClock _clock;
        readonly Random _rng;

        public FishingState State { get; private set; } = FishingState.Idle;
        public bool IsBigFish { get; private set; }
        public int FoodResult { get; private set; }
        float _stateUntil;

        public FishingSession(Core.IClock clock, int seed)
        {
            _clock = clock;
            _rng = new Random(seed);
        }

        /// <summary>Casts the line. Calm spots (заводи) bite much sooner.</summary>
        public void Cast(bool calmSpot)
        {
            State = FishingState.Waiting;
            float delay = calmSpot
                ? 2.5f + (float)_rng.NextDouble() * 6f
                : 8f + (float)_rng.NextDouble() * 16f;
            _stateUntil = _clock.Now + delay;
        }

        /// <summary>Advances timers; call every frame on the host.</summary>
        public void Tick()
        {
            switch (State)
            {
                case FishingState.Waiting when _clock.Now >= _stateUntil:
                    State = FishingState.Bite;
                    IsBigFish = _rng.NextDouble() < BigFishChance;
                    _stateUntil = _clock.Now + BiteWindow;
                    break;
                case FishingState.Bite when _clock.Now >= _stateUntil:
                case FishingState.Struggle when _clock.Now >= _stateUntil:
                    State = FishingState.Escaped;
                    break;
            }
        }

        /// <summary>Player action (E): hook on bite, fight during struggle.</summary>
        public void Hook()
        {
            switch (State)
            {
                case FishingState.Bite when !IsBigFish:
                    FoodResult = SmallFishFood;
                    State = FishingState.Landed;
                    break;
                case FishingState.Bite:
                    State = FishingState.Struggle;
                    _stateUntil = _clock.Now + StruggleWindow;
                    break;
                case FishingState.Struggle:
                    // Fighting alone: 40% per attempt, retry while the window lasts.
                    if (_rng.NextDouble() < 0.4)
                    {
                        FoodResult = BigFishFood;
                        State = FishingState.Landed;
                    }
                    break;
            }
        }

        /// <summary>A second player helping lands the big fish instantly.</summary>
        public void FriendAssist()
        {
            if (State == FishingState.Struggle)
            {
                FoodResult = BigFishFood;
                State = FishingState.Landed;
            }
        }

        public bool IsTerminal => State == FishingState.Landed || State == FishingState.Escaped;
    }
}
