using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class ChaopaikouTest {
    protected StdPattern V { get; set; } = new Chaopaikou(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("222s")
          .AddFree("444s")
          .AddFree("666s")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      // 5-7-9 也成立
      new StdTestBuilder(V)
          .AddCalled("555p", 0)
          .AddFree("777p")
          .AddFree("999p")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 公差为1（三連刻）
      new StdTestBuilder(V)
          .AddFree("222s")
          .AddFree("333s")
          .AddFree("444s")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(false);
      // 公差不一致
      new StdTestBuilder(V)
          .AddFree("222s")
          .AddFree("444s")
          .AddFree("777s")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(false);
      // 花色不同
      new StdTestBuilder(V)
          .AddFree("222s")
          .AddFree("444s")
          .AddFree("666p")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(false);
    }
  }
}
