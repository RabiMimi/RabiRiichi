using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame;
using RabiRiichi.Patterns;
using System;
using System.Linq;
using System.Threading.Tasks;
using RabiRiichi.Generated.Communication.Sync;
using SyncGameState = RabiRiichi.Communication.Sync.GameState;

namespace RabiRiichi.Tests.Scenario.Tests {
  [TestClass]
  public class ScenarioAgari {
    #region Tsumo

    private static async Task<Scenario> BuildTsumo(int dealer = 0, Action<ScenarioBuilder> action = null) {
      var scenarioBuilder = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, dealer, 2).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithWall(wall => wall.Reserve("1s").AddDoras("2m"));
      action?.Invoke(scenarioBuilder);
      var scenario = scenarioBuilder.Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<TsumoAction>()
      ).AssertAutoFinish();

      return scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertTsumo(0)
          .AssertScore(4, 30)
          .AssertYaku<SanshokuDoujun>(han: 1)
          .AssertYaku<JunchanTaiyao>(han: 2)
          .AssertYaku<Dora>(han: 1)
      );
    }

    [TestMethod]
    public async Task DealerTsumo() {
      var scenario = await BuildTsumo();
      await scenario.AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(14300, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(0, ev.dealer);
        Assert.AreEqual(3, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(0, game.info.dealer);
        Assert.AreEqual(3, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task DealerTsumo_NoRenchan() {
      var scenario = await BuildTsumo(0, scenarioBuilder => {
        scenarioBuilder.WithConfig(config =>
            config.SetRenchanPolicy(RenchanPolicy.Default & ~RenchanPolicy.DealerWin));
      });

      await scenario.AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(14300, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(1, ev.dealer);
        Assert.AreEqual(0, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(1, game.info.dealer);
        Assert.AreEqual(0, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task NonDealerTsumo() {
      var scenario = await BuildTsumo(1);

      await scenario.AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(10500, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-4100, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(-2200, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(-2200, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(2, ev.dealer);
        Assert.AreEqual(0, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(2, game.info.dealer);
        Assert.AreEqual(0, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task Tsumo_SyncSnapshotContainsAgariScore() {
      // After a win, the score breakdown AND the point transfer must be
      // persisted on the hand and included in the sync snapshot, so a
      // reconnecting client can rebuild the result screen. Capture the snapshot
      // synchronously at ConcludeGameEvent time, while the winning hand is still
      // intact (BeginGameEvent, which resets it, runs later).
      var scenario = await BuildTsumo();

      PlayerHandStateMsg winnerMsg = null;
      PlayerHandStateMsg loserMsg = null;
      scenario.WithGame(game => {
        new EventListener<ConcludeGameEvent>(game.eventBus).EarlyExec(_ => {
          var winner = game.GetPlayer(0);
          Assert.IsNotNull(winner.hand.agariTile, "agariTile should be set");
          Assert.IsNotNull(winner.hand.agariScore, "agariScore should be set");
          var proto = new SyncGameState(game, 0).ToProto(0);
          winnerMsg = proto.Players.First(p => p.Id == 0).Hand;
          loserMsg = proto.Players.First(p => p.Id == 1).Hand;
          return System.Threading.Tasks.Task.CompletedTask;
        }, times: 1);
      });

      await scenario.AssertEvent<ConcludeGameEvent>().Resolve();

      Assert.IsNotNull(winnerMsg, "ConcludeGameEvent should have fired");
      Assert.IsNotNull(winnerMsg.AgariScore, "sync snapshot should carry agari score");
      Assert.IsNotNull(winnerMsg.AgariTile, "sync snapshot should carry agari tile");
      Assert.AreEqual(4, winnerMsg.AgariScore.Result.Han);
      Assert.AreEqual(30, winnerMsg.AgariScore.Result.Fu);

      // The net point delta is persisted too: winner positive, loser negative.
      Assert.IsTrue(winnerMsg.PointDelta > 0, "winner should have positive delta");
      Assert.IsTrue(loserMsg.PointDelta < 0, "loser should have negative delta");
    }
    #endregion

    #region Ron
    [TestMethod]
    public async Task DealerRon() {
      var scenario = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, 0, 2).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithWall(wall => wall.Reserve("1s").AddDoras("2m"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<RonAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertRon(1, 0)
          .AssertScore(4, 30)
          .AssertYaku<SanshokuDoujun>(han: 1)
          .AssertYaku<JunchanTaiyao>(han: 2)
          .AssertYaku<Dora>(han: 1)
      ).AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(14200, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-12200, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(0, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(0, ev.dealer);
        Assert.AreEqual(3, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(0, game.info.dealer);
        Assert.AreEqual(3, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task NonDealerRon() {
      var scenario = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, 1, 2).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithWall(wall => wall.Reserve("1s").AddDoras("2m"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => playerInquiry
          .ApplyAction<RonAction>()
      ).AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => ev.agariInfos
          .AssertRon(1, 0)
          .AssertScore(4, 30)
          .AssertYaku<SanshokuDoujun>(han: 1)
          .AssertYaku<JunchanTaiyao>(han: 2)
          .AssertYaku<Dora>(han: 1)
      ).AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(10300, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-8300, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(0, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(2, ev.dealer);
        Assert.AreEqual(0, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(2, game.info.dealer);
        Assert.AreEqual(0, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task MultipleRon() {
      var scenario = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, 0, 1).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("23466m789p22334s")
              .SetRiichiTile("5s"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithWall(wall => wall.Reserve("1s").AddDoras("2m").AddUradoras("1z"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(2, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .AssertAutoFinish();

      await scenario.AssertEvent<AgariEvent>(ev => {
        ev.agariInfos
            .AssertRon(1, 0)
            .AssertScore(3, 30)
            .AssertYaku<Riichi>()
            .AssertYaku<Pinfu>()
            .AssertYaku<Dora>(han: 1);
        ev.agariInfos
            .AssertRon(1, 2)
            .AssertScore(4, 30)
            .AssertYaku<SanshokuDoujun>(han: 1)
            .AssertYaku<JunchanTaiyao>(han: 2)
            .AssertYaku<Dora>(han: 1);
      }).AssertEvent<ApplyScoreEvent>(ev => {
        Assert.AreEqual(6100, ev.scoreChange.DeltaScore(0));
        Assert.AreEqual(-14100, ev.scoreChange.DeltaScore(1));
        Assert.AreEqual(10000, ev.scoreChange.DeltaScore(2));
        Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
      }).AssertEvent<BeginGameEvent>(ev => {
        Assert.AreEqual(0, ev.round);
        Assert.AreEqual(0, ev.dealer);
        Assert.AreEqual(2, ev.honba);
      })
      .Resolve();

      scenario.WithGame(game => {
        Assert.AreEqual(0, game.info.round);
        Assert.AreEqual(0, game.info.dealer);
        Assert.AreEqual(2, game.info.honba);
        Assert.AreEqual(0, game.info.riichiStick);
      });
    }

    [TestMethod]
    public async Task CannotRon_DiscardedBySelf() {
      var scenario = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, 0, 2).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithWall(wall => wall.Reserve("1s"))
          .Start(0);

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry => {
        playerInquiry.ChooseTile<PlayTileAction>("1s");
      }).AssertAutoFinish();

      (await scenario.WaitInquiry()).AssertNoActionForPlayer(0);
    }

    #endregion

    #region Sanchahou
    [TestMethod]
    public async Task Sanchahou_Ryuukyoku() {
      var scenario = new ScenarioBuilder()
          .WithState(state => state.SetRound(Wind.E, 0, 1).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("23466m789p22334s")
              .SetRiichiTile("5s"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithPlayer(3, playerBuilder => playerBuilder
              .SetFreeTiles("2344s")
              .AddCalled("666s", 1, 1)
              .AddCalled("777s", 0, 2)
              .AddCalled("789s", 0, 2))
          .WithWall(wall => wall.Reserve("1s"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(2, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(3, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .AssertAutoFinish();

      await scenario
          .AssertRyuukyoku<Sanchahou>()
          .AssertEvent<ApplyScoreEvent>(ev => {
            foreach (int p in Enumerable.Range(0, 4)) {
              Assert.AreEqual(0, ev.scoreChange.DeltaScore(p));
            }
          })
          .AssertEvent<BeginGameEvent>(ev => {
            Assert.AreEqual(0, ev.round);
            Assert.AreEqual(0, ev.dealer);
            Assert.AreEqual(2, ev.honba);
            Assert.AreEqual(2, ev.game.info.riichiStick);
          })
          .Resolve();
    }

    [TestMethod]
    public async Task NoSanchahou_DisabledInConfig() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config
              .SetRyuukyokuTrigger(RyuukyokuTrigger.Default & ~RyuukyokuTrigger.Sanchahou))
          .WithState(state => state.SetRound(Wind.E, 0, 1).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("23466m789p22334s")
              .SetRiichiTile("5s"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithPlayer(3, playerBuilder => playerBuilder
              .SetFreeTiles("2344s")
              .AddCalled("666s", 1, 1)
              .AddCalled("777s", 0, 2)
              .AddCalled("789s", 0, 2))
          .WithWall(wall => wall.Reserve("1s"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(2, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(3, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .AssertAutoFinish();

      await scenario.AssertNoRyuukyoku<Sanchahou>().Resolve();
    }
    #endregion

    #region First Winner
    [TestMethod]
    public async Task SuccessFirstWinner() {
      var scenario = new ScenarioBuilder()
          .WithConfig(config => config
              .SetAgariOption(AgariOption.Default | AgariOption.FirstWinner))
          .WithState(state => state.SetRound(Wind.E, 0, 1).SetRiichiStick(2))
          .WithPlayer(0, playerBuilder => playerBuilder
              .SetFreeTiles("23466m789p22334s")
              .SetRiichiTile("5s"))
          .WithPlayer(2, playerBuilder => playerBuilder
              .SetFreeTiles("123m123p1123s")
              .AddCalled("111m", 0, 2))
          .WithPlayer(3, playerBuilder => playerBuilder
              .SetFreeTiles("2344s")
              .AddCalled("666s", 1, 1)
              .AddCalled("777s", 0, 2)
              .AddCalled("789s", 0, 2))
          .WithWall(wall => wall.Reserve("1s").AddDoras("6z"))
          .Start(1);

      (await scenario.WaitInquiry()).Finish();

      (await scenario.WaitInquiry()).ForPlayer(0, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .ForPlayer(2, playerInquiry =>
          playerInquiry.ApplyAction<RonAction>())
      .AssertAutoFinish();

      await scenario
          .AssertNoRyuukyoku<Sanchahou>()
          .AssertEvent<AgariEvent>(ev => ev.agariInfos
              .AssertRon(1, 2)
              .AssertScore(han: 3, fu: 30)
              .AssertYaku<JunchanTaiyao>()
              .AssertYaku<SanshokuDoujun>(han: 1)
          )
          .AssertEvent<ApplyScoreEvent>(ev => {
            Assert.AreEqual(0, ev.scoreChange.DeltaScore(0));
            Assert.AreEqual(-4200, ev.scoreChange.DeltaScore(1));
            Assert.AreEqual(6200, ev.scoreChange.DeltaScore(2));
            Assert.AreEqual(0, ev.scoreChange.DeltaScore(3));
          })
          .AssertEvent<BeginGameEvent>(ev => {
            Assert.AreEqual(0, ev.round);
            Assert.AreEqual(1, ev.dealer);
            Assert.AreEqual(0, ev.honba);
            Assert.AreEqual(0, ev.game.info.riichiStick);
          })
          .Resolve();
    }
    #endregion
  }
}