using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioFirstJun {
        [TestMethod]
        public async Task DealerTenhou() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11123455678999s");
                })
                .SetFirstJun()
                .Start(0);

            (await scenario.WaitInquiry()).ForPlayer(0, (playerInquiry) => {
                playerInquiry
                    .AssertAction<RiichiAction>()
                    .AssertAction<PlayTileAction>()
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(0)
                    .AssertScore(1, 40, 3)
                    .AssertYaku<Tenhou>(yakuman: 1)
                    .AssertYaku<JunseiChuurenPoutou>(yakuman: 2);
                return true;
            }).Resolve();
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

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(1, 40, 3)
                    .AssertYaku<Chiihou>(yakuman: 1)
                    .AssertYaku<JunseiChuurenPoutou>(yakuman: 2);
                return true;
            }).Resolve();
        }

        [TestMethod]
        public async Task DoubleRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(0, playerBuilder => {
                    playerBuilder.SetFreeTiles("11223344556678s");
                })
                .WithWall(wallBuilder => {
                    wallBuilder.Reserve("777888s");
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

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(0)
                    .AssertScore(13, 30)
                    .AssertKazoeYakuman()
                    .AssertYaku<DoubleRiichi>(han: 2)
                    .AssertYaku<Ryanpeikou>(han: 3)
                    .AssertYaku<Ippatsu>(han: 1)
                    .AssertYaku<Chinitsu>(han: 6)
                    .AssertYaku<MenzenchinTsumohou>(han: 1);
                return true;
            }).Resolve();
        }

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
                    playerInquiry.ChooseTile<PlayTileAction>("4z");
                }).AssertAutoFinish();
            }

            await scenario.AssertRyuukyoku<SuufonRenda>()
                .AssertEvent<BeginGameEvent>(ev => {
                    Assert.AreEqual(0, ev.round);
                    Assert.AreEqual(0, ev.dealer);
                    Assert.AreEqual(1, ev.honba);
                    return true;
                })
                .Resolve();
        }


        [TestMethod]
        public async Task SuufonRendaNoRyuukyoku() {
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
    }
}