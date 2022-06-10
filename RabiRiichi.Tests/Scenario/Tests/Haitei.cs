using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core.Setup;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Scenario.Tests {
    [TestClass]
    public class ScenarioHaitei {
        #region Call
        [TestMethod]
        public async Task Haitei_CannotChiiOrKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m111123356789s"))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("78m11122357999p"))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("999m444666888p3s"))
                .WithWall(wall => wall.Reserve("1m"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("9m")
                .AssertNoMoreActions()
            ).AssertAutoFinish();
        }

        [TestMethod]
        public async Task Haitei_CannotKaKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("19m23356789s")
                    .AddCalled("111s", 0, 2))
                .WithPlayer(3, playerBuilder => playerBuilder
                    .SetFreeTiles("99m4446668889p3s"))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("9m")
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<NextGameEvent>().Resolve();
        }

        [TestMethod]
        public async Task Haitei_CannotPon() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("19m23356789s")
                    .AddCalled("111s", 0, 2))
                .WithWall(wall => wall.Reserve("1s"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("9m")
                .AssertNoMoreActions()
            ).AssertAutoFinish();
        }
        #endregion

        #region HaiteiRaoyue
        [TestMethod]
        public async Task SuccessHaiteiRaoyue() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m455667999s")
                    .AddCalled("111s", 0, 2))
                .WithWall(wall => wall.Reserve("9m"))
                .Build(1)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertAction<PlayTileAction>()
                .ApplyAction<TsumoAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos.AssertTsumo(1)
                .AssertScore(han: 1)
                .AssertYaku<HaiteiRaoyue>()
            ).Resolve();
        }

        [TestMethod]
        public async Task NoHaiteiRaoyueWithAnKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m455667999s")
                    .AddCalled("111s", 0, 2))
                .WithWall(wall => wall.Reserve("9s").AddRinshan("9m"))
                .Start(1);

            var inquiry = await scenario.WaitInquiry();

            scenario.ForceHaitei();

            inquiry.ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTiles<KanAction>("9999s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<TsumoAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(han: 1)
                .AssertYaku<RinshanKaihou>()
            ).Resolve();
        }


        private class IshiueSannenSetup : RiichiSetup {
            protected override void InitPatterns() {
                base.InitPatterns();
                AddStdPattern<IshiueSannen>();
            }
        }

        [TestMethod]
        public async Task IshiueSannenWithAnKan() {
            var scenario = new ScenarioBuilder()
                .WithConfig(config => config.Setup(
                    setup => setup.AddExtraStdPattern<IshiueSannen>()))
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m111455667999s"))
                .WithWall(wall => wall.Reserve("9s").AddRinshan("9m"))
                .Start(1)
                .WithPlayer(1,
                    player => player.hand.Riichi(player.hand.freeTiles[0], true)
                );

            var inquiry = await scenario.WaitInquiry();

            scenario.ForceHaitei();

            inquiry.ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTiles<KanAction>("9999s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ApplyAction<TsumoAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertTsumo(1)
                .AssertScore(yakuman: 1)
                .AssertYaku<IshiueSannen>()
            ).Resolve();
        }
        #endregion

        #region HouteiRaoyui
        [TestMethod]
        public async Task SuccessHouteiRaoyui() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m455667999s")
                    .AddCalled("111s", 0, 2))
                .WithWall(wall => wall.Reserve("9m"))
                .Build(2)
                .ForceHaitei()
                .Start();

            (await scenario.WaitInquiry()).ForPlayer(2,
                playerInquiry => playerInquiry.ChooseTile<PlayTileAction>("9m")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .AssertSkip()
                .ApplyAction<RonAction>()
                .AssertNoMoreActions()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(2, 1)
                .AssertScore(han: 1)
                .AssertYaku<HouteiRaoyui>()
            ).Resolve();
        }

        [TestMethod]
        public async Task SuccessHouteiRaoyuiWithAnKan() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => playerBuilder
                    .SetFreeTiles("9m455667999s")
                    .AddCalled("111s", 0, 2))
                .WithPlayer(2, playerBuilder => playerBuilder
                    .SetFreeTiles("2345678999m")
                    .AddCalled("111p", 0, 0))
                .WithWall(wall => wall.Reserve("9s").AddRinshan("8m").AddDoras("12z"))
                .Start(1);

            var inquiry = await scenario.WaitInquiry();

            scenario.ForceHaitei();

            inquiry.ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTiles<KanAction>("9999s")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => playerInquiry
                .ChooseTile<PlayTileAction>("8m")
            ).AssertAutoFinish();

            (await scenario.WaitInquiry()).ForPlayer(2, playerInquiry => playerInquiry.ApplyAction<RonAction>()
            ).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
                .AssertRon(1, 2)
                .AssertScore(han: 1)
                .AssertYaku<HouteiRaoyui>()
            ).Resolve();
        }
        #endregion
    }
}