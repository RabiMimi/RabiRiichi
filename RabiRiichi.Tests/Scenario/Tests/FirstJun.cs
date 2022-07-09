using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Threading.Tasks;


namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioFirstJun {
        #region Tenhou / Chiihou / wRiichi
        [TestMethod]
        public async Task DealerTenhou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11123455678999s");
                })
                .WithWall(wall => wall.SetRevealedDoraCount(0))
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, (playerInquiry) => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => ev.agariInfos
                .AssertTsumo(0)
                .AssertScore(yakuman: 3)
                .AssertYaku<Tenhou>(yakuman: 1)
                .AssertYaku<JunseiChuurenPoutou>(yakuman: 2)
            ).Resolve();
        }

        [TestMethod]
        public async Task NonDealerChiihou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, (playerBuilder) => {
                    playerBuilder.SetFreeTiles("1112345678999s");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("2345678s");
                })
                .SetFirstJun()
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(yakuman: 3)
                .AssertYaku<Chiihou>(yakuman: 1)
                .AssertYaku<JunseiChuurenPoutou>(yakuman: 2)
            ).Resolve();
        }

        [TestMethod]
        public async Task DoubleRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11223344556678s");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("777888s").AddDoras("1z").AddUradoras("1z");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, (playerInquiry) => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTile<RiichiAction>("7s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            (await scenario.WaitPlayerTurn(0)).ForPlayer(0, (playerInquiry) => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => ev.agariInfos
                .AssertTsumo(0)
                .AssertScore(han: 13, fu: 30, yakuman: 1)
                .AssertKazoeYakuman()
                .AssertYaku<DoubleRiichi>(han: 2)
                .AssertYaku<Ryanpeikou>(han: 3)
                .AssertYaku<Ippatsu>(han: 1)
                .AssertYaku<Chinitsu>(han: 6)
                .AssertYaku<MenzenchinTsumohou>(han: 1)
            ).Resolve();
        }
        #endregion

        #region SuufonRenda
        [TestMethod]
        public async Task SuufonRendaRyuukyoku() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11122233345556z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("444z");
                })
                .SetFirstJun()
                .Start(0);

            for (int i = 0; i < 4; i++) {
                (await scenario.WaitInquiry()).ForPlayer(i, (playerInquiry) => {
                    scenario.WithGameInfo(info => Assert.AreEqual(i, info.currentPlayer));
                    playerInquiry.ChooseTile<PlayTileAction>("4z");
                }).AssertAutoFinish();
            }

            await scenario.AssertRyuukyoku<SuufonRenda>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(1, ev.honba);
                })
                .Resolve();
        }


        [TestMethod]
        public async Task SuufonRendaNoRyuukyoku_NotFirstJun() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("1112223334555z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("1444z");
                })
                .Start(0);

            for (int i = 0; i < 4; i++) {
                (await scenario.WaitInquiry()).ForPlayer(i, (playerInquiry) => {
                    playerInquiry.ChooseTile<PlayTileAction>("4z");
                }).AssertAutoFinish();
            }

            await scenario.AssertNoEvent<SuufonRenda>().Resolve();
        }

        [TestMethod]
        public async Task SuufonRendaNoRyuukyoku_NotSameWind() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11122233345556z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("444z");
                })
                .SetFirstJun()
                .Start(0);

            for (int i = 0; i < 4; i++) {
                (await scenario.WaitInquiry()).ForPlayer(i, (playerInquiry) => {
                    playerInquiry.ChooseTile<PlayTileAction>(i == 0 ? "1z" : "4z");
                }).AssertAutoFinish();
            }

            await scenario.AssertNoEvent<SuufonRenda>().Resolve();
        }

        [TestMethod]
        public async Task DisabledInConfig_NoSuufonRenda() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11122233345556z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("444z");
                })
                .WithConfig(configBuilder => {
                    configBuilder.SetRyuukyokuTrigger(RyuukyokuTrigger.All & ~RyuukyokuTrigger.SuufonRenda);
                })
                .SetFirstJun()
                .Start(0);

            for (int i = 0; i < 4; i++) {
                (await scenario.WaitInquiry()).ForPlayer(i, (playerInquiry) => {
                    playerInquiry.ChooseTile<PlayTileAction>("4z");
                }).AssertAutoFinish();
            }

            await scenario.AssertNoEvent<SuufonRenda>().Resolve();
        }
        #endregion

        #region KyuushuKyuuhai
        [TestMethod]
        public async Task KyuushuKyuuhaiRyuukyoku() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("19s19m1234569p17z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("234z");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.ApplyAction<RyuukyokuAction>();
            }).AssertAutoFinish();

            await scenario.AssertEvent<KyuushuKyuuhai>().Resolve();
        }

        [TestMethod]
        public async Task KyuushuKyuuhaiNoRyuukyoku_Fuuro() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("22223333444455s");
                })
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("19s19m19p1234567z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("1234567z").AddRinshan("3p");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
                playerInquiry.ChooseTiles<KanAction>("3333s");
            }).AssertAutoFinish();

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.AssertNoAction<RyuukyokuAction>();
            }).AssertAutoFinish(false);
        }

        [TestMethod]
        public async Task KyuushuKyuuhaiNoRyuukyoku_Only8() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("19s19m1234569p17z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("123z");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.AssertNoAction<RyuukyokuAction>();
            }).AssertAutoFinish(false);
        }

        [TestMethod]
        public async Task DisabledInConfig_NoKyuushuKyuuhai() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("19s19m1234569p17z");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("234z");
                })
                .WithConfig(configBuilder => {
                    configBuilder.SetRyuukyokuTrigger(RyuukyokuTrigger.All & ~RyuukyokuTrigger.KyuushuKyuuhai);
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry.AssertNoAction<RyuukyokuAction>();
            });
        }

        #endregion
    }
}