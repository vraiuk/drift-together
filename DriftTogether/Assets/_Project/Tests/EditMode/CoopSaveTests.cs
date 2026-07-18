using System.IO;
using DriftTogether.Coop;
using NUnit.Framework;
using UnityEngine;

namespace DriftTogether.Tests
{
    public class CoopSaveTests
    {
        string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "coop_save_test.json");
            CoopSave.PathOverrideForTests = _tempPath;
            if (File.Exists(_tempPath))
                File.Delete(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            CoopSave.PathOverrideForTests = null;
            if (File.Exists(_tempPath))
                File.Delete(_tempPath);
        }

        [Test]
        public void SaveRoundTripsAllFields()
        {
            CoopSave.Write(new CoopSaveData
            {
                posX = 5.5f, posY = 0f, posZ = 534f, rotY = 90f,
                food = 7, logs = 4, hull = 3, modulesMask = 0b101
            });

            var loaded = CoopSave.TryLoad();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(new Vector3(5.5f, 0f, 534f), loaded.Position);
            Assert.AreEqual(90f, loaded.Rotation.eulerAngles.y, 0.01f);
            Assert.AreEqual(7, loaded.food);
            Assert.AreEqual(4, loaded.logs);
            Assert.AreEqual(3, loaded.hull);
            Assert.AreEqual(0b101, loaded.modulesMask);
        }

        [Test]
        public void MissingFileReturnsNull()
        {
            Assert.IsNull(CoopSave.TryLoad());
        }

        [Test]
        public void ClearRemovesTheSave()
        {
            CoopSave.Write(new CoopSaveData { food = 1 });
            Assert.IsNotNull(CoopSave.TryLoad());
            CoopSave.Clear();
            Assert.IsNull(CoopSave.TryLoad());
        }
    }

    public class RaftConditionTests
    {
        [Test]
        public void ConditionMatchesTheDocExamples()
        {
            Assert.AreEqual("приплыли на яхте", RaftCondition.Describe(5, 3));
            Assert.AreEqual("крепкий плот", RaftCondition.Describe(4, 0));
            Assert.AreEqual("потрёпан, но горд", RaftCondition.Describe(2, 1));
            Assert.AreEqual("доползли на двери от шкафа", RaftCondition.Describe(1, 0));
        }
    }
}
