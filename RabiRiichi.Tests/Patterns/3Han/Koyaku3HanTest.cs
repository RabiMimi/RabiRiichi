using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class Koyaku3HanTest {
    [TestMethod]
    public void TestIsshokuSandoujun() {
      new StdTestBuilder(new IsshokuSandoujun(null))
          .AddFree("123m")
          .AddFree("123m")
          .AddFree("123m")
          .AddFree("11z")
          .AddAgari("12p", "3p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 3)
          .NoMore();
    }

    [TestMethod]
    public void TestChinpeikou() {
      new StdTestBuilder(new Chinpeikou(null, new Ryanpeikou(null, null), new Iipeikou(null)))
          .AddFree("123m")
          .AddFree("123m")
          .AddFree("456m")
          .AddFree("456m")
          .AddAgari("11z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 3)
          .NoMore();
    }
  }
}
