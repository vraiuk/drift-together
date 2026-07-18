using UnityEngine;

namespace DriftTogether.Coop
{
    public enum PortagePhase
    {
        NotStarted,
        Clearing,   // рубка просеки
        Hauling,    // совместное таскание
        Done
    }

    /// <summary>
    /// UC-11: portage around the final rapids. Clear a path through the
    /// trees, then the whole crew hauls the raft overland; hunger ticks —
    /// без еды тащить вдвое тяжелее. Pure logic, host-driven.
    /// </summary>
    public sealed class PortageSystem
    {
        public const int TreeCount = 3;
        public const int ChopsPerTree = 4;
        public const float HaulSpeedPerPuller = 0.022f;  // прогресс/сек на игрока
        public const float StarvingPenalty = 0.4f;
        public const float FoodPerQuarter = 1f;          // 1 еда за 25% пути

        readonly int[] _chopsLeft = new int[TreeCount];
        float _foodDebt;

        public PortagePhase Phase { get; private set; } = PortagePhase.NotStarted;
        public float HaulProgress { get; private set; }

        public PortageSystem()
        {
            for (int i = 0; i < TreeCount; i++)
                _chopsLeft[i] = ChopsPerTree;
        }

        public int ChopsLeft(int tree) =>
            tree >= 0 && tree < TreeCount ? _chopsLeft[tree] : 0;

        public bool AllTreesCleared
        {
            get
            {
                foreach (int left in _chopsLeft)
                    if (left > 0)
                        return false;
                return true;
            }
        }

        public void Begin()
        {
            if (Phase == PortagePhase.NotStarted)
                Phase = PortagePhase.Clearing;
        }

        /// <summary>One axe swing. Returns true when this swing fells the tree.</summary>
        public bool Chop(int tree)
        {
            if (Phase != PortagePhase.Clearing || tree < 0 || tree >= TreeCount ||
                _chopsLeft[tree] <= 0)
                return false;
            _chopsLeft[tree]--;
            bool felled = _chopsLeft[tree] == 0;
            if (AllTreesCleared)
                Phase = PortagePhase.Hauling;
            return felled;
        }

        /// <summary>
        /// Hauling tick. Returns how much food must be consumed this tick
        /// (0 or more); the caller owns the actual store.
        /// </summary>
        public int Tick(float dt, int pullingPlayers, bool hasFood)
        {
            if (Phase != PortagePhase.Hauling || pullingPlayers <= 0)
                return 0;

            float speed = pullingPlayers * HaulSpeedPerPuller * (hasFood ? 1f : StarvingPenalty);
            float before = HaulProgress;
            HaulProgress = Mathf.Min(1f, HaulProgress + speed * dt);

            // Голод тикает: единица еды за каждую четверть пути.
            _foodDebt += (HaulProgress - before) * 4f * FoodPerQuarter;
            int toConsume = hasFood ? Mathf.FloorToInt(_foodDebt) : 0;
            _foodDebt -= toConsume;

            if (HaulProgress >= 1f)
                Phase = PortagePhase.Done;
            return toConsume;
        }
    }
}
