using DriftTogether.Coop;
using DriftTogether.Core;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class BoarBrainTests
    {
        [Test]
        public void ChargesWhenPlayerComesClose()
        {
            var clock = new ManualClock();
            var boar = new BoarBrain(clock);
            Assert.AreEqual(BoarMood.Wandering, boar.Mood);

            boar.Tick(distanceToNearestPlayer: 4f);
            Assert.AreEqual(BoarMood.Charging, boar.Mood);
        }

        [Test]
        public void HitSendsBoarFleeing()
        {
            var clock = new ManualClock();
            var boar = new BoarBrain(clock);
            boar.Tick(4f);
            Assert.IsTrue(boar.Tick(0.8f), "контакт должен засчитать удар");
            Assert.AreEqual(BoarMood.Fleeing, boar.Mood);

            clock.Advance(BoarBrain.FleeSeconds + 0.5f);
            boar.Tick(30f);
            Assert.AreEqual(BoarMood.Wandering, boar.Mood, "после бегства кабан успокаивается");
        }

        [Test]
        public void GivesUpWhenPlayerEscapes()
        {
            var clock = new ManualClock();
            var boar = new BoarBrain(clock);
            boar.Tick(4f);
            boar.Tick(BoarBrain.AggroRange * 3f);
            Assert.AreEqual(BoarMood.Wandering, boar.Mood);
        }

        [Test]
        public void ChargeTimesOut()
        {
            var clock = new ManualClock();
            var boar = new BoarBrain(clock);
            boar.Tick(4f);
            clock.Advance(BoarBrain.ChargeTimeout + 0.5f);
            boar.Tick(4f);
            Assert.AreEqual(BoarMood.Wandering, boar.Mood);
        }

        [Test]
        public void GathererNominationCounts()
        {
            var stats = new CoopRunStats();
            stats.GetOrAdd(1).Gathered = 7;
            stats.GetOrAdd(2).Gathered = 2;
            Assert.Contains(("Добытчик", "Игрок 1"), stats.Nominations());
        }
    }
}
