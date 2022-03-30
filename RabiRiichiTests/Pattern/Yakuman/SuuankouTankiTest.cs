using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class SuuankouTankiTest {
        protected StdPattern V { get; set; } = new SuuankouTanki(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("444z")
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111z")
                .AddFree("2222z")
                .AddFree("3333z")
                .AddFree("4444z")
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("44z")
                .AddAgari("55z", "5z", true)
                .Resolve(false);
        }
    }
}
