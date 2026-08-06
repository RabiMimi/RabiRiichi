using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class Koyaku1HanTest {
    [TestMethod]
    public void TestTsubameGaeshi() {
      var builder = new StdTestBuilder(new TsubameGaeshi(null))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z");
      builder.anotherPlayer.Object.hand = new Hand {
        player = builder.anotherPlayer.Object,
        riichiTile = builder.incoming
      };
      builder.Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestKanburi() {
      new StdTestBuilder(new Kanburi(null))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z", reason: DiscardReason.DrawRinshan)
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestRyanankan() {
      new StdTestBuilder(new Ryanankan(null))
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddFree("789s")
          .AddAgari("11z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestChaopaikou() {
      new StdTestBuilder(new Chaopaikou(null))
          .AddFree("222m")
          .AddFree("444m")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("11p", "1p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestChinpaikou() {
      new StdTestBuilder(new Chinpaikou(null))
          .AddFree("111m")
          .AddFree("444m")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("11p", "1p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestShiiaruRaotai() {
      new StdTestBuilder(new ShiiaruRaotai(null))
          .AddCalled("123m", 0)
          .AddCalled("456p", 0)
          .AddCalled("789s", 0)
          .AddCalled("111z", 0)
          .AddAgari("2z", "2z", isTsumo: true, tileSource: TileSource.Wanpai)
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 1)
          .NoMore();
    }
  }
}
