using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;
using RabiRiichi.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Full {
    [TestClass]
    public class FullGame {
        private static int SmartPlayTile(Hand hand, PlayTileAction action) {
            var resolver = hand.game.Get<PatternResolver>();
            var incoming = action.options.Select(op => op.tile)
                .FirstOrDefault(t => !hand.freeTiles.Any(ft => ft.traceId == t.traceId));
            resolver.ResolveShanten(hand, incoming, out var tiles);
            var selected = action.options.FindIndex(op => tiles.Contains(op.tile.tile));
            return selected < 0 ? action.response : selected;

        }

        [TestMethod, Timeout(60 * 1000)]
        public async Task RunFullGame() {
            int playerCount = 4;
            var actionCenter = new ScenarioActionCenter(playerCount);
            ulong seed = 1145141919810ul;
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
                        int choice = playerInquiry.actions.FindIndex(a => a is AgariAction);
                        if (choice == -1) {
                            choice = random.Next(playerInquiry.actions.Count);
                        }
                        var action = playerInquiry.actions[choice];
                        bool ret;
                        if (action is PlayTileAction playTileAction) {
                            var hand = game.GetPlayer(playerInquiry.playerId).hand;
                            ret = inquiry.inquiry.OnResponse(new InquiryResponse(
                                    playerInquiry.playerId, choice, SmartPlayTile(hand, playTileAction).ToString()));
                        } else if (action is IChoiceAction choiceAction) {
                            ret = inquiry.inquiry.OnResponse(new InquiryResponse(
                                    playerInquiry.playerId, choice, random.Next(choiceAction.OptionCount).ToString()));
                        } else if (action is PlayerAction<Empty> playerAction) {
                            ret = inquiry.inquiry.OnResponse(new InquiryResponse(playerInquiry.playerId, choice, Empty.Json));
                        } else {
                            ret = false;
                            Assert.Fail("Unknown action type: " + action.GetType());
                        }
                    }
                    inquiry.AssertAutoFinish();
                } catch (OperationCanceledException) {
                    break;
                }
            }
            actionCenter.gameLog.Config = config.ToProto();
            actionCenter.gameLog.WriteTo("full_game");
            Assert.IsTrue(actionCenter.gameLog.PlayerLogs.All(
                logs => logs.Logs.Any((log) => log.Event?.StopGameEvent != null)),
                "Game did not stop");
            Assert.AreEqual(GamePhase.Finished, game.info.phase);
        }
    }
}