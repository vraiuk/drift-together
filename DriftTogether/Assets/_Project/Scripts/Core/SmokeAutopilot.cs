using System.Collections.Generic;
using System.IO;
using DriftTogether.World;
using UnityEngine;

namespace DriftTogether.Core
{
    /// <summary>
    /// Drives the kayak along the river automatically. Used by the automated
    /// smoke test (Play Mode test and `--smoke` standalone run) to verify the
    /// level is honestly completable end to end.
    /// </summary>
    public sealed class SmokeAutopilot : MonoBehaviour
    {
        public static bool RequestedByTest;
        public RiverRoute PreferredRoute = RiverRoute.NoisyStream;
        public bool WriteReportAndQuit;
        public float TimeScale = 5f;

        GameFlow _flow;
        readonly List<Vector3> _waypoints = new List<Vector3>();
        int _waypointIndex;
        float _startedRealtime;
        bool _done;

        public bool ReachedFinish { get; private set; }

        public static bool CommandLineRequested()
        {
            foreach (string arg in System.Environment.GetCommandLineArgs())
                if (arg == "--smoke" || arg == "-smoke")
                    return true;
            return false;
        }

        void Start()
        {
            _flow = GetComponent<GameFlow>();
            _startedRealtime = Time.realtimeSinceStartup;
            Time.timeScale = TimeScale;
            BuildWaypoints();
            if (_flow != null && _flow.Kayak != null)
                _flow.Kayak.Input.AutopilotEnabled = true;
        }

        void BuildWaypoints()
        {
            var level = _flow.Level;
            AddSpline(level.UpperSpline, 0f, level.UpperSpline.Length - 12f);
            RiverSpline branch = PreferredRoute == RiverRoute.QuietChannel
                ? level.QuietSpline
                : level.NoisySpline;
            AddSpline(branch, 10f, branch.Length - 10f);
            AddSpline(level.LowerSpline, 6f, level.LowerSpline.Length - 25f);
        }

        void AddSpline(RiverSpline spline, float from, float to)
        {
            for (float d = from; d < to; d += 7f)
                _waypoints.Add(spline.PointAtDistance(d));
        }

        void Update()
        {
            if (_done || _flow == null || _flow.Kayak == null)
                return;

            var kayak = _flow.Kayak;
            var input = kayak.Input;
            input.AutopilotEnabled = true;

            if (_flow.CurrentState == GameFlow.State.Finished)
            {
                ReachedFinish = true;
                Finish(success: true);
                return;
            }

            // Give up after 12 real minutes — the report will show the failure.
            if (Time.realtimeSinceStartup - _startedRealtime > 720f)
            {
                Finish(success: false);
                return;
            }

            // Campfire: stop and interact once.
            var campfire = _flow.Level.Campfire;
            if (campfire != null && campfire.PlayerInRange && !campfire.HasRested)
            {
                input.AutopilotThrust = -0.4f;
                input.AutopilotSteer = 0f;
                input.AutopilotInteract = true;
                return;
            }

            // Advance the waypoint pointer.
            Vector3 pos = kayak.transform.position;
            while (_waypointIndex < _waypoints.Count - 1)
            {
                Vector3 wp = _waypoints[_waypointIndex];
                Vector3 flat = wp - pos;
                flat.y = 0f;
                Vector3 fwd = kayak.transform.forward;
                fwd.y = 0f;
                if (flat.magnitude < 6f || Vector3.Dot(flat.normalized, fwd.normalized) < -0.15f)
                    _waypointIndex++;
                else
                    break;
            }

            Vector3 target = _waypoints[Mathf.Min(_waypointIndex + 1, _waypoints.Count - 1)];
            Vector3 to = target - pos;
            to.y = 0f;
            float angle = Vector3.SignedAngle(kayak.transform.forward, to, Vector3.up);
            input.AutopilotSteer = Mathf.Clamp(angle / 32f, -1f, 1f);
            input.AutopilotThrust = Mathf.Abs(angle) > 55f ? 0.25f : 1f;
        }

        void Finish(bool success)
        {
            _done = true;
            Time.timeScale = 1f;

            if (!WriteReportAndQuit)
                return;

            var stats = _flow.Stats;
            string json =
                "{\n" +
                $"  \"success\": {(success ? "true" : "false")},\n" +
                $"  \"elapsedGameSeconds\": {stats.ElapsedSeconds:F1},\n" +
                $"  \"route\": \"{stats.ChosenRoute}\",\n" +
                $"  \"mushrooms\": {stats.Mushrooms},\n" +
                $"  \"collisions\": {stats.Collisions},\n" +
                $"  \"respawns\": {stats.Respawns},\n" +
                $"  \"realSeconds\": {Time.realtimeSinceStartup - _startedRealtime:F1}\n" +
                "}\n";
            string path = Path.Combine(Application.persistentDataPath, "smoke_result.json");
            File.WriteAllText(path, json);
            Debug.Log($"[Smoke] result written to {path}: {json}");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(success ? 0 : 1);
#endif
        }
    }
}
