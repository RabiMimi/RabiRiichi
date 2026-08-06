using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class Koyaku2HanTest {
    [TestMethod]
    public void TestShousanfon() {
      new StdTestBuilder(new Shousanfon(null))
          .AddFree("111z") // Ton
          .AddFree("222z") // Nan
          .AddFree("33z")  // Sha (pair)
          .AddFree("123m")
          .AddAgari("12p", "3p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestSanrenkou() {
      new StdTestBuilder(new Sanrenkou(null))
          .AddFree("222m")
          .AddFree("333m")
          .AddFree("444m")
          .AddFree("11z")
          .AddAgari("12p", "3p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestSanfonkou() {
      new StdTestBuilder(new Sanfonkou(null))
          .AddFree("111z") // Ton
          .AddFree("222z") // Nan
          .AddFree("333z") // Sha
          .AddFree("11m")
          .AddAgari("12p", "3p")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestSanankan() {
      new StdTestBuilder(new Sanankan(null, new Ryanankan(null)))
          .AddCalled("1111m", -1)
          .AddCalled("2222p", -1)
          .AddCalled("3333s", -1)
          .AddFree("11z")
          .AddAgari("22m", "2m")
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }

    [TestMethod]
    public void TestUumensai() {
      new StdTestBuilder(new Uumensai(null))
          .AddFree("123m") // Man
          .AddFree("456p") // Pin
          .AddFree("789s") // Sou
          .AddFree("111z") // Wind (Ton)
          .AddAgari("55z", "5z") // Dragon (Haku pair)
          .Resolve(true)
          .ExpectScoring(ScoringType.Han, 2)
          .NoMore();
    }
  }
}
