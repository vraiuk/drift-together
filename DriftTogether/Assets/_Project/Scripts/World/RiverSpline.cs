using System.Collections.Generic;
using UnityEngine;

namespace DriftTogether.World
{
    /// <summary>
    /// Catmull-Rom centerline of a river branch, densely sampled for cheap
    /// closest-point and tangent queries.
    /// </summary>
    public sealed class RiverSpline
    {
        public readonly List<Vector3> ControlPoints;
        readonly List<Vector3> _samples = new List<Vector3>();
        readonly List<float> _sampleDistances = new List<float>();

        public float Length { get; private set; }
        public IReadOnlyList<Vector3> Samples => _samples;

        public RiverSpline(IEnumerable<Vector3> controlPoints, int samplesPerSegment = 12)
        {
            ControlPoints = new List<Vector3>(controlPoints);
            BuildSamples(samplesPerSegment);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void BuildSamples(int samplesPerSegment)
        {
            _samples.Clear();
            _sampleDistances.Clear();
            var pts = ControlPoints;
            if (pts.Count < 2)
                return;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
                Vector3 p1 = pts[i];
                Vector3 p2 = pts[i + 1];
                Vector3 p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
                int steps = i == pts.Count - 2 ? samplesPerSegment + 1 : samplesPerSegment;
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / samplesPerSegment;
                    _samples.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            Length = 0f;
            _sampleDistances.Add(0f);
            for (int i = 1; i < _samples.Count; i++)
            {
                Length += Vector3.Distance(_samples[i - 1], _samples[i]);
                _sampleDistances.Add(Length);
            }
        }

        public int ClosestSampleIndex(Vector3 position)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _samples.Count; i++)
            {
                Vector3 d = _samples[i] - position;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        public Vector3 PointAt(int sampleIndex) =>
            _samples[Mathf.Clamp(sampleIndex, 0, _samples.Count - 1)];

        /// <summary>Distance along the spline for a sample index.</summary>
        public float DistanceAt(int sampleIndex) =>
            _sampleDistances[Mathf.Clamp(sampleIndex, 0, _sampleDistances.Count - 1)];

        public Vector3 TangentAt(int sampleIndex)
        {
            int i0 = Mathf.Clamp(sampleIndex, 0, _samples.Count - 2);
            Vector3 t = _samples[i0 + 1] - _samples[i0];
            t.y = 0f;
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        /// <summary>Horizontal distance from the centerline at the closest point.</summary>
        public float DistanceFromCenter(Vector3 position)
        {
            int idx = ClosestSampleIndex(position);
            Vector3 d = _samples[idx] - position;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Interpolated point at a given distance along the spline.</summary>
        public Vector3 PointAtDistance(float distance)
        {
            if (_samples.Count == 0)
                return Vector3.zero;
            distance = Mathf.Clamp(distance, 0f, Length);
            for (int i = 1; i < _sampleDistances.Count; i++)
            {
                if (_sampleDistances[i] >= distance)
                {
                    float segLen = _sampleDistances[i] - _sampleDistances[i - 1];
                    float t = segLen > 1e-5f ? (distance - _sampleDistances[i - 1]) / segLen : 0f;
                    return Vector3.Lerp(_samples[i - 1], _samples[i], t);
                }
            }
            return _samples[_samples.Count - 1];
        }
    }
}
