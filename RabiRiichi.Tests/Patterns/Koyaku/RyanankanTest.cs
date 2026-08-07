using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class RyanankanTest {
    protected StdPattern V { get; set; } = new Ryanankan(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddFree("345s")
          .AddFree("678s")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      // 其余部分副露也成立
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddCalled("345s", 0)
          .AddFree("678s")
          .AddAgari("9s", "9s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只有一个暗杠
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddFree("222p")
          .AddFree("345s")
          .AddFree("678s")
          .AddAgari("9s", "9s")
          .Resolve(false);
      // 一暗杠一明杠
      new StdTestBuilder(V)
          .AddCalled("1111m", -1)
          .AddCalled("2222p", 0)
          .AddFree("345s")
          .AddFree("678s")
          .AddAgari("9s", "9s")
          .Resolve(false);
      // 暗刻不是暗杠
      new StdTestBuilder(V)
          .AddFree("111m")
          .AddFree("222p")
          .AddFree("345s")
          .AddFree("678s")
          .AddAgari("9s", "9s")
          .Resolve(false);
    }
  }
}
