using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Patterns;
using RabiRiichi.Tests.Helper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class BaseButaTest {
    private readonly BaseButa V = new();
    private List<List<MenLike>> groupList;

    /// <summary> 在起手状态的对局中解析不塔牌型 </summary>
    private bool Resolve(string handStr, string incoming, bool isTsumo = true, Action<Game> setup = null) {
      var mockGame = new RabiMockGame();
      var game = mockGame.Object;
      foreach (var p in game.players) {
        p.Reset();
        p.hand.jun = 1;
      }
      var player = game.players[0];
      player.hand.freeTiles = new Tiles(handStr).ToGameTileList();
      setup?.Invoke(game);
      var tile = new GameTile(new Tile(incoming), -1) {
        player = player,
        discardInfo = isTsumo ? null : new DiscardInfo(game.players[1], DiscardReason.Draw, 0),
      };
      return V.Resolve(player.hand, tile, out groupList);
    }

    [TestMethod]
    public void TestShiisanputa() {
      // 13张互不关联的牌，第14张与其中一张成对
      Assert.IsTrue(Resolve("147m147p147s1234z", "4z"));
      var groups = groupList.Single();
      Assert.AreEqual(1, groups.Count(gr => gr is Jantou));
      Assert.AreEqual(12, groups.Count(gr => gr is Musou));
    }

    [TestMethod]
    public void TestShiisuuputa() {
      // 14张全部互不关联
      Assert.IsTrue(Resolve("147m147p147s1234z", "5z"));
      var groups = groupList.Single();
      Assert.AreEqual(14, groups.Count(gr => gr is Musou));
    }

    [TestMethod]
    public void TestRelatedTiles() {
      // 1m和2m能组成搭子
      Assert.IsFalse(Resolve("127m147p147s1234z", "5z"));
      // 1m和3m能组成嵌张
      Assert.IsFalse(Resolve("137m147p147s1234z", "5z"));
      // 4m和7m相差3，互不关联
      Assert.IsTrue(Resolve("147m147p147s1234z", "5z"));
    }

    [TestMethod]
    public void TestTwoPairs() {
      // 两组对子不成立
      Assert.IsFalse(Resolve("1147m1447p147s12z", "3z"));
    }

    [TestMethod]
    public void TestTriplet() {
      Assert.IsFalse(Resolve("1114m147p147s123z", "5z"));
    }

    [TestMethod]
    public void TestRon() {
      // 荣和不成立，必须是配牌或第一次摸牌
      Assert.IsFalse(Resolve("147m147p147s1234z", "5z", isTsumo: false));
    }

    [TestMethod]
    public void TestNotFirstJun() {
      Assert.IsFalse(Resolve("147m147p147s1234z", "5z",
          setup: game => game.players[1].hand.jun = 2));
      Assert.IsFalse(Resolve("147m147p147s1234z", "5z",
          setup: game => game.players[1].hand.menzen = false));
    }

    [TestMethod]
    public void TestFuuro() {
      Assert.IsFalse(Resolve("147m147p1234z", "5z",
          setup: game => game.players[0].hand.called.Add(
              new Tiles("123s").ToMenLike())));
    }

    [TestMethod]
    public void TestWrongTileCount() {
      Assert.IsFalse(Resolve("147m147p147s123z", "5z"));
    }

    [TestMethod]
    public void TestShantenAlwaysRejects() {
      // 不塔只在起手成立，不参与向听数计算
      var hand = TestHelper.CreateHand("147m147p147s1234z");
      Assert.AreEqual(int.MaxValue, V.Shanten(hand, null, out var tiles));
      Assert.IsNull(tiles);
    }
  }
}
