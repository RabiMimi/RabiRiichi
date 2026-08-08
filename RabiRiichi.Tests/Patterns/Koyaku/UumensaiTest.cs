using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class UumensaiTest {
    protected StdPattern V { get; set; } = new Uumensai(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("123m")
          .AddFree("123p")
          .AddFree("123s")
          .AddFree("111z")
          .AddAgari("5z", "5z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      new StdTestBuilder(V)
          .AddCalled("123m", 0)
          .AddCalled("555z", 1)
          .AddFree("789p")
          .AddFree("22z")
          .AddAgari("78s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 缺三元牌
      new StdTestBuilder(V)
          .AddFree("123m")
          .AddFree("123p")
          .AddFree("123s")
          .AddFree("111z")
          .AddAgari("2z", "2z")
          .Resolve(false);
      // 缺风牌
      new StdTestBuilder(V)
          .AddFree("123m")
          .AddFree("123p")
          .AddFree("123s")
          .AddFree("555z")
          .AddAgari("6z", "6z")
          .Resolve(false);
      // 缺索子
      new StdTestBuilder(V)
          .AddFree("123m")
          .AddFree("123p")
          .AddFree("789p")
          .AddFree("111z")
          .AddAgari("5z", "5z")
          .Resolve(false);
    }
  }
}
