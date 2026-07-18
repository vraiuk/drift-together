using DriftTogether.Coop;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class DriftCatchTests
    {
        [Test]
        public void SnagLineIsAlwaysDownstream()
        {
            foreach (float z in new[] { 0f, 55f, 150f, 300f, 500f, 700f })
                Assert.GreaterOrEqual(DriftCatch.SnagLineFor(z), z,
                    $"зацеп для z={z} должен быть ниже по течению");
        }

        [Test]
        public void CatchLinesHaveNoSoftlockGaps()
        {
            var lines = DriftCatch.CatchLinesZ;
            Assert.LessOrEqual(lines[0], DriftCatch.MaxGap, "первая линия близко к старту");
            for (int i = 1; i < lines.Length; i++)
                Assert.LessOrEqual(lines[i] - lines[i - 1], DriftCatch.MaxGap,
                    $"разрыв между {lines[i - 1]} и {lines[i]} слишком велик");
        }

        [Test]
        public void MinimumDriftBeforeCatch()
        {
            // Плот, брошенный прямо на линии зацепа, цепляется за следующую.
            float line = DriftCatch.CatchLinesZ[2];
            Assert.Greater(DriftCatch.SnagLineFor(line), line);
        }
    }

    public class AdriftRuleTests
    {
        [Test]
        public void RequiresSustainedAbandonment()
        {
            var rule = new AdriftRule();
            Assert.IsFalse(rule.Tick(3f, anyoneAboard: false, anchorHolding: false, capsized: false));
            Assert.IsTrue(rule.Tick(2.5f, false, false, false), "после 5 секунд плот считается брошенным");
            Assert.IsTrue(rule.IsAdrift);
        }

        [Test]
        public void CrewOrAnchorOrCapsizePreventsAdrift()
        {
            var rule = new AdriftRule();
            Assert.IsFalse(rule.Tick(10f, anyoneAboard: true, anchorHolding: false, capsized: false));
            Assert.IsFalse(rule.Tick(10f, false, true, false));
            Assert.IsFalse(rule.Tick(10f, false, false, true));
            Assert.IsFalse(rule.IsAdrift);
        }

        [Test]
        public void BoardingResetsTheTimer()
        {
            var rule = new AdriftRule();
            rule.Tick(4f, false, false, false);
            rule.Tick(1f, true, false, false); // кто-то запрыгнул
            Assert.IsFalse(rule.Tick(4.5f, false, false, false),
                "таймер должен начаться заново");
        }
    }
}
