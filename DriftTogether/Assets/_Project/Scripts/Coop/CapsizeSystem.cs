using System;
using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.Coop
{
    /// <summary>
    /// Raft balance and capsizing (UC-08). Pure logic, host-driven:
    /// crew standing on one side plus hard hits build up tilt; past the
    /// threshold the raft flips. Righting is a joint effort.
    /// </summary>
    public sealed class CapsizeSystem
    {
        public const float CapsizeThreshold = 1f;
        public const float ImbalanceRate = 0.16f;   // tilt/sec at full one-side imbalance
        public const float RecoveryRate = 0.22f;    // tilt/sec back to level
        public const float HitKick = 0.18f;
        public const float RightingUnitsNeeded = 4f;
        public const float FoodLossFraction = 0.4f;

        public float Tilt { get; private set; }         // -1..1, sign = side
        public bool Capsized { get; private set; }
        public float RightingProgress { get; private set; }

        /// <summary>Advance balance. crewLocalX — local X of every aboard player (right = +).</summary>
        public void Tick(float dt, IReadOnlyList<float> crewLocalX, bool inFastWater)
        {
            if (Capsized)
                return;

            float imbalance = 0f;
            if (crewLocalX != null && crewLocalX.Count > 0)
            {
                float sum = 0f;
                foreach (float x in crewLocalX)
                    sum += Mathf.Clamp(x / 1.7f, -1f, 1f);
                imbalance = sum / crewLocalX.Count;
                // Full crew on one rail is far worse than a lone wanderer.
                imbalance *= Mathf.Min(1f, 0.45f + 0.28f * crewLocalX.Count);
            }

            float rate = ImbalanceRate * (inFastWater ? 1.6f : 1f);
            Tilt += imbalance * rate * dt;

            // Natural recovery toward level, weaker while the crew keeps leaning.
            float recovery = RecoveryRate * (1f - Mathf.Abs(imbalance) * 0.7f) * dt;
            if (Mathf.Abs(Tilt) <= recovery && Mathf.Abs(imbalance) < 0.05f)
                Tilt = 0f;
            else
                Tilt -= Mathf.Sign(Tilt) * Mathf.Min(recovery, Mathf.Abs(Tilt));

            Tilt = Mathf.Clamp(Tilt, -1.2f, 1.2f);
            if (Mathf.Abs(Tilt) >= CapsizeThreshold)
                Capsize();
        }

        /// <summary>A hard hull hit kicks the raft toward the given side (+1/-1).</summary>
        public void ApplyHitKick(float side)
        {
            if (Capsized)
                return;
            Tilt += Mathf.Sign(side == 0f ? 1f : side) * HitKick;
            if (Mathf.Abs(Tilt) >= CapsizeThreshold)
                Capsize();
        }

        void Capsize()
        {
            Capsized = true;
            RightingProgress = 0f;
        }

        /// <summary>For tests and scripted events.</summary>
        public void ForceCapsize() => Capsize();

        /// <summary>How much food goes overboard when the raft flips.</summary>
        public static int FoodLost(int food) =>
            Mathf.CeilToInt(food * FoodLossFraction);

        /// <summary>One righting action (a player pulling at the raft in the water).</summary>
        public void AddRightingEffort(float units = 1f)
        {
            if (!Capsized)
                return;
            RightingProgress += units;
            if (RightingProgress >= RightingUnitsNeeded)
            {
                Capsized = false;
                Tilt = 0f;
                RightingProgress = 0f;
            }
        }
    }
}
