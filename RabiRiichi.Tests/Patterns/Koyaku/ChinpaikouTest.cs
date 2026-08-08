using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class ChinpaikouTest {
    protected StdPattern V { get; set; } = new Chinpaikou(null);

    [TestMethod]
    public void TestResolved() {
      // 147
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("444s")
          .AddFree("777s")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      // 369，含副露与杠
      new StdTestBuilder(V)
          .AddCalled("333p", 0)
          .AddCalled("6666p", 1)
          .AddFree("999p")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 148 不成筋
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("444s")
          .AddFree("888s")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(false);
      // 跨花色
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("444s")
          .AddFree("777p")
          .AddFree("789m")
          .AddAgari("1z", "1z")
          .Resolve(false);
      // 只有两组
      new StdTestBuilder(V)
          .AddFree("111s")
          .AddFree("444s")
          .AddFree("789m")
          .AddFree("123p")
          .AddAgari("1z", "1z")
          .Resolve(false);
    }
  }
}
