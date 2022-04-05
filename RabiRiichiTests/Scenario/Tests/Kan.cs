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
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("4689s1135p66669m");
                })
                .WithWall(wall => wall.Reserve("2s").AddRinshan("1118m"))
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
            }).AssertNoEvent<RevealDoraEvent>();

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
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
                return true;
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
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
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
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

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.AnKan, ev.kanSource);
                return true;
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
                return true;
            });

            // Play a tile after 4 kans
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("8m")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            // Check Kans exist
            scenario.WithPlayer(0, player => {
                player.hand.called.AssertContains("1111s");
                player.hand.called.AssertContains("2222s");
                player.hand.called.AssertContains("3333s");
                player.hand.called.AssertContains("1111m");
            });

            // Other player plays waited tile, not allowed to AnKan
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("9m")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            // Player 0 wins
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(1, 0)
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

        private static async Task<Scenario> BuildSuuKanSanRaFromKaKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111122339s1239m");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("111222334p1234z");
                })
                .WithWall(wall => wall.Reserve("234563s").AddRinshan("2s1p1z"))
                .Start(1);

            // Pon 2s
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("9m");
            }).AssertAutoFinish();

            // Pon 3s
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("3s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("9s");
            }).AssertAutoFinish();

            // KaKan 3s
            (await scenario.WaitPlayerTurn(0)).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(2, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.KaKan, ev.kanSource);
                return true;
            }).AssertNoEvent<RevealDoraEvent>();

            // AnKan 1s
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
            }).AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
                return true;
            }).AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
                return true;
            });

            // KaKan 2s
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.KaKan, ev.kanSource);
                return true;
            }).AssertNoEvent<RevealDoraEvent>();

            // Play 1p
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("1p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
                return true;
            });

            // DaiMinKan 1p
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<ChiiAction>()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("1111p", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.DaiMinKan, ev.kanSource);
                return true;
            }).AssertNoEvent<RevealDoraEvent>();

            // Play 1z
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("1z")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(1, ev.playerId);
                return true;
            });

            return scenario;
        }

        [TestMethod]
        public async Task SuccessKaKan_SuccessSuuKanSanRa() {
            var scenario = await BuildSuuKanSanRaFromKaKan();

            // Player 0 does not ron
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<RonAction>()
                    .ApplyAction<SkipAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertRyuukyoku<SuukanSanra>().Resolve();
        }

        [TestMethod]
        public async Task SuccessKaKan_FailSuuKanSanRa() {
            var scenario = await BuildSuuKanSanRaFromKaKan();

            // Player 0 does not ron
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertNoRyuukyoku<SuukanSanra>()
                .AssertEvent<AgariEvent>(ev => {
                    ev.agariInfos
                        .AssertRon(1, 0)
                        .AssertScore(han: 2, fu: 80)
                        .AssertYaku<Sankantsu>(han: 2);
                    return true;
                })
                .AssertEvent<NextGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task FailAnKanAfterChii() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("1111223333s129m");
                })
                .WithWall(wall => wall.Reserve("3m"))
                .Start(3);

            (await scenario.WaitInquiry()).ForPlayer(3, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("3m");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<ChiiAction>("123m", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.AssertNoAction<KanAction>();
            });
        }

        [TestMethod]
        public async Task FailAnKanAfterPon() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("1111223333s129m");
                })
                .WithWall(wall => wall.Reserve("2s"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                        return true;
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.AssertNoAction<KanAction>();
            });
        }
    }
}