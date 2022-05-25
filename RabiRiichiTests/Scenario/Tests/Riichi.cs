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
        private static async Task RiichiWith(Scenario scenario, int playerId, string tile) {
            (await scenario.WaitInquiry()).ForPlayer(playerId, playerInquiry => {
                playerInquiry
                    .AssertAction<PlayTileAction>()
                    .ChooseTile<RiichiAction>(tile)
                    .AssertNoMoreActions();
            }).AssertAutoFinish();

            scenario.AssertEvent<RiichiEvent>((ev) => {
                Assert.AreEqual(TileSource.Discard, ev.tile.source);
                Assert.AreEqual(DiscardReason.Draw, ev.reason);
                ev.tile.tile.AssertEquals(tile);
                return true;
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

            await RiichiWith(scenario, 1, "7s");

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

            await scenario.AssertEvent<AgariEvent>((ev) => {
                ev.agariInfos
                    .AssertRon(2, 1)
                    .AssertScore(han: 1, fu: 40)
                    .AssertYaku<Riichi>();
                return true;
            }).Resolve();
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
                        return true;
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
                return true;
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
                return true;
            }).Resolve();
        }
    }
}