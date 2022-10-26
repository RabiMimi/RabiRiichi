using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Full {
    [TestClass]
    public class FullGame {
        [TestMethod]
        public async Task RunFullGame() {
            int playerCount = 4;
            var actionCenter = new ScenarioActionCenter(playerCount);
            var game = new Game(new GameConfig {
                playerCount = playerCount,
                actionCenter = actionCenter
            });
            var runToEnd = game.Start().ContinueWith((e) => {
                if (e.IsFaulted) {
                    actionCenter.ForceFail(e.Exception);
                } else if (e.IsCanceled) {
                    actionCenter.ForceFail(new Exception("Game cancelled"));
                } else {
                    actionCenter.ForceCancel();
                }
            });
            while (!runToEnd.IsCompleted) {
                try {
                    (await actionCenter.NextInquiry).Finish();
                } catch (OperationCanceledException) {
                    break;
                }

            }
            Assert.AreEqual(GamePhase.Finished, game.info.phase);
            actionCenter.gameLog.WriteTo("full_game");
        }
    }
}