using System.Collections;
using DriftTogether.Core;
using DriftTogether.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DriftTogether.Tests
{
    /// <summary>
    /// Automated smoke test of the main scenario: menu → start → autopilot
    /// playthrough (campfire rest included) → finish → results → back to menu.
    /// </summary>
    public class SmokePlaythroughTests
    {
        [UnityTest]
        [Timeout(900000)] // up to 15 minutes real time
        public IEnumerator FullPlaythroughReachesResultsAndReturnsToMenu()
        {
            // 1. Main menu loads and builds its UI.
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenuController>(),
                "main menu controller must exist");

            // 2. «Играть» — same call the button makes.
            SmokeAutopilot.RequestedByTest = true;
            SceneManager.LoadScene("River");
            yield return null;
            yield return null;

            var flow = Object.FindFirstObjectByType<GameFlow>();
            Assert.IsNotNull(flow, "game flow must exist in the River scene");

            // 3. Autopilot drives the kayak to the finish.
            float deadline = Time.realtimeSinceStartup + 840f;
            while (flow.CurrentState != GameFlow.State.Finished &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(GameFlow.State.Finished, flow.CurrentState,
                "autopilot must reach the finish zone");
            Assert.IsTrue(flow.Level.Campfire.HasRested, "campfire rest is mandatory");
            Assert.AreNotEqual(RiverRoute.None, flow.Stats.ChosenRoute,
                "route choice must be recorded");

            // 4. Results screen appears.
            float resultsDeadline = Time.realtimeSinceStartup + 10f;
            ResultsScreen results = null;
            while (results == null && Time.realtimeSinceStartup < resultsDeadline)
            {
                results = Object.FindFirstObjectByType<ResultsScreen>();
                yield return null;
            }
            Assert.IsNotNull(results, "results screen must appear after the finish");

            // 5. Back to the main menu (what the «В главное меню» button does).
            SmokeAutopilot.RequestedByTest = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenuController>(),
                "returning to the menu must work after a run");
        }

        [TearDown]
        public void Cleanup()
        {
            SmokeAutopilot.RequestedByTest = false;
            Time.timeScale = 1f;
        }
    }
}
