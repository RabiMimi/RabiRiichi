using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame.Listener;
using RabiRiichi.Tests.Helper;

namespace RabiRiichi.Tests.Events {
  /// <summary> 八连荘的连庄和了计数 </summary>
  [TestClass]
  public class AgariStreakTest {
    private static Game CreateGame(int dealer) {
      var mockGame = new RabiMockGame();
      var game = mockGame.Object;
      game.info.dealer = dealer;
      foreach (var player in game.players) {
        player.Reset();
      }
      return game;
    }

    /// <summary> 标记某玩家和了 </summary>
    private static void SetAgari(Game game, int playerId) {
      game.GetPlayer(playerId).hand.agariTile = new GameTile(new Tile("1m"), -1);
    }

    [TestMethod]
    public void DealerAgariIncrementsStreak() {
      var game = CreateGame(dealer: 0);
      game.GetPlayer(0).agariStreak = 3;
      SetAgari(game, 0);

      NextGameListener.UpdateAgariStreak(game, isRyuukyoku: false);

      Assert.AreEqual(4, game.GetPlayer(0).agariStreak);
      Assert.AreEqual(0, game.GetPlayer(1).agariStreak);
    }

    [TestMethod]
    public void NonDealerAgariResetsStreak() {
      var game = CreateGame(dealer: 0);
      game.GetPlayer(0).agariStreak = 5;
      SetAgari(game, 1);

      NextGameListener.UpdateAgariStreak(game, isRyuukyoku: false);

      Assert.AreEqual(0, game.GetPlayer(0).agariStreak, "dealer lost the streak");
      // 闲家和了不累计连庄
      Assert.AreEqual(0, game.GetPlayer(1).agariStreak);
    }

    [TestMethod]
    public void RyuukyokuResetsStreak() {
      // 即使是听牌连庄，八连荘也要从头数起
      var game = CreateGame(dealer: 0);
      game.GetPlayer(0).agariStreak = 7;

      NextGameListener.UpdateAgariStreak(game, isRyuukyoku: true);

      Assert.AreEqual(0, game.GetPlayer(0).agariStreak);
    }

    [TestMethod]
    public void DealerAgariWhileNotDealerSeatZero() {
      var game = CreateGame(dealer: 1);
      game.GetPlayer(1).agariStreak = 1;
      SetAgari(game, 1);

      NextGameListener.UpdateAgariStreak(game, isRyuukyoku: false);

      Assert.AreEqual(2, game.GetPlayer(1).agariStreak);
      Assert.AreEqual(0, game.GetPlayer(0).agariStreak);
    }
  }
}
