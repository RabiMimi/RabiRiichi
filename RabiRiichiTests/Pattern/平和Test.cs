using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class 平和Test {
        protected StdPattern V { get; set; } = new 平和();

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFreeGroup("123s")
                .AddFreeGroup("456s")
                .AddFreeGroup("789s")
                .AddFreeGroup("11m")
                .AddAgari("23s", "1s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1);
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFreeGroup("123s")
                .AddFreeGroup("456s")
                .AddFreeGroup("789s")
                .AddFreeGroup("11m")
                .AddAgari("12s", "3s")
                .Resolve(false);
        }
    }
}
