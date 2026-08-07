using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class BenikujakuTest {
    protected StdPattern V { get; set; } = new Benikujaku(null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("555s")
          .AddFree("777s")
          .AddFree("999s")
          .AddAgari("7z", "7z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
      // 赤五索也计入，且可副露
      new StdTestBuilder(V)
          .AddCalled("055s", 0)
          .AddFree("111s")
          .AddFree("777s")
          .AddFree("777z")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 含非红色索子
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("222s")
          .AddFree("777s")
          .AddFree("999s")
          .AddAgari("7z", "7z")
          .Resolve(false);
      // 發不是红色
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("555s")
          .AddFree("777s")
          .AddFree("666z")
          .AddAgari("9s", "9s")
          .Resolve(false);
    }
  }
}
