using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class KoyakuYakumanTest {
    [TestMethod]
    public void TestSuurenkou() {
      new StdTestBuilder(new Suurenkou(null, new Sanrenkou(null)))
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("555m")
          .AddAgari("11z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestIsshokuYondoujun() {
      new StdTestBuilder(new IsshokuYondoujun(null, new IsshokuSandoujun(null), new Iipeikou(null)))
          .AddFree("123m")
          .AddFree("123m")
          .AddFree("123m")
          .AddFree("123m")
          .AddAgari("11z", "1z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestRenhou() {
      var builder = new StdTestBuilder(new Renhou(null))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z", isTsumo: false);
      builder.mockGame.Object.info.dealer = 1; // Dealer is player 1, winner is player 0
      foreach (var player in builder.mockGame.Object.players) {
        player.hand ??= new Hand { player = player };
      }
      builder.Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaisharin() {
      new StdTestBuilder(new Daisharin(null, new Chiitoitsu(null)))
          .AddFree("22p")
          .AddFree("33p")
          .AddFree("44p")
          .AddFree("55p")
          .AddFree("66p")
          .AddFree("77p")
          .AddAgari("88p", "8p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaichikurin() {
      new StdTestBuilder(new Daichikurin(null, new Chiitoitsu(null)))
          .AddFree("22s")
          .AddFree("33s")
          .AddFree("44s")
          .AddFree("55s")
          .AddFree("66s")
          .AddFree("77s")
          .AddAgari("88s", "8s")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaisuurin() {
      new StdTestBuilder(new Daisuurin(null, new Chiitoitsu(null)))
          .AddFree("22m")
          .AddFree("33m")
          .AddFree("44m")
          .AddFree("55m")
          .AddFree("66m")
          .AddFree("77m")
          .AddAgari("88m", "8m")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestShiisanputa() {
      new StdTestBuilder(new Shiisanputa(null))
          .AddFree("1m").AddFree("4m").AddFree("7m")
          .AddFree("2p").AddFree("5p").AddFree("8p")
          .AddFree("3s").AddFree("6s").AddFree("9s")
          .AddFree("1z").AddFree("2z").AddFree("3z")
          .AddAgari("4z", "4z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestShiisuuputa() {
      new StdTestBuilder(new Shiisuuputa(null))
          .AddFree("1m").AddFree("4m").AddFree("7m")
          .AddFree("2p").AddFree("5p").AddFree("8p")
          .AddFree("3s").AddFree("6s").AddFree("9s")
          .AddFree("1z").AddFree("2z").AddFree("3z").AddFree("4z")
          .AddAgari("", "5z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestPaarenchan() {
      var builder = new StdTestBuilder(new Paarenchan(null))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z");
      builder.mockGame.Object.info.dealer = 0; // Winner is dealer
      builder.mockGame.Object.info.honba = 8;
      builder.Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestBenikujaku() {
      new StdTestBuilder(new Benikujaku(null))
          .AddFree("111s")
          .AddFree("555s")
          .AddFree("777s")
          .AddFree("999s")
          .AddAgari("77z", "7z") // Chun is 7z
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestHeiiisou() {
      new StdTestBuilder(new Heiiisou(null))
          .AddFree("222s")
          .AddFree("444s")
          .AddFree("888s")
          .AddFree("111z") // Ton
          .AddAgari("22z", "2z") // Nan
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestDaichiishin() {
      new StdTestBuilder(new Daichiishin(null, new Chiitoitsu(null), new Tsuuiisou(null, null)))
          .AddFree("11z")
          .AddFree("22z")
          .AddFree("33z")
          .AddFree("44z")
          .AddFree("55z")
          .AddFree("66z")
          .AddAgari("77z", "7z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestIshiNoUeNiMoSannen() {
      var builder = new StdTestBuilder(new IshiNoUeNiMoSannen(null, new DoubleRiichi(null, null), new HaiteiRaoyue(null), new HouteiRaoyui(null)))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z", isTsumo: true)
          .ForceWRiichi(true);
      builder.MockWall(wall => wall.SetIsHaitei());
      builder.Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }

    [TestMethod]
    public void TestKatengecchi() {
      var builder = new StdTestBuilder(new Katengecchi(null, new RinshanKaihou(null)))
          .AddFree("123m")
          .AddFree("456p")
          .AddFree("789s")
          .AddFree("11z")
          .AddAgari("22z", "2z", isTsumo: true, tileSource: TileSource.Wanpai);
      builder.MockWall(wall => wall.SetIsHaitei());
      builder.Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 1)
          .NoMore();
    }
  }
}
