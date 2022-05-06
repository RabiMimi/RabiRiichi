using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class ToitoiTest {
        protected StdPattern V { get; set; } = new Toitoi(null);

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
                .AddCalled("111s", 0)
                .AddCalled("111p", 1)
                .AddCalled("111m", 2)
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddCalled("111p", 0)
                .AddCalled("1111m", 1)
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
                .AddFree("111p")
                .AddFree("111m")
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(false);
        }
    }
}
