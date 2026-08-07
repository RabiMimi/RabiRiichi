using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class SanrenkouTest {
    protected StdPattern V { get; set; } = new Sanrenkou(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("555m")
          .AddFree("789p")
          .AddAgari("1s", "1s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestResolvedWithFuuroAndKan() {
      new StdTestBuilder(V)
          .AddCalled("777s", 0)
          .AddCalled("8888s", 1)
          .AddFree("999s")
          .AddFree("123m")
          .AddAgari("1p", "1p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 花色不同
      new StdTestBuilder(V)
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("555p")
          .AddFree("789p")
          .AddAgari("1s", "1s")
          .Resolve(false);
      // 序数不连续
      new StdTestBuilder(V)
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("666m")
          .AddFree("789p")
          .AddAgari("1s", "1s")
          .Resolve(false);
      // 顺子而非刻子
      new StdTestBuilder(V)
          .AddFree("345m")
          .AddFree("345m")
          .AddFree("345m")
          .AddFree("789p")
          .AddAgari("1s", "1s")
          .Resolve(false);
      // 只有两组连续刻子
      new StdTestBuilder(V)
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("789p")
          .AddFree("123s")
          .AddAgari("1s", "1s")
          .Resolve(false);
    }
  }
}
