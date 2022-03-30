using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
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
                .WithWall(wall => wall.Reserve("2s"))
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

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                return true;
            }).Resolve();

            // Check Kans exist
            scenario.WithPlayer(0, player => {
                player.hand.called.AssertContains("1111s");
                player.hand.called.AssertContains("2222s");
                player.hand.called.AssertContains("3333s");
            });
        }

        [TestMethod]
        public async Task SuccessAnKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11122233345s19m");
                })
                .WithWall(wall => wall.Reserve("1s"))
                .WithWall(wall => wall.AddRinshan("29s"))
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            Kan kan1 = null;
            scenario
                .AssertEvent<KanEvent>((ev) => {
                    kan1 = ev.kan;
                    Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                    return true;
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                    return true;
                });

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            Kan kan2 = null;
            scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    kan2 = ev.kan;
                    Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                    return true;
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                    return true;
                });

            // scenario.WithGame(game => {
            //     CollectionAssert.Contains(game.GetPlayer(0).hand.freeTiles, kan1);
            //     CollectionAssert.Contains(game.GetPlayer(0).hand.freeTiles, kan2);
            // });
        }
    }
}