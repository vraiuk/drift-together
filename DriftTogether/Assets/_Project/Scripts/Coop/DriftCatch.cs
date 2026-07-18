using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.Coop
{
    /// <summary>
    /// UC-09: «река всегда возвращает плот». An abandoned raft drifts with the
    /// current and is guaranteed to snag on rocks a bit downstream — a soft
    /// lock is impossible by construction. Pure logic.
    /// </summary>
    public static class DriftCatch
    {
        /// <summary>Guaranteed snag lines (world Z), spaced ≤ 130 m apart.</summary>
        public static readonly float[] CatchLinesZ =
        {
            70f, 160f, 246f, 350f, 470f, 545f, 645f, 735f
        };

        public const float MaxGap = 130f;
        public const float MinDriftBeforeCatch = 12f;

        /// <summary>
        /// The Z line where a raft that went adrift at <paramref name="driftStartZ"/>
        /// will snag. Always exists and is always downstream.
        /// </summary>
        public static float SnagLineFor(float driftStartZ)
        {
            foreach (float line in CatchLinesZ)
                if (line >= driftStartZ + MinDriftBeforeCatch)
                    return line;
            // За последней линией финиш совсем рядом — цепляем у него.
            return CatchLinesZ[CatchLinesZ.Length - 1];
        }
    }

    /// <summary>
    /// Detects an abandoned raft: nobody aboard, anchor not holding, not
    /// capsized — sustained for a grace period.
    /// </summary>
    public sealed class AdriftRule
    {
        public const float GraceSeconds = 5f;

        float _abandonedFor;
        public bool IsAdrift { get; private set; }

        /// <summary>Returns true on the frame the raft becomes adrift.</summary>
        public bool Tick(float dt, bool anyoneAboard, bool anchorHolding, bool capsized)
        {
            bool abandoned = !anyoneAboard && !anchorHolding && !capsized;
            if (!abandoned)
            {
                _abandonedFor = 0f;
                IsAdrift = false;
                return false;
            }

            _abandonedFor += dt;
            if (!IsAdrift && _abandonedFor >= GraceSeconds)
            {
                IsAdrift = true;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _abandonedFor = 0f;
            IsAdrift = false;
        }
    }
}
