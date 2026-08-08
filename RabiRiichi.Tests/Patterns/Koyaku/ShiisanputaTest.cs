using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  /// <summary> 牌型合法性由<see cref="BaseButa"/>保证，这两个役种只区分有没有对子 </summary>
  [TestClass]
  public class ShiisanputaTest {
    /// <summary> 12张互不关联的单牌 </summary>
    private static StdTestBuilder Scattered(StdPattern pattern) {
      return new StdTestBuilder(pattern)
          .AddFree("1m").AddFree("4m").AddFree("7m")
          .AddFree("1p").AddFree("4p").AddFree("7p")
          .AddFree("1s").AddFree("4s").AddFree("7s")
          .AddFree("1z").AddFree("2z").AddFree("3z");
    }

    [TestMethod]
    public void TestShiisanputa() {
      // 12张单牌 + 一组对子
      Scattered(new Shiisanputa(null))
          .AddAgari("4z", "4z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestShiisanputaFailed() {
      // 全是单牌，没有对子，那是十四不塔
      Scattered(new Shiisanputa(null))
          .AddFree("4z")
          .AddAgari("", "5z")
          .Resolve(false);
    }

    [TestMethod]
    public void TestShiisuuputa() {
      // 13张单牌 + 第14张单牌
      Scattered(new Shiisuuputa(null))
          .AddFree("4z")
          .AddAgari("", "5z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestShiisuuputaFailed() {
      // 有对子，那是十三不塔
      Scattered(new Shiisuuputa(null))
          .AddAgari("4z", "4z")
          .Resolve(false);
    }
  }
}
