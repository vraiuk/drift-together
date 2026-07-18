using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.TestTools;

namespace DriftTogether.Tests
{
    /// <summary>
    /// Live Unity Relay sanity check (needs the project linked to Unity Cloud
    /// and network access). Excluded from normal runs — execute explicitly:
    /// -runTests -testPlatform PlayMode -testFilter RelayCheckTests
    /// </summary>
    [Explicit("hits live Unity Gaming Services")]
    public class RelayCheckTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RelayAllocationAndJoinCodeWork()
        {
            Assert.IsFalse(string.IsNullOrEmpty(Application.cloudProjectId),
                "project must be linked to Unity Cloud");

            Task<string> task = Allocate();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                Assert.Fail("Relay allocation failed: " + task.Exception?.GetBaseException().Message);

            string joinCode = task.Result;
            Debug.Log($"[RelayCheck] join code: {joinCode}");
            Assert.IsFalse(string.IsNullOrEmpty(joinCode), "join code must be issued");
            Assert.GreaterOrEqual(joinCode.Length, 6);
        }

        static async Task<string> Allocate()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            var allocation = await RelayService.Instance.CreateAllocationAsync(3);
            return await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
    }
}
