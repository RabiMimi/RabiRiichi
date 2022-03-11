using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class 役牌白Test {
        protected StdPattern V { get; set; } = new 役牌白(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("55z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("555z")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("123s")
                .AddAgari("5z", "5z")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("66z", "6z")
                .Resolve(false);
        }
    }
}
