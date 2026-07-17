using UnityEngine;

namespace DriftTogether.Coop
{
    /// <summary>
    /// «Мокрый» статус (UC-05): after a swim the player walks slower for a
    /// while; drying is much faster near a campfire. Pure logic.
    /// </summary>
    public sealed class WetStatus
    {
        public const float WetDuration = 60f;
        public const float CampfireDryMultiplier = 4f;
        public const float WetSpeedMultiplier = 0.8f;

        public float Remaining { get; private set; }
        public bool IsWet => Remaining > 0f;
        public float SpeedMultiplier => IsWet ? WetSpeedMultiplier : 1f;

        /// <summary>Total time spent wet, for the «Морж» nomination.</summary>
        public float TotalWetSeconds { get; private set; }

        public void Soak() => Remaining = WetDuration;

        public void Tick(float dt, bool nearCampfire)
        {
            if (!IsWet || dt <= 0f)
                return;
            TotalWetSeconds += dt;
            Remaining = Mathf.Max(0f,
                Remaining - dt * (nearCampfire ? CampfireDryMultiplier : 1f));
        }
    }
}
