using DriftTogether.Coop;
using NUnit.Framework;

namespace DriftTogether.Tests
{
    public class ModuleSystemTests
    {
        [Test]
        public void InstallCostsTimberAndIsIrreversible()
        {
            var modules = new ModuleSystem();
            Assert.AreEqual(ModuleSystem.SailCost, modules.TryInstall(RaftSlot.Bow, logsAvailable: 5));
            Assert.IsTrue(modules.Has(RaftSlot.Bow));
            Assert.AreEqual(-1, modules.TryInstall(RaftSlot.Bow, 10), "слот занят — переустановка невозможна");
        }

        [Test]
        public void NotEnoughTimberRejected()
        {
            var modules = new ModuleSystem();
            Assert.AreEqual(-1, modules.TryInstall(RaftSlot.Bow, ModuleSystem.SailCost - 1));
            Assert.IsFalse(modules.Has(RaftSlot.Bow));
        }

        [Test]
        public void SailTradesSpeedForHandling()
        {
            var modules = new ModuleSystem();
            Assert.AreEqual(1f, modules.SpeedMultiplier);
            modules.TryInstall(RaftSlot.Bow, 10);
            Assert.AreEqual(ModuleSystem.SailSpeedBonus, modules.SpeedMultiplier);
            Assert.AreEqual(ModuleSystem.SailTurnPenalty, modules.TurnMultiplier);
        }

        [Test]
        public void CanopyDriesAndWorkbenchRepairs()
        {
            var modules = new ModuleSystem();
            modules.TryInstall(RaftSlot.Midship, 10);
            modules.TryInstall(RaftSlot.Stern, 10);
            Assert.IsTrue(modules.DryingEverywhere);
            Assert.AreEqual(ModuleSystem.WorkbenchRepairFactor, modules.RepairSpeedMultiplier);
        }

        [Test]
        public void EveryModuleRaisesTheTiltFactor()
        {
            var modules = new ModuleSystem();
            Assert.AreEqual(1f, modules.TiltFactor);
            modules.TryInstall(RaftSlot.Bow, 10);
            modules.TryInstall(RaftSlot.Midship, 10);
            modules.TryInstall(RaftSlot.Stern, 10);
            Assert.AreEqual(1f + 3 * ModuleSystem.TiltFactorPerModule, modules.TiltFactor, 1e-4f);
            Assert.AreEqual(3, modules.InstalledCount);
        }

        [Test]
        public void MaskRoundTripsThroughNetwork()
        {
            var host = new ModuleSystem();
            host.TryInstall(RaftSlot.Bow, 10);
            host.TryInstall(RaftSlot.Stern, 10);

            var client = new ModuleSystem();
            client.LoadMask(host.Mask);
            Assert.IsTrue(client.Has(RaftSlot.Bow));
            Assert.IsFalse(client.Has(RaftSlot.Midship));
            Assert.IsTrue(client.Has(RaftSlot.Stern));
        }
    }
}
