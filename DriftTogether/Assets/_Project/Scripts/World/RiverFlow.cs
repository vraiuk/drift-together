using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>One branch of the river with its own current strength.</summary>
    public sealed class FlowBranch
    {
        public RiverSpline Spline;
        public float BaseCurrent = 1.2f;
        public float HalfWidth = 7f;
        /// <summary>Extra current zones: (startDistance, endDistance, multiplier).</summary>
        public readonly List<(float start, float end, float multiplier)> FastZones = new();
    }

    /// <summary>
    /// Samples the river current at any world position: direction follows the
    /// closest branch centerline, magnitude depends on branch and fast zones.
    /// Also pushes the kayak gently back toward the channel near the banks.
    /// </summary>
    public sealed class RiverFlow
    {
        readonly List<FlowBranch> _branches = new();

        public void AddBranch(FlowBranch branch) => _branches.Add(branch);

        public FlowBranch ClosestBranch(Vector3 position, out int sampleIndex)
        {
            FlowBranch best = null;
            sampleIndex = 0;
            float bestDist = float.MaxValue;
            foreach (var b in _branches)
            {
                int idx = b.Spline.ClosestSampleIndex(position);
                Vector3 d = b.Spline.PointAt(idx) - position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = b;
                    sampleIndex = idx;
                }
            }
            return best;
        }

        public Vector3 CurrentAt(Vector3 position)
        {
            var branch = ClosestBranch(position, out int idx);
            if (branch == null)
                return Vector3.zero;

            Vector3 tangent = branch.Spline.TangentAt(idx);
            float distanceAlong = branch.Spline.DistanceAt(idx);

            float strength = branch.BaseCurrent;
            foreach (var zone in branch.FastZones)
            {
                if (distanceAlong >= zone.start && distanceAlong <= zone.end)
                    strength *= zone.multiplier;
            }

            // Gentle centering push when hugging the banks.
            Vector3 toCenter = branch.Spline.PointAt(idx) - position;
            toCenter.y = 0f;
            float off = toCenter.magnitude;
            Vector3 centering = Vector3.zero;
            if (off > branch.HalfWidth * 0.55f)
                centering = toCenter.normalized * (off - branch.HalfWidth * 0.55f) * 0.35f;

            return tangent * strength + centering;
        }

        /// <summary>Water surface height (flat river with a tiny travelling wave).</summary>
        public static float WaterHeightAt(Vector3 position, float time)
        {
            return 0f + Mathf.Sin(position.x * 0.6f + time * 1.4f) * 0.045f
                      + Mathf.Sin(position.z * 0.45f + time * 1.1f) * 0.055f;
        }
    }
}
