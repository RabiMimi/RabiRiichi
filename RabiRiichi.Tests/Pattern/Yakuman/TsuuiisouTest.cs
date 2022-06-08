using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichi.Tests.Pattern {
    [TestClass]
    public class TsuuiisouTest {
        protected StdPattern V { get; set; } = new Tsuuiisou(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("444z")
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("11z")
                .AddFree("22z")
                .AddFree("33z")
                .AddFree("44z")
                .AddFree("55z")
                .AddFree("66z")
                .AddAgari("7z", "7z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("55s")
                .AddAgari("44z", "4z")
                .Resolve(false);
        }
    }
}
