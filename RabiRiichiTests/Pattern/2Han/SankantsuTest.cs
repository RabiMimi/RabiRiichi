using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class SankantsuTest {
        protected StdPattern V { get; set; } = new Sankantsu(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFree("1111p")
                .AddFree("1111m")
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFree("1111p")
                .AddFree("1111m")
                .AddFree("1111z")
                .AddAgari("9p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFuuro("1111s", 0)
                .AddFuuro("1111p", 1)
                .AddFuuro("1111m", 2)
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFree("1111p")
                .AddFree("111m")
                .AddFree("99m")
                .AddAgari("99s", "9s")
                .Resolve(false);
        }
    }
}
