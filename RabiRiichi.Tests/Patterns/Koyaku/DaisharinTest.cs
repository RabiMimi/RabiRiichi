using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  /// <summary> 大車輪/大竹林/大数隣共用<see cref="DaisharinLike"/> </summary>
  [TestClass]
  public class DaisharinTest {
    private static StdTestBuilder Chiitoi(StdPattern pattern, string suit) {
      return new StdTestBuilder(pattern)
          .AddFree("22" + suit)
          .AddFree("33" + suit)
          .AddFree("44" + suit)
          .AddFree("55" + suit)
          .AddFree("66" + suit)
          .AddFree("77" + suit);
    }

    [TestMethod]
    public void TestDaisharin() {
      Chiitoi(new Daisharin(null, null, null, null), "p")
          .AddAgari("8p", "8p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaichikurin() {
      Chiitoi(new Daichikurin(null, null, null, null), "s")
          .AddAgari("8s", "8s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaisuurin() {
      Chiitoi(new Daisuurin(null, null, null, null), "m")
          .AddAgari("8m", "8m")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestAkadora() {
      // 赤五筒不影响判定
      new StdTestBuilder(new Daisharin(null, null, null, null))
          .AddFree("22p")
          .AddFree("33p")
          .AddFree("44p")
          .AddFree("05p")
          .AddFree("66p")
          .AddFree("77p")
          .AddAgari("8p", "8p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestFailed() {
      // 花色不符
      Chiitoi(new Daisharin(null, null, null, null), "s")
          .AddAgari("8s", "8s")
          .Resolve(false);
      // 含幺九牌
      new StdTestBuilder(new Daisharin(null, null, null, null))
          .AddFree("22p")
          .AddFree("33p")
          .AddFree("44p")
          .AddFree("55p")
          .AddFree("66p")
          .AddFree("77p")
          .AddAgari("9p", "9p")
          .Resolve(false);
      // 门前清限定
      Chiitoi(new Daisharin(null, null, null, null), "p")
          .ForceMenzen(false)
          .AddAgari("8p", "8p")
          .Resolve(false);
    }
  }
}
