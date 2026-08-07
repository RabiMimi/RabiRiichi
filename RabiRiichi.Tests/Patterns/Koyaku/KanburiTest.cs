using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class KanburiTest {
    protected StdPattern V { get; set; } = new Kanburi(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", reason: DiscardReason.DrawRinshan)
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 普通摸牌后的舍牌
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", reason: DiscardReason.Draw)
          .Resolve(false);
      // 副露后的舍牌
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", reason: DiscardReason.Pon)
          .Resolve(false);
      // 自摸岭上牌是嶺上開花，不是槓振り
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", true, TileSource.Wanpai)
          .Resolve(false);
    }
  }
}
