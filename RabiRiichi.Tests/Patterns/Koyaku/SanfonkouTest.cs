using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class SanfonkouTest {
    protected StdPattern V { get; set; } = new Sanfonkou(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("333z")
          .AddFree("456m")
          .AddAgari("99p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      new StdTestBuilder(V)
          .AddCalled("111z", 0)
          .AddCalled("2222z", 1)
          .AddFree("444z")
          .AddFree("456m")
          .AddAgari("99p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestResolvedOnSuushiiShapes() {
      // 小四喜/大四喜的牌型本身也满足三風刻，由上位役负责移除
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("333z")
          .AddFree("44z")
          .AddAgari("78p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("333z")
          .AddFree("444z")
          .AddAgari("9p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 只有两组风刻
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("456m")
          .AddFree("789m")
          .AddAgari("9p", "9p")
          .Resolve(false);
      // 三元刻不算风刻
      new StdTestBuilder(V)
          .AddFree("555z")
          .AddFree("666z")
          .AddFree("777z")
          .AddFree("456m")
          .AddAgari("99p", "9p")
          .Resolve(false);
    }
  }
}
