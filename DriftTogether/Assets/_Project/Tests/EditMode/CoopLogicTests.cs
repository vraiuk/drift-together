using DriftTogether.Coop;
using DriftTogether.Core;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class CoopStatsTests
    {
        static CoopRunStats MakeCrew()
        {
            var stats = new CoopRunStats();
            var a = stats.GetOrAdd(1);
            var b = stats.GetOrAdd(2);
            var c = stats.GetOrAdd(3);
            a.RudderSeconds = 120f;
            b.OarStrokes = 40;
            b.OverboardCount = 2;
            b.WetSeconds = 80f;
            c.PushesGiven = 3;
            return stats;
        }

        [Test]
        public void NominationsPickMaxPerCategory()
        {
            var stats = MakeCrew();
            var noms = stats.Nominations();

            Assert.Contains(("Главный по рулю", "Игрок 1"), noms);
            Assert.Contains(("Мотор", "Игрок 2"), noms);
            Assert.Contains(("Морж", "Игрок 2"), noms);
            Assert.Contains(("Толкач", "Игрок 3"), noms);
        }

        [Test]
        public void DryNominationListsOnlyDryPlayers()
        {
            var stats = MakeCrew();
            var noms = stats.Nominations();
            Assert.Contains(("Сухарь", "Игрок 1, Игрок 3"), noms);
        }

        [Test]
        public void EmptyMetricsProduceNoNomination()
        {
            var stats = new CoopRunStats();
            stats.GetOrAdd(1);
            var noms = stats.Nominations();
            Assert.IsFalse(noms.Exists(n => n.title == "Мотор"));
            Assert.IsFalse(noms.Exists(n => n.title == "Морж"));
            // Единственный игрок, ни разу за бортом — «сухари всей командой».
            Assert.Contains(("Сухари", "вся команда"), noms);
        }

        [Test]
        public void PlayersGetStableNamesById()
        {
            var stats = new CoopRunStats();
            var first = stats.GetOrAdd(7);
            var again = stats.GetOrAdd(7);
            Assert.AreSame(first, again);
            Assert.AreEqual("Игрок 1", first.Name);
        }
    }

    public class PostSystemTests
    {
        [Test]
        public void OnePlayerPerPost()
        {
            var posts = new PostSystem();
            Assert.IsTrue(posts.TryOccupy(RaftPost.Rudder, 1));
            Assert.IsFalse(posts.TryOccupy(RaftPost.Rudder, 2), "post already taken");
            Assert.AreEqual((ulong?)1, posts.OccupantOf(RaftPost.Rudder));
        }

        [Test]
        public void SwitchingPostsReleasesThePreviousOne()
        {
            var posts = new PostSystem();
            posts.TryOccupy(RaftPost.OarLeft, 1);
            Assert.IsTrue(posts.TryOccupy(RaftPost.Rudder, 1));
            Assert.IsNull(posts.OccupantOf(RaftPost.OarLeft), "old post must free up");
            Assert.AreEqual(RaftPost.Rudder, posts.PostOf(1));
        }

        [Test]
        public void ReleaseAllOnOverboard()
        {
            var posts = new PostSystem();
            posts.TryOccupy(RaftPost.OarRight, 5);
            posts.ReleaseAll(5);
            Assert.IsNull(posts.OccupantOf(RaftPost.OarRight));
            Assert.AreEqual(RaftPost.None, posts.PostOf(5));
        }

        [Test]
        public void NonePostCannotBeOccupied()
        {
            var posts = new PostSystem();
            Assert.IsFalse(posts.TryOccupy(RaftPost.None, 1));
        }
    }

    public class WetStatusTests
    {
        [Test]
        public void SoakMakesWetAndSlow()
        {
            var wet = new WetStatus();
            Assert.IsFalse(wet.IsWet);
            wet.Soak();
            Assert.IsTrue(wet.IsWet);
            Assert.AreEqual(WetStatus.WetSpeedMultiplier, wet.SpeedMultiplier);
        }

        [Test]
        public void DriesInSixtySecondsAwayFromFire()
        {
            var wet = new WetStatus();
            wet.Soak();
            wet.Tick(59f, nearCampfire: false);
            Assert.IsTrue(wet.IsWet);
            wet.Tick(1.5f, nearCampfire: false);
            Assert.IsFalse(wet.IsWet);
            Assert.AreEqual(1f, wet.SpeedMultiplier);
        }

        [Test]
        public void CampfireDriesFourTimesFaster()
        {
            var wet = new WetStatus();
            wet.Soak();
            wet.Tick(15.1f, nearCampfire: true); // 15.1 * 4 > 60
            Assert.IsFalse(wet.IsWet);
        }

        [Test]
        public void ResoakResetsTimerAndAccumulatesTotal()
        {
            var wet = new WetStatus();
            wet.Soak();
            wet.Tick(30f, nearCampfire: false);
            wet.Soak();
            Assert.AreEqual(WetStatus.WetDuration, wet.Remaining);
            wet.Tick(10f, nearCampfire: false);
            Assert.AreEqual(40f, wet.TotalWetSeconds, 0.01f);
        }
    }

    public class HullIntegrityMaxPointsTests
    {
        [Test]
        public void CustomMaxPointsWorkForRaft()
        {
            var clock = new ManualClock();
            var hull = new HullIntegrity(clock, 5);
            Assert.AreEqual(5, hull.Current);
            Assert.AreEqual(5, hull.Max);

            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(hull.ApplyHit(), $"hit {i} must land");
                clock.Advance(HullIntegrity.InvulnerabilityDuration + 0.1f);
            }
            Assert.IsTrue(hull.IsBroken);

            hull.RestoreFull();
            Assert.AreEqual(5, hull.Current);
        }
    }
}
