using DriftTogether.Coop;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class CapsizeSystemTests
    {
        [Test]
        public void FullCrewOnOneRailEventuallyCapsizes()
        {
            var balance = new CapsizeSystem();
            var crew = new float[] { 1.6f, 1.6f, 1.5f, 1.7f }; // все на правом борту

            float t = 0f;
            while (!balance.Capsized && t < 30f)
            {
                balance.Tick(0.02f, crew, inFastWater: false);
                t += 0.02f;
            }
            Assert.IsTrue(balance.Capsized, "полный экипаж на одном борту должен перевернуть плот");
            Assert.Less(t, 20f, "переворот должен случиться в разумное время");
        }

        [Test]
        public void BalancedCrewNeverCapsizes()
        {
            var balance = new CapsizeSystem();
            var crew = new float[] { -1.5f, 1.5f, -0.5f, 0.5f };
            for (int i = 0; i < 3000; i++)
                balance.Tick(0.02f, crew, inFastWater: true);
            Assert.IsFalse(balance.Capsized);
            Assert.Less(UnityEngine.Mathf.Abs(balance.Tilt), 0.5f);
        }

        [Test]
        public void LoneOffCenterPlayerIsSafe()
        {
            // Одиночка у борта (как хост в smoke-прогоне) не должен переворачивать плот.
            var balance = new CapsizeSystem();
            var crew = new float[] { -0.9f };
            for (int i = 0; i < 6000; i++)
                balance.Tick(0.02f, crew, inFastWater: true);
            Assert.IsFalse(balance.Capsized, "одиночный игрок у борта — это ещё не катастрофа");
        }

        [Test]
        public void TiltRecoversWhenCrewCentres()
        {
            var balance = new CapsizeSystem();
            var oneSide = new float[] { 1.6f, 1.6f };
            for (int i = 0; i < 150; i++)
                balance.Tick(0.02f, oneSide, false);
            float tilted = balance.Tilt;
            Assert.Greater(tilted, 0.05f);

            var centred = new float[] { -0.2f, 0.2f };
            for (int i = 0; i < 600; i++)
                balance.Tick(0.02f, centred, false);
            Assert.Less(UnityEngine.Mathf.Abs(balance.Tilt), 0.05f, "крен должен сходить на нет");
        }

        [Test]
        public void HitKicksAccumulateIntoCapsize()
        {
            var balance = new CapsizeSystem();
            for (int i = 0; i < 10 && !balance.Capsized; i++)
                balance.ApplyHitKick(1f);
            Assert.IsTrue(balance.Capsized, "серия ударов в один борт опрокидывает плот");
        }

        [Test]
        public void RightingNeedsJointEffort()
        {
            var balance = new CapsizeSystem();
            balance.ForceCapsize();
            Assert.IsTrue(balance.Capsized);

            for (int i = 0; i < (int)CapsizeSystem.RightingUnitsNeeded - 1; i++)
                balance.AddRightingEffort(1f);
            Assert.IsTrue(balance.Capsized, "усилий меньше нормы недостаточно");

            balance.AddRightingEffort(1f);
            Assert.IsFalse(balance.Capsized);
            Assert.AreEqual(0f, balance.Tilt);
        }

        [Test]
        public void FoodLossIsFortyPercentRoundedUp()
        {
            Assert.AreEqual(4, CapsizeSystem.FoodLost(10));
            Assert.AreEqual(2, CapsizeSystem.FoodLost(4));
            Assert.AreEqual(1, CapsizeSystem.FoodLost(1));
            Assert.AreEqual(0, CapsizeSystem.FoodLost(0));
        }
    }
}
