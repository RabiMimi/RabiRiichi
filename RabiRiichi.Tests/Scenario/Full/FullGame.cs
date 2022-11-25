using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Utils;
using System;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Full {
    [TestClass]
    public class FullGame {
        private static T Execute<T>(Func<T> func) => func();

        [TestMethod]
        public async Task RunFullGame() {
            int playerCount = 4;
            var actionCenter = new ScenarioActionCenter(playerCount);
            ulong seed = (ulong)new Random().Next();
            var config = new GameConfig {
                playerCount = playerCount,
                actionCenter = actionCenter,
                seed = seed
            };
            var game = new Game(config);
            var random = new RabiRand(seed);
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
                    var inquiry = await actionCenter.NextInquiry;
                    foreach (var playerInquiry in inquiry.inquiry.playerInquiries) {
                        if (playerInquiry.actions.Count == 0) {
                            continue;
                        }
                        int choice = random.Next(playerInquiry.actions.Count);
                        var action = playerInquiry.actions[choice];
                        bool ret = action switch {
                            ChoiceAction<int> choiceAction =>
                                inquiry.inquiry.OnResponse(new InquiryResponse(
                                    playerInquiry.playerId, choice, random.Next(choiceAction.options.Count).ToString())),
                            PlayerAction<Empty> confirmAction => inquiry.inquiry.OnResponse(
                                new InquiryResponse(playerInquiry.playerId, choice, Empty.Json)),
                            _ => Execute(() => {
                                Assert.Fail("Unknown action type: " + action.GetType());
                                return false;
                            }),
                        };
                        if (ret) {
                            break;
                        }
                    }
                    inquiry.AssertAutoFinish();
                } catch (OperationCanceledException) {
                    break;
                }

            }
            Assert.AreEqual(GamePhase.Finished, game.info.phase);
            actionCenter.gameLog.Config = config.ToProto();
            actionCenter.gameLog.WriteTo("full_game");
        }
    }
}