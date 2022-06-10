using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioFuriten {
        #region General
        [TestMethod]
        public async Task Furiten_ClearedBetweenRounds() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => player
                    .SetFreeTiles("1112345678999s"))
                .WithPlayer(0, player => player
                    .SetFreeTiles("3334445666777s")
                    .SetRiichiTile("1p"))
                .WithWall(wall => wall.Reserve("r5s1p"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                scenario.ForceHaitei();
                playerInquiry.ChooseTile<PlayTileAction>("r5s");
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isRiichiFuriten);
                Assert.IsTrue(player.hand.isDiscardFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            scenario.WithPlayer(0, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isRiichiFuriten);
                Assert.IsFalse(player.hand.isDiscardFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish();

            inquiry = await scenario.WaitInquiry();

            for (int i = 0; i < 4; i++) {
                scenario.WithPlayer(i, player => {
                    Assert.IsFalse(player.hand.isFuriten);
                    Assert.IsFalse(player.hand.isRiichiFuriten);
                    Assert.IsFalse(player.hand.isDiscardFuriten);
                    Assert.IsFalse(player.hand.isTempFuriten);
                });
            }
        }
        #endregion

        #region Temporary Furiten
        [TestMethod]
        public async Task TemporaryFuriten() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => {
                    player.SetFreeTiles("123456789m11z34s");
                })
                .WithWall(wall => {
                    wall.Reserve("2sr5s2s5m");
                })
                .Start(2);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            inquiry.Finish(); // 玩家2摸切

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();  // 玩家1见逃

            inquiry = await scenario.WaitInquiry(); // 玩家3回合

            // Enters Temporary Furiten after refusing to Ron a valid discarded tile
            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish(); // 玩家3摸切

            // Cannot Ron when furiten
            (await scenario.WaitInquiry()).AssertNoActionForPlayer(1).Finish(); // 玩家0摸切

            // Exits Temporary Furiten after drawing a regular tile
            (await scenario.WaitInquiry()).Finish(); // 玩家1摸切

            await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });
        }

        [TestMethod]
        public async Task TemporaryFuriten_Chankan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => player
                    .SetFreeTiles("333567m2344p")
                    .AddCalled("222m", 0, 0))
                .WithPlayer(2, player => player
                    .SetFreeTiles("123789m1234z")
                    .AddCalled("111p", 0, 0))
                .WithWall(wall => {
                    wall.Reserve("1p1z1z4p").AddDoras("12z");
                })
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("1111p");
            }).AssertAutoFinish(); // Kan

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish();

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplyAction<TsumoAction>();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(han: 1)
                .AssertYaku<Tanyao>()).Resolve();
        }

        [TestMethod]
        public async Task TemporaryFuriten_ClaimingTiles() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => {
                    player.SetFreeTiles("123456789m11z34s");
                })
                .WithPlayer(2, player => {
                    player.SetFreeTiles("123456789m55s12z");
                })
                .WithWall(wall => {
                    wall.Reserve("2sr5s2s5m");
                })
                .Start(2);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });

            inquiry.Finish(); // 玩家2摸切

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplySkip();
            }).AssertAutoFinish();  // 玩家1见逃

            inquiry = await scenario.WaitInquiry(); // 玩家3回合

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            inquiry.Finish(); // 玩家3摸切

            (await scenario.WaitInquiry())
                .AssertNoActionForPlayer(1)
                .ForPlayer(2, playerInquiry => {
                    playerInquiry.ChooseTiles<PonAction>("55r5s");
                })
                .AssertAutoFinish(); // 玩家2碰

            inquiry = await scenario.WaitInquiry();

            // 玩家1依然振听
            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });

            // 玩家2切牌
            inquiry.ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("1z")).AssertAutoFinish();

            // 玩家1碰
            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTiles<PonAction>("111z");
            }).AssertAutoFinish();

            await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isTempFuriten);
            });
        }

        #endregion

        #region Discard Furiten

        [TestMethod]
        public async Task DiscardFuriten() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => {
                    player.SetFreeTiles("123456789m11z34s");
                })
                .WithWall(wall => {
                    wall.Reserve("r52593s");
                })
                .Start(1);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isDiscardFuriten);
            });

            inquiry.ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("r5s");
            }).AssertAutoFinish(); // 玩家1摸切

            for (int i = 0; i < 3; i++) {
                // 其余三个玩家摸切，玩家1无法荣和
                inquiry = await scenario.WaitInquiry();

                scenario.WithPlayer(1, player => {
                    Assert.IsTrue(player.hand.isFuriten);
                    Assert.IsTrue(player.hand.isDiscardFuriten);
                });

                inquiry.AssertNoActionForPlayer(1).Finish();
            }

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("4s");
            }).AssertAutoFinish();

            inquiry = await scenario.WaitInquiry();

            // 不再舍牌振听
            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isDiscardFuriten);
            });
        }

        [TestMethod]
        public async Task DiscardFuriten_AfterTenpaiChanges() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, player => {
                    player.SetFreeTiles("1112345678999s");
                })
                .WithWall(wall => {
                    wall.Reserve("r52593s");
                })
                .Start(1);

            var inquiry = await scenario.WaitInquiry();

            scenario.WithPlayer(1, player => {
                Assert.IsFalse(player.hand.isFuriten);
                Assert.IsFalse(player.hand.isDiscardFuriten);
            });

            inquiry.ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("r5s");
            }).AssertAutoFinish(); // 玩家1摸切

            for (int i = 0; i < 3; i++) {
                // 其余三个玩家摸切，玩家1无法荣和
                inquiry = await scenario.WaitInquiry();

                scenario.WithPlayer(1, player => {
                    Assert.IsTrue(player.hand.isFuriten);
                    Assert.IsTrue(player.hand.isDiscardFuriten);
                });

                inquiry.AssertNoActionForPlayer(1).Finish();
            }

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<ChiiAction>()
                    .AssertAction<KanAction>()
                    .AssertAction<PonAction>()
                    .ApplySkip()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry.ChooseTile<PlayTileAction>("2s");
            }).AssertAutoFinish();

            inquiry = await scenario.WaitInquiry();

            // 依然舍牌振听
            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isDiscardFuriten);
            });
        }

        #endregion
    }
}