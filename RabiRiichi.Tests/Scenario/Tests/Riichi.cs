using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioRiichi {
        private static async Task RiichiWith(Scenario scenario, int playerId, string tile) {
            (await scenario.WaitInquiry()).ForPlayer(playerId, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTile<RiichiAction>(tile)
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RiichiEvent>((ev) => {
                Assert.AreEqual(TileSource.Discard, ev.discarded.source);
                Assert.AreEqual(DiscardReason.Draw, ev.reason);
                ev.discarded.tile.AssertEquals(tile);
            });
        }

        [TestMethod]
        public async Task SuccessRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithWall(wall => wall.Reserve("7r5s6p").AddUradoras("5s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<AgariEvent>(ev => ev.agariInfos
                    .AssertRon(3, 1)
                    .AssertScore(han: 4, fu: 40)
                    .AssertYaku<Riichi>()
                    .AssertYaku<Ippatsu>()
                    .AssertYaku<Uradora>(han: 2)
                )
                .AssertEvent<ConcludeGameEvent>(
                    ev => ev.uradoras[0].AssertEquals("5s"))
                .Resolve();
        }

        [TestMethod]
        public async Task SuccessWRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithWall(wall => wall.Reserve("7r557s6p"))
                .SetFirstJun()
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options
                            .OfType<ChooseTileActionOption>()
                            .Select(o => o.tile)
                            .ToTiles()
                            .AssertEquals("6p");
                    })
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(han: 4, fu: 30)
                .AssertYaku<DoubleRiichi>()
                .AssertYaku<Ippatsu>()
                .AssertYaku<MenzenchinTsumohou>()
                .AssertYaku<Uradora>(han: 0)
            ).Resolve();
        }

        [TestMethod]
        public async Task NoAnkanIfTenpaiChanges() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("45667s234m34566p");
                })
                .WithWall(wall => wall
                    .Reserve("67776s")
                    .AddDoras("1z")
                    .AddUradoras("1z"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();
        }

        #region Ippatsu
        [TestMethod]
        public async Task NoIppatsuWhenRiichiTileClaimed() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithPlayer(2, playerBuilder => {
                    playerBuilder.SetFreeTiles("77s1234566789p1z");
                })
                .WithWall(wall => wall.Reserve("7s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("777s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("6p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(2, 1)
                .AssertScore(han: 1, fu: 40)
                .AssertYaku<Riichi>()
            ).Resolve();
        }

        [TestMethod]
        public async Task NoIppatsuWhenRinshanKaihou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("123666s234m3456p");
                })
                .WithWall(wall => wall
                    .Reserve("77776s")
                    .AddRinshan("3p")
                    .AddDoras("11z")
                    .AddUradoras("55s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("6666s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options
                            .OfType<ChooseTileActionOption>()
                            .Select(o => o.tile)
                            .ToTiles()
                            .AssertEquals("3p");
                    })
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(han: 11, fu: 40)
                    .AssertYaku<Riichi>()
                    .AssertYaku<RinshanKaihou>()
                    .AssertYaku<MenzenchinTsumohou>()
                    .AssertYaku<Uradora>(han: 8);
            }).Resolve();
        }

        [TestMethod]
        public async Task NoIppatsuAfter1Jun() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithWall(wall => wall.Reserve("777786s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitPlayerTurn(1)).Finish();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("6s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(han: 1, fu: 40)
                    .AssertYaku<Riichi>();
            }).Resolve();
        }

        [TestMethod]
        public async Task NoIppatsuWhenOtherTileClaimed() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("88s1234566789p1z");
                })
                .WithWall(wall => wall.Reserve("78s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ChooseTiles<PonAction>("888s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry
                    .ChooseTile<PlayTileAction>("6p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(0, 1)
                    .AssertScore(han: 1, fu: 40)
                    .AssertYaku<Riichi>();
            }).Resolve();
        }

        [TestMethod]
        public async Task SuccessIppatsuOnChankan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12367s234m34567p");
                })
                .WithPlayer(2, playerBuilder => {
                    playerBuilder
                        .AddCalled("888p", 0, 3, DiscardReason.Draw)
                        .SetFreeTiles("1234567z111s");
                })
                .WithWall(wall => wall.Reserve("6s8p").AddUradoras("1z"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTiles<KanAction>("8888p")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(han: 4, fu: 30)
                    .AssertYaku<Riichi>()
                    .AssertYaku<Chankan>()
                    .AssertYaku<Pinfu>()
                    .AssertYaku<Ippatsu>();
            }).Resolve();
        }
        #endregion

        #region Riichi Furiten

        [TestMethod]
        public async Task SuccessTsumoOnRiichiFuriten() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("12366s234m34566p")
                )
                .WithWall(wall => wall.Reserve("76s6p56s"))
                .Start(1);

            await RiichiWith(scenario, 1, "7s");

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isRiichiFuriten);
            });

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isRiichiFuriten);
            });

            inquiry.Finish();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(1).Finish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario
                .AssertEvent<AgariEvent>(ev => ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(han: 3, fu: 30)
                    .AssertYaku<Riichi>()
                    .AssertYaku<Ippatsu>()
                    .AssertYaku<MenzenchinTsumohou>()
                )
                .Resolve();
        }

        #endregion

        #region Riichi Policy
        private static readonly long[] POINTS_RANGE = new long[] { 10000, 50000 };
        private static ScenarioBuilder WithPolicy(RiichiPolicy policy, long points, long riichiPoints = 1000) {
            return new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder
                        .SetPoints(points)
                        .SetFreeTiles("12367s234m34567p");
                })
                .WithWall(wall => wall.Reserve("7s"))
                .WithConfig(configBuilder => {
                    configBuilder
                        .SetPointsRange(POINTS_RANGE)
                        .SetRiichiPolicy(policy)
                        .SetRiichiPoints(riichiPoints);
                });
        }

        [TestMethod]
        public async Task SuccessSufficientPoints() {
            var scenario = WithPolicy(RiichiPolicy.SufficientPoints, POINTS_RANGE[0] + 1000).Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }

        [TestMethod]
        public async Task FailInsufficientPoints() {
            var scenario = WithPolicy(RiichiPolicy.SufficientPoints, POINTS_RANGE[0] + 500).Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }

        [TestMethod]
        public async Task SuccessNonNegativePoints() {
            var scenario = WithPolicy(RiichiPolicy.ValidPoints, POINTS_RANGE[0]).Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }

        [TestMethod]
        public async Task FailNegativePoints() {
            var scenario = WithPolicy(RiichiPolicy.ValidPoints, POINTS_RANGE[0] - 1).Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }

        [TestMethod]
        public async Task SuccessSufficientTiles() {
            var scenario = WithPolicy(RiichiPolicy.SufficientTiles, POINTS_RANGE[0] - 1000)
                .Build(1)
                .WithWall(wall => {
                    wall.remaining.RemoveRange(0, wall.remaining.Count - 5); // P1 will draw 1 tile
                })
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }

        [TestMethod]
        public async Task FailInsufficientTiles() {
            var scenario = WithPolicy(RiichiPolicy.SufficientTiles, POINTS_RANGE[0] - 1000)
                .Build(1)
                .WithWall(wall => {
                    wall.remaining.RemoveRange(0, wall.remaining.Count - 4); // P1 will draw 1 tile
                })
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .AssertNoMoreActions();
            });
        }
        #endregion

        #region SuuchaRiichi
        private static async Task<Scenario> BuildSuuchaRiichi(Action<ScenarioBuilder> setup = null) {
            static void playerHandSetup(ScenarioBuilder.PlayerHandBuilder playerBuilder) {
                playerBuilder.SetFreeTiles("19s19m129p123456z");
            };
            var scenarioBuilder = new ScenarioBuilder()
                .WithPlayer(0, playerHandSetup)
                .WithPlayer(1, playerHandSetup)
                .WithPlayer(2, playerHandSetup)
                .WithPlayer(3, playerHandSetup)
                .WithWall(wall => wall.Reserve("7777z"));
            setup?.Invoke(scenarioBuilder);
            var scenario = scenarioBuilder.Start(0);

            for (int i = 0; i < 4; i++) {
                (await scenario.WaitInquiry()).ForPlayer(i, playerInquiry => {
                    playerInquiry
                        .AssertAction<PlayTileAction>()
                        .ChooseTile<RiichiAction>("2p")
                        .AssertNoMoreActions();
                }).AssertAutoFinish();
            }

            return scenario;
        }

        [TestMethod]
        public async Task SuuchaRiichiRyuukyoku() {
            var scenario = await BuildSuuchaRiichi();

            await scenario.AssertRyuukyoku<SuuchaRiichi>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(1, ev.honba);
                }).Resolve();
        }

        [TestMethod]
        public async Task DisabledInConfig_NoSuuchaRiichi() {
            var scenario = await BuildSuuchaRiichi(builder => builder.WithConfig(configBuilder => configBuilder.SetRyuukyokuTrigger(RyuukyokuTrigger.All & ~RyuukyokuTrigger.SuuchaRiichi)));

            await scenario.AssertNoRyuukyoku<SuuchaRiichi>().Resolve();
        }
        #endregion
    }
}