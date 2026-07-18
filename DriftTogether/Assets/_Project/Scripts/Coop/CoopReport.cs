using Unity.Collections;
using Unity.Netcode;

namespace DriftTogether.Coop
{
    /// <summary>One player's row in the end-of-run report (network-serializable).</summary>
    public struct PlayerReportRow : INetworkSerializable
    {
        public FixedString64Bytes Name;
        public int OverboardCount;
        public float WetSeconds;
        public int PushesGiven;
        public int OarStrokes;
        public float RudderSeconds;
        public int FishCaught;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref OverboardCount);
            serializer.SerializeValue(ref WetSeconds);
            serializer.SerializeValue(ref PushesGiven);
            serializer.SerializeValue(ref OarStrokes);
            serializer.SerializeValue(ref RudderSeconds);
            serializer.SerializeValue(ref FishCaught);
        }
    }

    /// <summary>Full «отчёт о сплаве» payload broadcast by the host on finish.</summary>
    public struct CoopReportPayload : INetworkSerializable
    {
        public float ElapsedSeconds;
        public int Route;               // (int)RiverRoute
        public int RaftCollisions;
        public int HullAtFinish;
        public PlayerReportRow[] Players;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ElapsedSeconds);
            serializer.SerializeValue(ref Route);
            serializer.SerializeValue(ref RaftCollisions);
            serializer.SerializeValue(ref HullAtFinish);

            int count = Players?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                Players = new PlayerReportRow[count];
            for (int i = 0; i < count; i++)
                serializer.SerializeValue(ref Players[i]);
        }

        public static CoopReportPayload From(CoopRunStats stats)
        {
            var payload = new CoopReportPayload
            {
                ElapsedSeconds = stats.ElapsedSeconds,
                Route = (int)stats.ChosenRoute,
                RaftCollisions = stats.RaftCollisions,
                HullAtFinish = stats.HullAtFinish,
                Players = new PlayerReportRow[stats.Players.Count]
            };
            for (int i = 0; i < stats.Players.Count; i++)
            {
                var p = stats.Players[i];
                payload.Players[i] = new PlayerReportRow
                {
                    Name = p.Name,
                    OverboardCount = p.OverboardCount,
                    WetSeconds = p.WetSeconds,
                    PushesGiven = p.PushesGiven,
                    OarStrokes = p.OarStrokes,
                    RudderSeconds = p.RudderSeconds,
                    FishCaught = p.FishCaught
                };
            }
            return payload;
        }

        /// <summary>Rebuilds stats locally so nominations can be computed on every client.</summary>
        public CoopRunStats ToStats()
        {
            var stats = new CoopRunStats
            {
                ElapsedSeconds = ElapsedSeconds,
                ChosenRoute = (Core.RiverRoute)Route,
                RaftCollisions = RaftCollisions,
                HullAtFinish = HullAtFinish
            };
            for (int i = 0; i < (Players?.Length ?? 0); i++)
            {
                var row = Players[i];
                var p = stats.GetOrAdd((ulong)(i + 1));
                p.Name = row.Name.ToString();
                p.OverboardCount = row.OverboardCount;
                p.WetSeconds = row.WetSeconds;
                p.PushesGiven = row.PushesGiven;
                p.OarStrokes = row.OarStrokes;
                p.RudderSeconds = row.RudderSeconds;
                p.FishCaught = row.FishCaught;
            }
            return stats;
        }
    }
}
