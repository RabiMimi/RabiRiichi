using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class ChinpeikouTest {
    protected StdPattern V { get; set; } = new Chinpeikou(null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("789m")
          .AddFree("789m")
          .AddFree("789s")
          .AddFree("789s")
          .AddAgari("1p", "1p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 3)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 同花色的二盃口
      new StdTestBuilder(V)
          .AddFree("789m")
          .AddFree("789m")
          .AddFree("123m")
          .AddFree("123m")
          .AddAgari("1p", "1p")
          .Resolve(false);
      // 异色但序数不同
      new StdTestBuilder(V)
          .AddFree("789m")
          .AddFree("789m")
          .AddFree("123s")
          .AddFree("123s")
          .AddAgari("1p", "1p")
          .Resolve(false);
      // 四组完全相同的顺子（一色四同順）
      new StdTestBuilder(V)
          .AddFree("789m")
          .AddFree("789m")
          .AddFree("789m")
          .AddFree("789m")
          .AddAgari("1p", "1p")
          .Resolve(false);
      // 门前清限定
      new StdTestBuilder(V)
          .AddCalled("789m", 0)
          .AddFree("789m")
          .AddFree("789s")
          .AddFree("789s")
          .AddAgari("1p", "1p")
          .Resolve(false);
    }
  }
}
