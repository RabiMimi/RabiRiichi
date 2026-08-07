using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class KatengecchiTest {
    // 嶺上開花本身由依赖（RinshanKaihou）保证，这里只测"岭上牌是最后一张牌"
    protected StdPattern V { get; set; } = new Katengecchi(null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", true, TileSource.Wanpai)
          .MockWall(wall => wall.SetIsHaitei())
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 牌山还有牌
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
