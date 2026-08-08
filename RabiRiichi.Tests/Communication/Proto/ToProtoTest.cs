using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Communication.Proto;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using RabiRiichi.Events.InGame;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Events.InGame;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using RabiRiichi.Tests.Scenario;
using RabiRiichi.Communication.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Tests.Communication.Proto {

  [TestClass]
  public class ToProtoTest {
    [TestMethod]
    public void TestToProtoForAllClasses() {
      var types = typeof(Game).Assembly.GetTypes()
          .Where(t => t.IsClass && !t.IsAbstract && t.GetMethod("ToProto") != null)
          .ToList();
      var game = new Game(new GameConfig {
        actionCenter = new JsonStringActionCenter(null),
      });
      var tile = new Tile("1s");
      var gameTile = new GameTile(tile, -1);
      game.wall.Reset();
      foreach (var player in game.players) {
        player.Reset();
      }
      game.info.Reset();
      var parameters = new List<object> {
                game,
                game.initialEvent,
                (Kan)new Tiles("1111s").ToMenLike(),
                (Kou)new Tiles("222s").ToMenLike(),
                (Shun)new Tiles("345s").ToMenLike(),
                (Jantou)new Tiles("33s").ToMenLike(),
                (Musou)new Tiles("3s").ToMenLike(),
                true,
                0,
                0L,
                game.config.scoringOption,
                ScoringType.None,
                DiscardReason.None,
                ConcludeGameReason.None,
                ScoreTransferReason.None,
                TileSource.None,
                game.Get<Tanyao>(),
                tile,
                gameTile,
                game.Dealer,
                game.Dealer.hand,
                game.wall,
                Array.Empty<AgariInfo>(),
                Array.Empty<int>(),
                new ScoreTransferList(),
                new List<GameTile>(),
                new List<List<GameTile>>(),
                new List<GameTileMsg>(),
            };
      while (types.Count > 0) {
        bool hasProgress = false;
        foreach (var type in types.ToArray()) {
          var instance = parameters.FirstOrDefault(p => p.GetType() == type);
          if (instance == null) {
            var constructors = type.GetConstructors();
            var validConstructor = constructors.FirstOrDefault(
                c => c.GetParameters().All(p => parameters.Any(pa => pa.GetType().IsAssignableTo(p.ParameterType))));
            if (validConstructor == null) {
              continue;
            }
            instance = validConstructor.Invoke(
                [.. validConstructor.GetParameters()
                    .Select(p => parameters
                        .First(pa => pa.GetType().IsAssignableTo(p.ParameterType)))]);
          }

          hasProgress = true;

          var method = type.GetMethod("ToProto");
          var toProtoParams = method.GetParameters();
          if (toProtoParams.Length == 0) {
            var proto = method.Invoke(instance, null);
            Assert.IsNotNull(proto, $"{type.Name} has a ToProto method that returns null");
          } else {
            var args = toProtoParams.Select(p =>
                parameters.FirstOrDefault(pa => pa.GetType().IsAssignableTo(p.ParameterType))).ToArray();
            var proto = method.Invoke(instance, args);
            Assert.IsNotNull(proto, $"{type.Name} has a ToProto method that returns null.");
          }

          parameters.Add(instance);
          types.Remove(type);
        }

        if (!hasProgress) {
          Assert.Fail($"No valid constructor for:\n{string.Join("\n", types.Select(t => t.Name))}");
          break;
        }
      }
    }

    [TestMethod]
    public async Task TestPlayerHandStateTenpaiWaitsSerialization() {
      Game game = null;
      var scenario = new ScenarioBuilder()
          .WithPlayer(1, playerBuilder => {
            // A hand that is already in tenpai (waiting on 6s and 6p)
            playerBuilder.SetFreeTiles("12366s234m34566p");
          })
          .Start(1);

      scenario.WithGame(g => game = g);

      // Trigger game startup so player states are active
      await scenario.WaitInquiry();

      var player1 = game.GetPlayer(1);

      // Verify that the hand is indeed in Tenpai
      Assert.IsTrue(player1.hand.isFuriten || player1.hand.Tenpai.Count > 0, "Player 1 should be in tenpai");
      Assert.AreEqual(2, player1.hand.Tenpai.Count);

      // Construct PlayerHandState for Player 1
      var handState = new PlayerHandState(player1.hand, 1);

      // CASE 1: ToProto for the player themselves (1)
      var protoSelf = handState.ToProto(1);
      Assert.IsNotNull(protoSelf);
      Assert.AreEqual(2, protoSelf.TenpaiWaits.Count, "Should serialize tenpai waits for self");

      var wait6s = protoSelf.TenpaiWaits.FirstOrDefault(w => w.WinningTile == new Tile("6s").Val);
      Assert.IsNotNull(wait6s, "6s should be in the waits list");

      var wait6p = protoSelf.TenpaiWaits.FirstOrDefault(w => w.WinningTile == new Tile("6p").Val);
      Assert.IsNotNull(wait6p, "6p should be in the waits list");

      // CASE 2: ToProto for opponent player (e.g. 0)
      var protoOpponent = handState.ToProto(0);
      Assert.IsNotNull(protoOpponent);
      Assert.AreEqual(0, protoOpponent.TenpaiWaits.Count, "Should NOT leak tenpai waits to opponents");
    }

    [TestMethod]
    public async Task TestGameStateHidesOpponentConcealedHand() {
      // Regression: GameState.ToProto must hide every OTHER player's concealed
      // freeTiles from the receiver. A previous bug passed the receiver id (not
      // the hand owner's id) into PlayerHandState, making `reveal` always true
      // and leaking all hands to everyone.
      // Give the OPPONENT (seat 0) a fixed 13-tile concealed hand and start at
      // seat 1 (so the non-dealer seat 0 keeps exactly 13 tiles, no draw).
      Game game = null;
      var scenario = new ScenarioBuilder()
          .WithPlayer(0, pb => pb.SetFreeTiles("1223344556677s"))
          .Start(1);
      scenario.WithGame(g => game = g);
      await scenario.WaitInquiry();

      // Serialize the whole game state from seat 1's perspective.
      var state = new GameState(game, 1).ToProto(1);

      var self = state.Players.First(p => p.Id == 1);
      var opponent = state.Players.First(p => p.Id == 0);
      int emptyTile = Tile.Empty.Val;

      // Own hand is fully visible.
      Assert.IsTrue(self.Hand.FreeTiles.Count > 0);
      Assert.IsTrue(self.Hand.FreeTiles.All(t => t.Tile != emptyTile),
          "Own concealed tiles must be visible to self");

      // Opponent's concealed hand: traceIds present, faces hidden.
      Assert.IsTrue(opponent.Hand.FreeTiles.Count > 0);
      Assert.IsTrue(opponent.Hand.FreeTiles.All(t => t.Tile == emptyTile),
          "Opponent concealed tiles must be hidden (face 0)");
      Assert.IsTrue(opponent.Hand.FreeTiles.All(t => t.TraceId > 0),
          "Opponent tiles still carry their real per-round traceId");

      // God view sees everything.
      var godState = new GameState(game, ProtoConverters.GOD_VIEW_PLAYER_ID)
          .ToProto(ProtoConverters.GOD_VIEW_PLAYER_ID);
      var godOpponent = godState.Players.First(p => p.Id == 1);
      Assert.IsTrue(godOpponent.Hand.FreeTiles.All(t => t.Tile != emptyTile),
          "God view reveals all hands");
    }
  }
}