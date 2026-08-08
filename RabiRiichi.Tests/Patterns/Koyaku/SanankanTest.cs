using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class SanankanTest {
    protected StdPattern V { get; set; } = new Sanankan(null, null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddCalled("3333s", -1)
          .AddFree("456s")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 两暗杠一明杠
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddCalled("3333s", 0)
          .AddFree("456s")
          .AddAgari("9s", "9s")
          .Resolve(false);
      // 只有两个暗杠
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddFree("333s")
          .AddFree("456s")
          .AddAgari("9s", "9s")
          .Resolve(false);
    }
  }
}
