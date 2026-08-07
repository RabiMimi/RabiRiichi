using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class ShousanfonTest {
    protected StdPattern V { get; set; } = new Shousanfon(null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("33z")
          .AddFree("456m")
          .AddAgari("78p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
      new StdTestBuilder(V)
          .AddCalled("444z", 0)
          .AddCalled("222z", 1)
          .AddFree("11z")
          .AddFree("456m")
          .AddAgari("78p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestResolvedOnShousuushiiShape() {
      // 小四喜的牌型也满足小三風，由 Shousuushii 负责移除
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("333z")
          .AddFree("44z")
          .AddAgari("78p", "9p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 大四喜没有风牌雀头
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("333z")
          .AddFree("444z")
          .AddAgari("9p", "9p")
          .Resolve(false);
      // 雀头不是风牌
      new StdTestBuilder(V)
          .AddFree("111z")
          .AddFree("222z")
          .AddFree("456m")
          .AddFree("99p")
          .AddAgari("78p", "9p")
          .Resolve(false);
      // 三元牌不算风牌（这是小三元）
      new StdTestBuilder(V)
          .AddFree("555z")
          .AddFree("666z")
          .AddFree("77z")
          .AddFree("456m")
          .AddAgari("78p", "9p")
          .Resolve(false);
    }
  }
}
