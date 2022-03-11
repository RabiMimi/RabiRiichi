using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class 平和Test {
        protected StdPattern V { get; set; } = new 平和(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("23s", "1s")
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
                .AddFree("11m")
                .AddAgari("12s", "3s")
                .Resolve(false)
                .NoMore();
        }
    }
}
