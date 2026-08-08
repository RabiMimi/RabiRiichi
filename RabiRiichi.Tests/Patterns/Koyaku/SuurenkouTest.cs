using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class SuurenkouTest {
    protected StdPattern V { get; set; } = new Suurenkou(null, null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("111m")
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("444m")
          .AddAgari("1s", "1s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
      new StdTestBuilder(V)
          .AddCalled("666p", 0)
          .AddCalled("777p", 1)
          .AddCalled("8888p", 2)
          .AddFree("999p")
          .AddAgari("1s", "1s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只有三連刻
      new StdTestBuilder(V)
          .AddFree("111m")
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("789p")
          .AddAgari("1s", "1s")
          .Resolve(false);
      // 第四组不连续
      new StdTestBuilder(V)
          .AddFree("111m")
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("555m")
          .AddAgari("1s", "1s")
          .Resolve(false);
      // 跨花色
      new StdTestBuilder(V)
          .AddFree("111m")
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("444p")
          .AddAgari("1s", "1s")
          .Resolve(false);
    }
  }
}
