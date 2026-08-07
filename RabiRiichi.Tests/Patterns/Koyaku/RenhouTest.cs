using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class RenhouTest {
    protected StdPattern V { get; set; } = new Renhou(null);

    /// <summary> 默认庄家是0号玩家，改为1号以让被测玩家成为闲家 </summary>
    private static StdTestBuilder Builder(StdPattern pattern) {
      return new StdTestBuilder(pattern)
          .MockGame(game => game.Object.info.dealer = 1)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m");
    }

    [TestMethod]
    public void TestResolved() {
      Builder(V)
          .AddAgari("23s", "4s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 自摸不是人和（那是地和）
      Builder(V)
          .AddAgari("23s", "4s", true)
          .Resolve(false);
      // 庄家不能人和
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 他家已经摸过第二张牌，不再是第一巡
      Builder(V)
          .MockGame(game => game.Object.players[1].hand.jun = 2)
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 他家已经鸣牌
      Builder(V)
          .MockGame(game => game.Object.players[1].hand.menzen = false)
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 他家开杠或拔北同样会推进巡目，由上面的 jun 用例覆盖
    }
  }
}
