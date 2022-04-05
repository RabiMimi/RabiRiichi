using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using RabiRiichiTests.Helper;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioKan {
        [TestMethod]
        public async Task SuccessDaiMinKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11112223333s19m");
                })
                .WithWall(wall => wall.Reserve("2s").AddRinshan("1119m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.DaiMinKan, ev.kanSource);
                return true;
            });

            // AnKan after DaiMinKan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(2, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                return true;
            });

            // AnKan after AnKan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                return true;
            });

            // 4th AnKan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111m", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            var inquiry = await scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                return true;
            }).WaitInquiry();

            // Check Kans exist
            scenario.WithPlayer(0, player => {
                player.hand.called.AssertContains("1111s");
                player.hand.called.AssertContains("2222s");
                player.hand.called.AssertContains("3333s");
                player.hand.called.AssertContains("1111m");
            });

            // Suukantsu
            inquiry.ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(0)
                    .AssertScore(yakuman: 1)
                    .AssertYaku<Suukantsu>(yakuman: 1);
                return true;
            }).Resolve();
        }

        [TestMethod]
        public async Task SuccessAnKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11112223334s19m");
                })
                .WithWall(wall => wall.Reserve("3s"))
                .WithWall(wall => wall.AddRinshan("2s"))
                .Start(1);

            // DaiMinKan
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("3s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    Assert.AreEqual(TileSource.DaiMinKan, ev.kanSource);
                    return true;
                })
                .AssertNoEvent<RevealDoraEvent>();

            // When tsumo, ok to AnKan but not DaiMinKan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    // If multiple kans in hand, have the option to choose which one
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(2, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            // Dora is revealed immediately after AnKan
            // Dora is revealed after playing a tile when DaiMinKan
            scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                    return true;
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                    return true;
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                    return true;
                });

            // Can AnKan after AnKan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                    return true;
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                    return true;
                }).Resolve();

            // Check Kans exist
            scenario.WithPlayer(0, player => {
                player.hand.called.AssertContains("1111s");
                player.hand.called.AssertContains("2222s");
                player.hand.called.AssertContains("3333s");
            });
        }
    }
}