using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
  [TestClass]
  public class DaichiishinTest {
    protected StdPattern V { get; set; } = new Daichiishin(null, null, null);

    [TestMethod]
    public void TestResolved() {
      new StdTestBuilder(V)
          .AddFree("11z")
          .AddFree("22z")
          .AddFree("33z")
          .AddFree("44z")
          .AddFree("55z")
          .AddFree("66z")
          .AddAgari("7z", "7z")
          .Resolve(true)
          .ExpectScoring(ScoringType.Yakuman, 2)
          .NoMore();
    }

    // 字一色由依赖保证，此处只测门前清；牌型见 ScenarioKoyaku.DaichiishinRemovesTsuuiisou
    [TestMethod]
    public void TestFailed() {
      new StdTestBuilder(V)
          .AddFree("11z")
          .AddFree("22z")
          .AddFree("33z")
          .AddFree("44z")
          .AddFree("55z")
          .AddFree("66z")
          .ForceMenzen(false)
          .AddAgari("7z", "7z")
          .Resolve(false);
    }
  }
}
