using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class PaarenchanTest {
    protected StdPattern V { get; set; } = new Paarenchan(null);

    [TestMethod]
    public void TestResolved() {
      // 之前已连续和了7次，本局是第8次
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .MockPlayer(player => player.agariStreak = Paarenchan.REQUIRED_STREAK - 1)
          .AddAgari("23s", "4s")
          .Resolve(true)
          .ExpectScoring(ScoringType.BonusYakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只连续和了6次，本局是第7次
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .MockPlayer(player => player.agariStreak = Paarenchan.REQUIRED_STREAK - 2)
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 闲家不成立（八连荘是庄家连庄）
      new StdTestBuilder(V)
          .MockGame(game => game.Object.info.dealer = 1)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .MockPlayer(player => player.agariStreak = Paarenchan.REQUIRED_STREAK - 1)
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 首次和了
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s")
          .Resolve(false);
    }
  }
}
