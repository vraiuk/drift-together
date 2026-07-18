using System.Collections.Generic;
using System.Linq;
using DriftTogether.Core;

namespace DriftTogether.Coop
{
    /// <summary>Per-player statistics gathered during a co-op run.</summary>
    public sealed class PlayerRunStats
    {
        public ulong ClientId;
        public string Name = "";
        public int OverboardCount;
        public float WetSeconds;
        public int PushesGiven;
        public int OarStrokes;
        public float RudderSeconds;
        public int FishCaught;
        public int ZonesScouted;
        public int Gathered;
    }

    /// <summary>
    /// Shared co-op run statistics plus the meme nominations for the
    /// «отчёт о сплаве» (UC-15).
    /// </summary>
    public sealed class CoopRunStats
    {
        public float ElapsedSeconds;
        public RiverRoute ChosenRoute = RiverRoute.None;
        public int RaftCollisions;
        public int HullAtFinish;
        public int Capsizes;
        public int RaftLosses;
        public bool PortageUsed;
        public int ModulesBuilt;
        public readonly List<PlayerRunStats> Players = new List<PlayerRunStats>();

        public void ChooseRoute(RiverRoute route)
        {
            if (route != RiverRoute.None)
                ChosenRoute = route;
        }

        public string RouteDisplayName()
        {
            switch (ChosenRoute)
            {
                case RiverRoute.QuietChannel: return "Тихий канал";
                case RiverRoute.NoisyStream: return "Шумный ручей";
                default: return "—";
            }
        }

        public PlayerRunStats GetOrAdd(ulong clientId)
        {
            var found = Players.FirstOrDefault(p => p.ClientId == clientId);
            if (found == null)
            {
                found = new PlayerRunStats { ClientId = clientId, Name = $"Игрок {Players.Count + 1}" };
                Players.Add(found);
            }
            return found;
        }

        /// <summary>
        /// Nominations. Ties resolve to the earliest player in list order;
        /// a nomination is omitted when nobody qualifies.
        /// </summary>
        public List<(string title, string playerName)> Nominations()
        {
            var result = new List<(string, string)>();

            AddMax(result, "Главный по рулю", p => p.RudderSeconds);
            AddMax(result, "Мотор", p => p.OarStrokes);
            AddMax(result, "Морж", p => p.WetSeconds);
            AddMax(result, "Толкач", p => p.PushesGiven);
            AddMax(result, "Рыбак", p => p.FishCaught);
            AddMax(result, "Разведчик", p => p.ZonesScouted);
            AddMax(result, "Добытчик", p => p.Gathered);

            var dry = Players.Where(p => p.OverboardCount == 0).Select(p => p.Name).ToList();
            if (dry.Count > 0 && dry.Count < Players.Count)
                result.Add(("Сухарь", string.Join(", ", dry)));
            else if (dry.Count == Players.Count && Players.Count > 0)
                result.Add(("Сухари", "вся команда"));

            return result;
        }

        void AddMax(List<(string, string)> result, string title,
            System.Func<PlayerRunStats, float> metric)
        {
            PlayerRunStats best = null;
            float bestValue = 0f;
            foreach (var p in Players)
            {
                float v = metric(p);
                if (v > bestValue)
                {
                    bestValue = v;
                    best = p;
                }
            }
            if (best != null)
                result.Add((title, best.Name));
        }
    }
}
