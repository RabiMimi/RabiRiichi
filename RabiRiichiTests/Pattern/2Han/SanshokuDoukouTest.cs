using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class SanshokuDoukouTest {
        protected StdPattern V { get; set; } = new SanshokuDoukou(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("111p")
                .AddFree("111m")
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFuuro("111s", 0)
                .AddFuuro("111p", 1)
                .AddFuuro("111m", 2)
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFuuro("111p", 0)
                .AddFuuro("1111m", 1)
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123m")
                .AddFree("123p")
                .AddFree("99m")
                .AddAgari("99s", "9s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("111s")
                .AddFree("111s")
                .AddFree("99m")
                .AddAgari("99s", "9s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("111p")
                .AddFree("111m")
                .AddFree("11s")
                .AddAgari("99p", "9p")
                .Resolve(false);
        }
    }
}