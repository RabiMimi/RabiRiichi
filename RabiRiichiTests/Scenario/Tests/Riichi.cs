using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using RabiRiichi.Pattern;
using RabiRiichiTests.Helper;
using System.Linq;
using System.Threading.Tasks;


namespace RabiRiichiTests.Scenario.Tests {
    [TestClass]
    public class ScenarioRiichi {
        [TestMethod]
        public async Task SuccessRiichi() {
            var scenario = new ScenarioBuilder()
                .WithPlayer(1, playerBuilder => {
                    playerBuilder.SetFreeTiles("12366s234m34566p");
                })
                .WithWall(wall => wall.Reserve("7r5s6p").AddUradoras("5s"))
                .Start(1);

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTile<RiichiAction>("7s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RiichiEvent>((ev) => {
                Assert.AreEqual(TileSource.Discard, ev.tile.source);
                Assert.AreEqual(DiscardReason.Draw, ev.reason);
                ev.tile.tile.AssertEquals("7s");
                return true;
            });

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).Finish();

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertSkip()
                    .ApplyAction<RonAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(3, 1)
                    .AssertScore(han: 4, fu: 40)
                    .AssertYaku<Riichi>()
                    .AssertYaku<Ippatsu>()
                    .AssertYaku<Uradora>(han: 2);
                return true;
            }).Resolve();
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

            (await scenario.WaitInquiry()).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTile<RiichiAction>("7s")
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RiichiEvent>((ev) => {
                Assert.AreEqual(TileSource.Discard, ev.tile.source);
                Assert.AreEqual(DiscardReason.Draw, ev.reason);
                ev.tile.tile.AssertEquals("7s");
                return true;
            });

            (await scenario.WaitPlayerTurn(1)).ForPlayer(1, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>(action => {
                        action.options
                            .OfType<ChooseTileActionOption>()
                            .Select(o => o.tile)
                            .ToTiles()
                            .AssertEquals("6p");
                        return true;
                    })
                    .ApplyAction<TsumoAction>()
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertTsumo(1)
                    .AssertScore(han: 4, fu: 30)
                    .AssertYaku<DoubleRiichi>()
                    .AssertYaku<Ippatsu>()
                    .AssertYaku<MenzenchinTsumohou>();
                return true;
            }).Resolve();
        }
    }
}