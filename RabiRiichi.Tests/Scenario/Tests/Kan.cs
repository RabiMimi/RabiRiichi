using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Threading.Tasks;


namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioKan {
        #region Kan
        [TestMethod]
        public async Task SuccessDaiminkan() {
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
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Daiminkan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            // Ankan after Daiminkan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(2, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
            });

            // Ankan after Ankan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
            });

            // 4th Ankan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111m", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>((ev) => {
                Assert.AreEqual(0, ev.playerId);
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

            // Other player plays waited tile, not allowed to Ankan
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
            }).Resolve();
        }

        [TestMethod]
        public async Task SuccessAnkan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11112223334s19m");
                })
                .WithWall(wall => wall.Reserve("3s"))
                .WithWall(wall => wall.AddRinshan("2s"))
                .Start(1);

            // Daiminkan
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("3s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    Assert.AreEqual(TileSource.Daiminkan, ev.kanSource);
                })
                .AssertNoEvent<RevealDoraEvent>();

            // When tsumo, ok to Ankan but not Daiminkan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    // If multiple kans in hand, have the option to choose which one
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(2, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            // Dora is revealed immediately after Ankan
            // Dora is revealed after playing a tile when Daiminkan
            scenario
                .AssertEvent<AddKanEvent>(ev => {
                    Assert.AreEqual(TileSource.Ankan, ev.kanSource);
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                });

            // Can Ankan after Ankan
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<AddKanEvent>((ev) => {
                    Assert.AreEqual(TileSource.Ankan, ev.kanSource);
                })
                .AssertEvent<RevealDoraEvent>(ev => {
                    Assert.AreEqual(0, ev.playerId);
                }).Resolve();

            // Check Kans exist
            scenario.WithPlayer(0, player => {
                player.hand.called.AssertContains("1111s");
                player.hand.called.AssertContains("2222s");
                player.hand.called.AssertContains("3333s");
            });
        }
        #endregion

        #region SuukanSanra
        private static async Task<Scenario> BuildSuuKanSanRaFromKakan(Action<ScenarioBuilder> setup = null) {
            var scenarioBuilder = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111122339s1239m");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("111222334p1234z");
                })
                .WithWall(wall => wall.Reserve("234563s").AddRinshan("2s1p1z"));
            setup?.Invoke(scenarioBuilder);
            var scenario = scenarioBuilder.Start(1);

            // Pon 2s
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("222s", action => {
                        Assert.AreEqual(1, action.options.Count);
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
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("9s");
            }).AssertAutoFinish();

            // Kakan 3s
            (await scenario.WaitPlayerTurn(0)).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("3333s", action => {
                        Assert.AreEqual(2, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Kakan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            // Ankan 1s
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(2, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
            }).AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
            });

            // Kakan 2s
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("2222s", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Kakan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            // Play 1p
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("1p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(0, ev.playerId);
            });

            // Daiminkan 1p
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<ChiiAction>()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("1111p", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Daiminkan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            // Play 1z
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("1z")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>(ev => {
                Assert.AreEqual(1, ev.playerId);
            });

            return scenario;
        }

        [TestMethod]
        public async Task SuccessKakan_SuccessSuuKanSanRa() {
            var scenario = await BuildSuuKanSanRaFromKakan();

            // Player 0 does not ron
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<RonAction>()
                    .ApplySkip()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertRyuukyoku<SuukanSanra>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(1, ev.honba);
                })
                .Resolve();
        }

        [TestMethod]
        public async Task SuccessKakan_FailSuuKanSanRa() {
            var scenario = await BuildSuuKanSanRaFromKakan();

            // Player 0 does not ron
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertNoRyuukyoku<SuukanSanra>()
                .AssertEvent<AgariEvent>(ev => ev.agariInfos
                    .AssertRon(1, 0)
                    .AssertScore(han: 2, fu: 80)
                    .AssertYaku<Sankantsu>(han: 2))
                .AssertEvent<NextGameEvent>()
                .Resolve();
        }

        [TestMethod]
        public async Task DisabledInConfig_FailSuukanSanra() {
            var scenario = await BuildSuuKanSanRaFromKakan(scenarioBuilder => {
                scenarioBuilder.WithConfig(config => {
                    config.SetRyuukyokuTrigger(RyuukyokuTrigger.All & ~RyuukyokuTrigger.SuukanSanra);
                });
            });


            // Player 0 does not ron
            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<RonAction>()
                    .ApplySkip()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            // No SuukanSanra
            await scenario.AssertNoRyuukyoku<SuukanSanra>().Resolve();
        }

        #endregion

        #region Failed Kan
        [TestMethod]
        public async Task FailAnkanAfterChii() {
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
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.AssertNoAction<KanAction>();
            });
        }

        [TestMethod]
        public async Task FailAnkanAfterPon() {
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
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.AssertNoAction<KanAction>();
            });
        }
        #endregion

        #region Chankan

        [TestMethod]
        public async Task SuccessChankan4Kakan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111s23456789m11z");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("123456789p1s").AddCalled("111m", 2, 2);
                })
                .WithWall(wall => wall.Reserve("1m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111m", action => {
                        Assert.AreEqual(1, action.options.Count);
                    });
            }).AssertAutoFinish();

            scenario.AssertEvent<KanEvent>((ev) => {
                Assert.AreEqual(TileSource.Kakan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(1, 0)
                .AssertScore(han: 3)
                .AssertYaku<Chankan>()
            ).Resolve();
        }

        [TestMethod]
        public async Task SuccessChankan4Ankan_Kokushi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("19s99m19p1234567z");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("123456789p1s111m");
                })
                .WithWall(wall => wall.Reserve("1m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111m", action => {
                        Assert.AreEqual(1, action.options.Count);
                    });
            }).AssertAutoFinish();

            scenario.AssertEvent<KanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(1, 0)
                .AssertScore(yakuman: 1)
                .AssertYaku<Chankan>(han: 1)
                .AssertYaku<KokushiMusou>(yakuman: 1)
            ).Resolve();
        }

        [TestMethod]
        public async Task FailChankan4Ankan_NoKokushi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111s23456789m11z");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("123456789p1s111m");
                })
                .WithWall(wall => wall.Reserve("1m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("1111m", action => {
                        Assert.AreEqual(1, action.options.Count);
                    });
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(0);
        }

        [TestMethod]
        public async Task FailChankan4Daiminkan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111s23456789m11z");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("111m123456789p1s");
                })
                .WithWall(wall => wall.Reserve("1m"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111m", action => {
                    Assert.AreEqual(1, action.options.Count);
                });
            }).ForPlayer(0, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();

            scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Daiminkan, ev.kanSource);
            }).AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(0);
        }
        #endregion

        #region Reveal Dora Option
        private static Scenario BuildRevealDoraOption(DoraOption option) {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.SetDoraOption(option))
                .WithPlayer(0, playerBuilder => playerBuilder
                    .SetFreeTiles("1111579s169m")
                    .AddCalled("222s", 2, 2))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("4689s1135p16669m"))
                .WithWall(wall => wall.Reserve("2s"))
                .Start(0);
            return scenario;
        }

        [TestMethod]
        public async Task InstantReveal_Daiminkan() {
            var scenario = BuildRevealDoraOption(DoraOption.InstantRevealAfterDaiminkan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("6m");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("6666m");
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertNoEvent<RevealDoraEvent>().Resolve();
        }

        [TestMethod]
        public async Task DelayedReveal_Daiminkan() {
            var scenario = BuildRevealDoraOption(DoraOption.All & ~DoraOption.InstantRevealAfterDaiminkan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("6m");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("6666m");
            }).AssertAutoFinish();

            scenario.AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertEvent<RevealDoraEvent>().Resolve();
        }

        [TestMethod]
        public async Task InstantReveal_Kakan() {
            var scenario = BuildRevealDoraOption(DoraOption.InstantRevealAfterKakan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("2222s");
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertNoEvent<RevealDoraEvent>().Resolve();
        }

        [TestMethod]
        public async Task DelayedReveal_Kakan() {
            var scenario = BuildRevealDoraOption(DoraOption.All & ~DoraOption.InstantRevealAfterKakan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("2222s");
            }).AssertAutoFinish();

            scenario.AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertEvent<RevealDoraEvent>().Resolve();
        }

        [TestMethod]
        public async Task InstantReveal_Ankan() {
            var scenario = BuildRevealDoraOption(DoraOption.InstantRevealAfterAnkan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111s");
            }).AssertAutoFinish();

            scenario.AssertEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertNoEvent<RevealDoraEvent>().Resolve();
        }

        [TestMethod]
        public async Task DelayedReveal_Ankan() {
            var scenario = BuildRevealDoraOption(DoraOption.All & ~DoraOption.InstantRevealAfterAnkan);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111s");
            }).AssertAutoFinish();

            scenario.AssertNoEvent<RevealDoraEvent>();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1m");
            }).AssertAutoFinish();

            await scenario.AssertEvent<RevealDoraEvent>().Resolve();
        }
        #endregion

        #region Other

        [TestMethod]
        public async Task SuccessRinshanKaihou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("111s123456789m1z");
                })
                .WithWall(wall => wall.Reserve("1s").AddRinshan("1z").AddDoras("2z"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("1s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .AssertAction<PonAction>()
                    .ChooseTiles<KanAction>("1111s", action => {
                        Assert.AreEqual(1, action.options.Count);
                    })
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(0)
                .AssertScore(han: 2)
                .AssertYaku<RinshanKaihou>(han: 1)
                .AssertYaku<Ittsu>()
            ).Resolve();
        }

        [TestMethod]
        public async Task NoDoraRevealAfter5() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("111m123456789p1s");
                })
                .WithWall(wall => wall
                    .Reserve("1m")
                    .SetRevealedDoraCount(5))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111m", action => {
                    Assert.AreEqual(1, action.options.Count);
                });
            }).AssertAutoFinish();

            await scenario.AssertEvent<AddKanEvent>((ev) => {
                Assert.AreEqual(TileSource.Ankan, ev.kanSource);
            }).AssertEvent<RevealDoraEvent>(ev => {
                Assert.IsNull(ev.dora);
            }).Resolve();
        }
        #endregion
    }
}