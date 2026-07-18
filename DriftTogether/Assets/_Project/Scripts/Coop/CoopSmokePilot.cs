using System.Collections.Generic;
using System.IO;
using DriftTogether.Core;
using DriftTogether.World;
using UnityEngine;

namespace DriftTogether.Coop
{
    /// <summary>
    /// Host-side autopilot for the co-op smoke test: steers the raft down the
    /// river (noisy stream), rests at the campfire pier, reaches the finish
    /// and reports. Enabled by `--smoke-coop` or by the Play Mode test.
    /// </summary>
    public sealed class CoopSmokePilot : MonoBehaviour
    {
        public static bool RequestedByTest;
        public static bool ReportShown;

        public bool WriteReportAndQuit;

        readonly List<Vector3> _waypoints = new List<Vector3>();
        int _waypointIndex;
        float _startedRealtime;
        float _nextProgressLog;
        bool _done;
        bool _restedRequested;

        void Start()
        {
            _startedRealtime = Time.realtimeSinceStartup;
            Time.timeScale = 5f;

            var level = GetComponent<LevelBuilder>();
            AddSpline(level.UpperSpline, 0f, level.UpperSpline.Length - 12f);
            AddSpline(level.NoisySpline, 10f, level.NoisySpline.Length - 10f);
            AddSpline(level.LowerSpline, 6f, level.LowerSpline.Length - 25f);
        }

        void AddSpline(RiverSpline spline, float from, float to)
        {
            for (float d = from; d < to; d += 7f)
                _waypoints.Add(spline.PointAtDistance(d));
        }

        void Update()
        {
            if (_done)
                return;
            var raft = CoopBootstrap.Raft;
            if (raft == null)
                return;

            if (Time.realtimeSinceStartup >= _nextProgressLog)
            {
                _nextProgressLog = Time.realtimeSinceStartup + 15f;
                Debug.Log($"[SmokeCoop] pos={raft.transform.position} wp={_waypointIndex}/{_waypoints.Count} " +
                          $"hull={raft.Hull.Value} vel={raft.GetComponent<Rigidbody>().linearVelocity.magnitude:F1}");
            }

            if (ReportShown)
            {
                Finish(success: true);
                return;
            }
            if (Time.realtimeSinceStartup - _startedRealtime > 720f)
            {
                Finish(success: false);
                return;
            }

            // Mandatory campfire stop once the pier zone reports the raft.
            var campfire = CoopBootstrap.Active != null ? CampfireOf() : null;
            if (campfire != null && campfire.PlayerInRange && !campfire.HasRested)
            {
                raft.AutoThrust = 0.1f;
                raft.AutoSteer = 0f;
                if (!_restedRequested)
                {
                    _restedRequested = true;
                    CoopBootstrap.HostCampfireRest();
                }
                return;
            }

            // Stuck recovery: full thrust but no движение — back off for a moment.
            var body = raft.GetComponent<Rigidbody>();
            if (_reverseUntil > Time.time)
            {
                raft.AutoThrust = -0.8f;
                raft.AutoSteer = _reverseSteer;
                return;
            }
            if (body.linearVelocity.magnitude < 0.3f && Mathf.Abs(raft.AutoThrust) > 0.5f)
            {
                _stuckSeconds += Time.deltaTime;
                if (_stuckSeconds > 4f)
                {
                    _stuckSeconds = 0f;
                    _reverseUntil = Time.time + 2.5f;
                    _reverseSteer = Random.value > 0.5f ? 1f : -1f;
                    return;
                }
            }
            else
            {
                _stuckSeconds = 0f;
            }

            Vector3 pos = raft.transform.position;
            while (_waypointIndex < _waypoints.Count - 1)
            {
                Vector3 flat = _waypoints[_waypointIndex] - pos;
                flat.y = 0f;
                Vector3 fwd = raft.transform.forward;
                fwd.y = 0f;
                // Advance only near the waypoint: a spun raft must not eat the list.
                bool close = flat.magnitude < 7f;
                bool passed = flat.magnitude < 14f &&
                              Vector3.Dot(flat.normalized, fwd.normalized) < -0.15f;
                if (close || passed)
                    _waypointIndex++;
                else
                    break;
            }

            Vector3 target = _waypoints[Mathf.Min(_waypointIndex + 1, _waypoints.Count - 1)];
            Vector3 to = target - pos;
            to.y = 0f;
            float angle = Vector3.SignedAngle(raft.transform.forward, to, Vector3.up);
            raft.AutoSteer = Mathf.Clamp(angle / 30f, -1f, 1f);
            raft.AutoThrust = Mathf.Abs(angle) > 60f ? 0.35f : 1f;
        }

        float _stuckSeconds;
        float _reverseUntil;
        float _reverseSteer;

        Campfire CampfireOf()
        {
            var level = GetComponent<LevelBuilder>();
            return level != null ? level.Campfire : null;
        }

        void Finish(bool success)
        {
            _done = true;
            Time.timeScale = 1f;
            var raft = CoopBootstrap.Raft;
            if (raft != null)
            {
                raft.AutoThrust = 0f;
                raft.AutoSteer = 0f;
            }

            if (!WriteReportAndQuit)
                return;

            var stats = CoopBootstrap.Stats;
            string json =
                "{\n" +
                $"  \"success\": {(success ? "true" : "false")},\n" +
                "  \"mode\": \"coop\",\n" +
                $"  \"elapsedGameSeconds\": {(stats?.ElapsedSeconds ?? 0f):F1},\n" +
                $"  \"route\": \"{stats?.ChosenRoute}\",\n" +
                $"  \"raftCollisions\": {stats?.RaftCollisions ?? 0},\n" +
                $"  \"hullAtFinish\": {stats?.HullAtFinish ?? 0},\n" +
                $"  \"realSeconds\": {Time.realtimeSinceStartup - _startedRealtime:F1}\n" +
                "}\n";
            string path = Path.Combine(Application.persistentDataPath, "smoke_result.json");
            File.WriteAllText(path, json);
            Debug.Log($"[SmokeCoop] result written to {path}: {json}");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(success ? 0 : 1);
#endif
        }
    }
}
