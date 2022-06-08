using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using RabiRiichiTests.Helper;
using System.Threading.Tasks;

namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioDora {
        #region Dora
        [TestMethod]
        public async Task SuccessDora() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2345r5m234p34s")
                    .AddCalled("234s", 2, 2))
                .WithWall(wall => wall
                    .Reserve("2s")
                    .AddDoras("12345s")
                    .AddUradoras("12345m"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("2s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(2, 1)
                .AssertScore(han: 5)
                .AssertYaku<Tanyao>()
                .AssertYaku<SanshokuDoujun>()
                .AssertYaku<Dora>(han: 2)
                .AssertYaku<Akadora>(han: 1)
            ).AssertEvent<ConcludeGameEvent>(ev => {
                ev.doras.AssertEquals("12345s");
                ev.uradoras.AssertEquals("12345m");
            }).Resolve();
        }
        #endregion

        #region Uradora
        [TestMethod]
        public async Task SuccessUradora() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder =>
                    playerBuilder.SetFreeTiles("2345r5m234p13344s"))
                .WithWall(wall => wall
                    .Reserve("29462s")
                    .AddDoras("12456s")
                    .AddUradoras("3s2345m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<RiichiAction>("1s")
            ).AssertAutoFinish();

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertKazoeYakuman()
                .AssertScore(han: 13, yakuman: 1)
                .AssertYaku<Riichi>()
                .AssertYaku<Ippatsu>()
                .AssertYaku<MenzenchinTsumohou>()
                .AssertYaku<Iipeikou>()
                .AssertYaku<Pinfu>()
                .AssertYaku<Tanyao>()
                .AssertYaku<SanshokuDoujun>(han: 2)
                .AssertYaku<Dora>(han: 2)
                .AssertYaku<Uradora>(han: 2)
                .AssertYaku<Akadora>(han: 1)
            ).AssertEvent<ConcludeGameEvent>(ev => {
                ev.doras.AssertEquals("12456s");
                ev.uradoras.AssertEquals("3s2345m");
            }).Resolve();
        }

        [TestMethod]
        public async Task NoUradora_NotRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder =>
                    playerBuilder.SetFreeTiles("2345r5m234p13344s"))
                .WithWall(wall => wall
                    .Reserve("29462s")
                    .AddDoras("12456s")
                    .AddUradoras("3s2345m"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("1s")
            ).AssertAutoFinish();

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .AssertAction<RiichiAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(han: 9)
                .AssertYaku<MenzenchinTsumohou>()
                .AssertYaku<Iipeikou>()
                .AssertYaku<Pinfu>()
                .AssertYaku<Tanyao>()
                .AssertYaku<SanshokuDoujun>(han: 2)
                .AssertYaku<Dora>(han: 2)
                .AssertYaku<Akadora>(han: 1)
            ).AssertEvent<ConcludeGameEvent>(ev => {
                ev.doras.AssertEquals("12456s");
                ev.uradoras.AssertEquals("3s2345m");
            }).Resolve();
        }
        #endregion

        #region Yaku
        [TestMethod]
        public async Task NoAgari_DoraIsNotYaku() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("2345r5m23344s")
                    .AddCalled("111s", 1, 2))
                .WithWall(wall => wall
                    .Reserve("2s")
                    .AddDoras("12456s")
                    .AddUradoras("3s2345m"))
                .Start(2);

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("2s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).AssertNoActionForPlayer(1);

            scenario.WithPlayer(1, player => {
                Assert.IsTrue(player.hand.isFuriten);
                Assert.IsTrue(player.hand.isTempFuriten);
            });
        }
        #endregion
    }
}