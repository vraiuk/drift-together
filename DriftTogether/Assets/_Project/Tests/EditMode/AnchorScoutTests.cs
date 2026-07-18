using DriftTogether.Coop;
using DriftTogether.Core;
using NUnit.Framework;
using UnityEngine;

namespace DriftTogether.Tests
{
    public class AnchorSystemTests
    {
        [Test]
        public void HoldsForeverInCalmWater()
        {
            var clock = new ManualClock();
            var anchor = new AnchorSystem(clock, 1);
            anchor.Drop(currentStrength: 1.0f, mooringZone: false);

            clock.Advance(600f);
            anchor.Tick();
            Assert.AreEqual(AnchorState.Holding, anchor.State);
        }

        [Test]
        public void DragsAfterAWhileInStrongCurrent()
        {
            var clock = new ManualClock();
            var anchor = new AnchorSystem(clock, 2);
            anchor.Drop(currentStrength: 3.0f, mooringZone: false);

            clock.Advance(3.5f);
            anchor.Tick();
            Assert.AreEqual(AnchorState.Holding, anchor.State, "первые секунды держит");

            clock.Advance(8f); // максимум срыва — 10 с
            anchor.Tick();
            Assert.AreEqual(AnchorState.Dragging, anchor.State, "в быстрой воде якорь срывает");
        }

        [Test]
        public void MooringZoneAlwaysHolds()
        {
            var clock = new ManualClock();
            var anchor = new AnchorSystem(clock, 3);
            anchor.Drop(currentStrength: 5f, mooringZone: true);
            clock.Advance(600f);
            anchor.Tick();
            Assert.AreEqual(AnchorState.Holding, anchor.State, "у стоянки якорь держит всегда");
        }

        [Test]
        public void RaiseResetsAndAllowsRedrop()
        {
            var clock = new ManualClock();
            var anchor = new AnchorSystem(clock, 4);
            anchor.Drop(3f, false);
            clock.Advance(15f);
            anchor.Tick();
            Assert.AreEqual(AnchorState.Dragging, anchor.State);

            anchor.Raise();
            Assert.AreEqual(AnchorState.Raised, anchor.State);
            anchor.Drop(0.5f, false);
            clock.Advance(100f);
            anchor.Tick();
            Assert.AreEqual(AnchorState.Holding, anchor.State);
        }
    }

    public class ScoutSystemTests
    {
        static ScoutSystem NewScouts()
        {
            var scouts = new ScoutSystem();
            scouts.Zones.Add(new ScoutZone { Name = "Пороги", StartZ = 100f, EndZ = 200f });
            return scouts;
        }

        [Test]
        public void ScoutAheadOfRaftRevealsZone()
        {
            var scouts = NewScouts();
            var zone = scouts.TryScout(1, scoutZ: 150f, raftZ: 40f);
            Assert.IsNotNull(zone);
            Assert.IsTrue(zone.Scouted);
        }

        [Test]
        public void NoCreditWhenRaftIsAlreadyThere()
        {
            var scouts = NewScouts();
            Assert.IsNull(scouts.TryScout(1, scoutZ: 150f, raftZ: 95f),
                "плот почти в зоне — это уже проход, не разведка");
        }

        [Test]
        public void ZoneRevealsOnlyOnce()
        {
            var scouts = NewScouts();
            int events = 0;
            scouts.ZoneScouted += (_, _) => events++;
            Assert.IsNotNull(scouts.TryScout(1, 150f, 40f));
            Assert.IsNull(scouts.TryScout(2, 160f, 40f));
            Assert.AreEqual(1, events);
        }

        [Test]
        public void ScoutNominationCounts()
        {
            var stats = new CoopRunStats();
            stats.GetOrAdd(1).ZonesScouted = 2;
            stats.GetOrAdd(2).ZonesScouted = 1;
            Assert.Contains(("Разведчик", "Игрок 1"), stats.Nominations());
        }
    }
}
