using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class IsshokuSandoujunTest {
    protected StdPattern V { get; set; } = new IsshokuSandoujun(null, null);

    [TestMethod]
    public void TestResolvedMenzen() {
      new StdTestBuilder(V)
          .AddFree("234m")
          .AddFree("234m")
          .AddFree("234m")
          .AddFree("567p")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 3)
          .NoMore();
    }

    [TestMethod]
    public void TestResolvedFuuro() {
      // 副露减一番
      new StdTestBuilder(V)
          .AddCalled("234m", 0)
          .AddFree("234m")
          .AddFree("234m")
          .AddFree("567p")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只有两组相同顺子（一盃口）
      new StdTestBuilder(V)
          .AddFree("234m")
          .AddFree("234m")
          .AddFree("567p")
          .AddFree("567p")
          .AddAgari("9s", "9s")
          .Resolve(false);
      // 三组同花色但不同序数
      new StdTestBuilder(V)
          .AddFree("234m")
          .AddFree("345m")
          .AddFree("456m")
          .AddFree("567p")
          .AddAgari("9s", "9s")
          .Resolve(false);
      // 三组同序数但不同花色（三色同順）
      new StdTestBuilder(V)
          .AddFree("234m")
          .AddFree("234p")
          .AddFree("234s")
          .AddFree("567p")
          .AddAgari("9s", "9s")
          .Resolve(false);
    }
  }
}
