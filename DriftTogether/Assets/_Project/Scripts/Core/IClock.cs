namespace DriftTogether.Core
{
    /// <summary>Abstracts time so gameplay rules can be unit-tested without Play Mode.</summary>
    public interface IClock
    {
        float Now { get; }
    }

    public sealed class GameClock : IClock
    {
        public float Now => UnityEngine.Time.time;
    }

    public sealed class ManualClock : IClock
    {
        public float Now { get; set; }

        public void Advance(float seconds) => Now += seconds;
    }
}
