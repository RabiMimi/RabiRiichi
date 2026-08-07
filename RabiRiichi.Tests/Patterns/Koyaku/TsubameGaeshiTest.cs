using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class TsubameGaeshiTest {
    protected StdPattern V { get; set; } = new TsubameGaeshi(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", isRiichiDeclaration: true)
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 普通的舍牌
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s")
          .Resolve(false);
      // 自摸
      new StdTestBuilder(V)
          .AddFree("234s")
          .AddFree("345p")
          .AddFree("456m")
          .AddFree("22m")
          .AddAgari("23s", "4s", true)
          .Resolve(false);
    }
  }
}
