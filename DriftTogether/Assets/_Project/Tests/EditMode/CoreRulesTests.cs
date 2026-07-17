using DriftTogether.Core;
using NUnit.Framework;
using UnityEngine;

namespace DriftTogether.Tests
{
    public class HullIntegrityTests
    {
        [Test]
        public void StartsAtMaxAndLosesOnePointPerHit()
        {
            var clock = new ManualClock();
            var hull = new HullIntegrity(clock);

            Assert.AreEqual(HullIntegrity.MaxPoints, hull.Current);
            Assert.IsTrue(hull.ApplyHit());
            Assert.AreEqual(HullIntegrity.MaxPoints - 1, hull.Current);
        }

        [Test]
        public void InvulnerabilityWindowBlocksImmediateSecondHit()
        {
            var clock = new ManualClock();
            var hull = new HullIntegrity(clock);

            Assert.IsTrue(hull.ApplyHit());
            clock.Advance(HullIntegrity.InvulnerabilityDuration * 0.5f);
            Assert.IsFalse(hull.ApplyHit(), "hit inside the invulnerability window must be ignored");
            Assert.AreEqual(HullIntegrity.MaxPoints - 1, hull.Current);

            clock.Advance(HullIntegrity.InvulnerabilityDuration);
            Assert.IsTrue(hull.ApplyHit(), "hit after the window must count");
            Assert.AreEqual(HullIntegrity.MaxPoints - 2, hull.Current);
        }

        [Test]
        public void BreaksAtZeroAndRestoresToFull()
        {
            var clock = new ManualClock();
            var hull = new HullIntegrity(clock);
            bool broke = false;
            hull.Broken += () => broke = true;

            for (int i = 0; i < HullIntegrity.MaxPoints; i++)
            {
                Assert.IsTrue(hull.ApplyHit());
                clock.Advance(HullIntegrity.InvulnerabilityDuration + 0.1f);
            }

            Assert.IsTrue(hull.IsBroken);
            Assert.IsTrue(broke);
            Assert.IsFalse(hull.ApplyHit(), "a broken hull cannot take further hits");

            hull.RestoreFull();
            Assert.AreEqual(HullIntegrity.MaxPoints, hull.Current);
            Assert.IsFalse(hull.IsBroken);
        }
    }

    public class CheckpointSystemTests
    {
        [Test]
        public void RespawnUsesTheLatestCheckpoint()
        {
            var checkpoints = new CheckpointSystem();
            Assert.IsFalse(checkpoints.HasCheckpoint);

            var first = new Vector3(0f, 0f, 10f);
            var second = new Vector3(5f, 0f, 120f);
            checkpoints.Set(first, Quaternion.identity);
            Assert.IsTrue(checkpoints.HasCheckpoint);
            Assert.AreEqual(first, checkpoints.Position);

            var rot = Quaternion.Euler(0f, 45f, 0f);
            checkpoints.Set(second, rot);
            Assert.AreEqual(second, checkpoints.Position);
            Assert.AreEqual(rot, checkpoints.Rotation);
        }
    }

    public class MushroomTrackerTests
    {
        [Test]
        public void CountsEachMushroomOnlyOnce()
        {
            var tracker = new MushroomTracker();

            Assert.IsTrue(tracker.Collect(1));
            Assert.IsTrue(tracker.Collect(2));
            Assert.IsFalse(tracker.Collect(1), "the same mushroom must not count twice");
            Assert.AreEqual(2, tracker.Count);
            Assert.IsTrue(tracker.IsCollected(1));
            Assert.IsFalse(tracker.IsCollected(3));
        }

        [Test]
        public void SurvivesRespawnScenario()
        {
            // After death/respawn collected ids stay collected — no re-pickup.
            var tracker = new MushroomTracker();
            tracker.Collect(4);
            tracker.Collect(5);

            // Simulated respawn: the world objects were destroyed on pickup, but
            // even if a stale trigger fired again, the count must not change.
            Assert.IsFalse(tracker.Collect(4));
            Assert.AreEqual(2, tracker.Count);
        }
    }

    public class RunStatsTests
    {
        [Test]
        public void RouteChoiceIsStoredForResults()
        {
            var stats = new RunStats();
            Assert.AreEqual(RiverRoute.None, stats.ChosenRoute);

            stats.ChooseRoute(RiverRoute.QuietChannel);
            Assert.AreEqual(RiverRoute.QuietChannel, stats.ChosenRoute);
            Assert.AreEqual("Тихий канал", stats.RouteDisplayName());

            stats.ChooseRoute(RiverRoute.None);
            Assert.AreEqual(RiverRoute.QuietChannel, stats.ChosenRoute,
                "None must never overwrite a real choice");
        }
    }

    public class DialogueQueueTests
    {
        [Test]
        public void CategoryCooldownPreventsSpam()
        {
            var clock = new ManualClock();
            var queue = new DialogueQueue(clock);
            var lines = new[] { "line-a", "line-b" };

            Assert.IsTrue(queue.TryEnqueue(LineCategory.Collision, lines, cooldown: 10f));
            Assert.IsTrue(queue.TryDequeue(out string first));
            Assert.AreEqual("line-a", first);

            Assert.IsFalse(queue.TryEnqueue(LineCategory.Collision, lines, cooldown: 10f),
                "cooldown must reject a repeat");

            clock.Advance(11f);
            Assert.IsTrue(queue.TryEnqueue(LineCategory.Collision, lines, cooldown: 10f));
            Assert.IsTrue(queue.TryDequeue(out string second));
            Assert.AreEqual("line-b", second, "variants must rotate");
        }
    }
}
