using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Communication.Sync;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Scenario;
using System.Linq;

namespace RabiRiichi.Tests.Actions {
  [TestClass]
  public class TenpaiPredictionTest {
    private static Game RiichiGame(string uradoras = "467s56z") {
      Game game = null;
      new ScenarioBuilder()
          .WithPlayer(1, player => player
              .SetFreeTiles("223344s678s45s66s")
              .SetRiichiTile("9m", false))
          .WithWall(wall => wall
              .AddDoras("1s1234z")
              .AddUradoras(uradoras)
              .SetRevealedDoraCount(3)
              .SetRevealedUradoraCount(3))
          .Build(1)
          .WithGame(g => game = g);
      return game;
    }

    [DataTestMethod]
    [DataRow("3s")]
    [DataRow("6s")]
    public void RiichiPreviewExcludesUradoraFromRonAndTsumo(string winningTile) {
      var game = RiichiGame();
      var hand = game.GetPlayer(1).hand;
      var resolver = game.Get<PatternResolver>();
      var tile = new Tile(winningTile);
      var ronTile = new GameTile(tile, 0) {
        source = TileSource.Discard,
        discardInfo = new DiscardInfo(null, DiscardReason.Draw, 0),
      };
      var tsumoTile = new GameTile(tile, 0) { source = TileSource.Wall };

      // Public han: chinitsu 6 + tanyao 1 + pinfu 1 + iipeikou 1 + riichi 1
      // + dora 2 = 12. The three hidden ura hits count only on an actual win.
      var ron = resolver.ResolveMaxScore(hand, ronTile, PatternMask.All);
      var tsumo = resolver.ResolveMaxScore(hand, tsumoTile, PatternMask.All);
      Assert.AreEqual(15, ron.result.han);
      Assert.AreEqual(16, tsumo.result.han);
      Assert.AreEqual(3, ron.Single(score => score.Source is Uradora).Val);
      Assert.AreEqual(3, tsumo.Single(score => score.Source is Uradora).Val);

      var bonus = resolver.ResolveMaxScore(hand, ronTile, PatternMask.Bonus);
      Assert.AreEqual(5, bonus.result.han, "Bonus selection must still include dora and uradora");
      Assert.AreEqual(0, bonus.result.yaku, "Bonus han must not satisfy the yaku requirement");

      var preview = PlayTileAction.ComputeTenpaiInfo(resolver, hand, tile);
      Assert.AreEqual(12, preview.han, "Hidden ura must not confirm a counted yakuman on ron");
      Assert.AreEqual(13, preview.maxHan, "Only menzen tsumo should raise the preview ceiling");
      Assert.AreEqual(10, preview.yaku);
      Assert.AreEqual(0, preview.yakuman);
      Assert.AreEqual(6000L, preview.points, "The guaranteed score must remain sanbaiman");
    }

    [TestMethod]
    public void RiichiSyncWaitsDoNotDependOnHiddenUradora() {
      var withUra = RiichiGame().GetPlayer(1).hand;
      var withoutUra = RiichiGame("456z17m").GetPlayer(1).hand;
      var withUraWaits = new PlayerHandState(withUra, 1).ToProto(1).TenpaiWaits;
      var withoutUraWaits = new PlayerHandState(withoutUra, 1).ToProto(1).TenpaiWaits;

      Assert.AreEqual(3, withUraWaits.Count);
      CollectionAssert.AreEqual(withoutUraWaits, withUraWaits,
          "Changing hidden indicators must not change any serialized tenpai score");
    }
  }
}