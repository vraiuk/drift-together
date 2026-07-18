using System.Collections;
using DriftTogether.Coop;
using DriftTogether.Coop.Net;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DriftTogether.Tests
{
    /// <summary>
    /// Network-level checks on a loopback host: the co-op scene must spawn
    /// the raft and the host avatar, mirror hull state, and honour posts.
    /// </summary>
    public class CoopNetworkTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator HostLocalSpawnsRaftAndAvatar()
        {
            var session = SessionManager.Ensure();
            Assert.IsTrue(session.HostLocal(), "loopback host must start");
            CoopBootstrap.CoopRequested = true;

            NetworkManager.Singleton.SceneManager.LoadScene("River", LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + 30f;
            RaftController raft = null;
            PlayerAvatar avatar = null;
            while ((raft == null || avatar == null) && Time.realtimeSinceStartup < deadline)
            {
                raft = Object.FindFirstObjectByType<RaftController>();
                avatar = Object.FindFirstObjectByType<PlayerAvatar>();
                yield return null;
            }

            Assert.IsNotNull(raft, "raft must spawn on the host");
            Assert.IsNotNull(avatar, "host avatar must spawn");
            Assert.AreEqual(RaftController.MaxHull, raft.Hull.Value, "hull starts full");
            Assert.IsTrue(avatar.IsOwner, "host owns its avatar");

            // Posts: occupancy rules work through the host-side system.
            Assert.IsTrue(raft.Posts.TryOccupy(RaftPost.Rudder, avatar.OwnerClientId));
            Assert.IsFalse(raft.Posts.TryOccupy(RaftPost.Rudder, 999),
                "occupied post must reject another client");
            raft.Posts.ReleaseAll(avatar.OwnerClientId);
        }

        [TearDown]
        public void Cleanup()
        {
            CoopBootstrap.CoopRequested = false;
            CoopSmokePilot.RequestedByTest = false;
            CoopSmokePilot.ReportShown = false;
            if (SessionManager.Instance != null)
                SessionManager.Instance.Shutdown();
            Time.timeScale = 1f;
        }
    }

    /// <summary>Full co-op smoke: loopback host raft autopilot start → finish → report.</summary>
    public class CoopSmokeTests
    {
        [UnityTest]
        [Timeout(900000)]
        public IEnumerator CoopPlaythroughReachesReport()
        {
            CoopSmokePilot.RequestedByTest = true;
            CoopSmokePilot.ReportShown = false;

            var session = SessionManager.Ensure();
            Assert.IsTrue(session.HostLocal(), "loopback host must start");
            CoopBootstrap.CoopRequested = true;
            NetworkManager.Singleton.SceneManager.LoadScene("River", LoadSceneMode.Single);

            float deadline = Time.realtimeSinceStartup + 840f;
            while (!CoopSmokePilot.ReportShown && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(CoopSmokePilot.ReportShown, "autopilot raft must reach the finish report");
            Assert.IsNotNull(CoopBootstrap.Stats, "host stats must exist");
            Assert.AreNotEqual(Core.RiverRoute.None, CoopBootstrap.Stats.ChosenRoute,
                "route choice must be recorded");
            Assert.IsTrue(CoopBootstrap.SavedCheckpoint.HasValue,
                "campfire/checkpoint must be recorded during the run");
        }

        [TearDown]
        public void Cleanup()
        {
            CoopBootstrap.CoopRequested = false;
            CoopBootstrap.StartFromCheckpoint = false;
            CoopBootstrap.SavedCheckpoint = null;
            CoopSmokePilot.RequestedByTest = false;
            CoopSmokePilot.ReportShown = false;
            if (SessionManager.Instance != null)
                SessionManager.Instance.Shutdown();
            Time.timeScale = 1f;
        }
    }
}
