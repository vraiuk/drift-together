namespace DriftTogether.Core
{
    public enum RiverRoute
    {
        None,
        QuietChannel,
        NoisyStream
    }

    /// <summary>Per-run statistics shown on the results screen.</summary>
    public sealed class RunStats
    {
        public float ElapsedSeconds;
        public RiverRoute ChosenRoute = RiverRoute.None;
        public int Mushrooms;
        public int Collisions;
        public int Respawns;

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
    }
}
