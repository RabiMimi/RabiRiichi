using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class HeiiisouTest {
    protected StdPattern V { get; set; } = new Heiiisou(null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("222p")
          .AddFree("444p")
          .AddFree("888p")
          .AddFree("111z")
          .AddAgari("2z", "2z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
      // 可副露
      new StdTestBuilder(V)
          .AddCalled("222p", 0)
          .AddCalled("4444p", 1)
          .AddFree("333z")
          .AddFree("444z")
          .AddAgari("8p", "8p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 含非黑色筒子
      new StdTestBuilder(V)
          .AddFree("222p")
          .AddFree("333p")
          .AddFree("888p")
          .AddFree("111z")
          .AddAgari("2z", "2z")
          .Resolve(false);
      // 含三元牌
      new StdTestBuilder(V)
          .AddFree("222p")
          .AddFree("444p")
          .AddFree("888p")
          .AddFree("555z")
          .AddAgari("2z", "2z")
          .Resolve(false);
      // 索子不算（黑一色是筒子）
      new StdTestBuilder(V)
          .AddFree("222s")
          .AddFree("444s")
          .AddFree("888s")
          .AddFree("111z")
          .AddAgari("2z", "2z")
          .Resolve(false);
    }
  }
}
