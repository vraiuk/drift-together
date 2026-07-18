using DriftTogether.Coop;
using DriftTogether.Core;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class FishingSessionTests
    {
        static FishingSession NewSession(ManualClock clock, int seed) =>
            new FishingSession(clock, seed);

        [Test]
        public void CalmSpotBitesWithinTenSeconds()
        {
            var clock = new ManualClock();
            var session = NewSession(clock, 1);
            session.Cast(calmSpot: true);
            Assert.AreEqual(FishingState.Waiting, session.State);

            clock.Advance(10.6f); // calm: 2.5..8.5 c
            session.Tick();
            Assert.AreEqual(FishingState.Bite, session.State);
        }

        [Test]
        public void MissingTheBiteWindowLosesTheFish()
        {
            var clock = new ManualClock();
            var session = NewSession(clock, 2);
            session.Cast(calmSpot: true);
            clock.Advance(10.6f);
            session.Tick();
            Assert.AreEqual(FishingState.Bite, session.State);

            clock.Advance(FishingSession.BiteWindow + 0.1f);
            session.Tick();
            Assert.AreEqual(FishingState.Escaped, session.State);
            Assert.IsTrue(session.IsTerminal);
        }

        [Test]
        public void SmallFishLandsOnHook()
        {
            // Seeds are deterministic: find one that rolls a small fish.
            for (int seed = 0; seed < 50; seed++)
            {
                var clock = new ManualClock();
                var session = NewSession(clock, seed);
                session.Cast(calmSpot: true);
                clock.Advance(10.6f);
                session.Tick();
                session.Hook();
                if (session.State == FishingState.Landed)
                {
                    Assert.AreEqual(FishingSession.SmallFishFood, session.FoodResult);
                    return;
                }
                Assert.AreEqual(FishingState.Struggle, session.State,
                    "hook on bite must land or start a struggle");
            }
            Assert.Fail("no small fish in 50 seeds — chance table broken");
        }

        [Test]
        public void BigFishStruggleAndFriendAssist()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var clock = new ManualClock();
                var session = NewSession(clock, seed);
                session.Cast(calmSpot: true);
                clock.Advance(10.6f);
                session.Tick();
                session.Hook();
                if (session.State != FishingState.Struggle)
                    continue;

                session.FriendAssist();
                Assert.AreEqual(FishingState.Landed, session.State);
                Assert.AreEqual(FishingSession.BigFishFood, session.FoodResult,
                    "big fish must be worth more food");
                return;
            }
            Assert.Fail("no big fish in 200 seeds — chance table broken");
        }

        [Test]
        public void FishermanNominationGoesToTopCatcher()
        {
            var stats = new CoopRunStats();
            stats.GetOrAdd(1).FishCaught = 4;
            stats.GetOrAdd(2).FishCaught = 1;
            Assert.Contains(("Рыбак", "Игрок 1"), stats.Nominations());
        }
    }
}
