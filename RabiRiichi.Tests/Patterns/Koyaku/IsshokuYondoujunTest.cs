using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class IsshokuYondoujunTest {
    protected StdPattern V { get; set; } = new IsshokuYondoujun(null, null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("234p")
          .AddAgari("6z", "6z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
      new StdTestBuilder(V)
          .AddCalled("234p", 0)
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("234p")
          .AddAgari("6z", "6z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只有三组
      new StdTestBuilder(V)
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("567m")
          .AddAgari("6z", "6z")
          .Resolve(false);
      // 二盃口但不是四组同顺
      new StdTestBuilder(V)
          .AddFree("234p")
          .AddFree("234p")
          .AddFree("567m")
          .AddFree("567m")
          .AddAgari("6z", "6z")
          .Resolve(false);
    }
  }
}
