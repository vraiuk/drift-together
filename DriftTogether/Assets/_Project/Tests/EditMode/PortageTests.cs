using DriftTogether.Coop;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class PortageSystemTests
    {
        static PortageSystem Cleared()
        {
            var portage = new PortageSystem();
            portage.Begin();
            for (int t = 0; t < PortageSystem.TreeCount; t++)
                for (int c = 0; c < PortageSystem.ChopsPerTree; c++)
                    portage.Chop(t);
            return portage;
        }

        [Test]
        public void ClearingNeedsAllTrees()
        {
            var portage = new PortageSystem();
            portage.Begin();
            Assert.AreEqual(PortagePhase.Clearing, portage.Phase);

            for (int c = 0; c < PortageSystem.ChopsPerTree; c++)
                portage.Chop(0);
            Assert.AreEqual(PortagePhase.Clearing, portage.Phase, "одно дерево — ещё не просека");
            Assert.AreEqual(0, portage.ChopsLeft(0));

            var done = Cleared();
            Assert.AreEqual(PortagePhase.Hauling, done.Phase);
        }

        [Test]
        public void FourthChopFellsTheTree()
        {
            var portage = new PortageSystem();
            portage.Begin();
            for (int c = 0; c < PortageSystem.ChopsPerTree - 1; c++)
                Assert.IsFalse(portage.Chop(1));
            Assert.IsTrue(portage.Chop(1), "последний удар валит дерево");
        }

        [Test]
        public void MorePullersHaulFaster()
        {
            var one = Cleared();
            var four = Cleared();
            for (int i = 0; i < 20; i++) // 10 секунд — до насыщения прогресса
            {
                one.Tick(0.5f, 1, hasFood: true);
                four.Tick(0.5f, 4, hasFood: true);
            }
            Assert.Greater(four.HaulProgress, one.HaulProgress * 2.5f,
                "четверо тащат существенно быстрее одного");
        }

        [Test]
        public void StarvingCrewIsMuchSlower()
        {
            var fed = Cleared();
            var hungry = Cleared();
            for (int i = 0; i < 16; i++) // 8 секунд — оба далеко от финиша
            {
                fed.Tick(0.5f, 2, hasFood: true);
                hungry.Tick(0.5f, 2, hasFood: false);
            }
            Assert.Greater(fed.HaulProgress, hungry.HaulProgress * 2f);
        }

        [Test]
        public void HaulConsumesAboutFourFood()
        {
            var portage = Cleared();
            int eaten = 0;
            int guard = 0;
            while (portage.Phase == PortagePhase.Hauling && guard++ < 100000)
                eaten += portage.Tick(0.25f, 4, hasFood: true);
            Assert.AreEqual(PortagePhase.Done, portage.Phase);
            Assert.GreaterOrEqual(eaten, 3, "волок должен стоить еды");
            Assert.LessOrEqual(eaten, 4);
        }
    }
}
