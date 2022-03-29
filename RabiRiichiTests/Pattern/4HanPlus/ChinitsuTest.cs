using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class ChinitsuTest {
        protected StdPattern V { get; set; } = new Chinitsu(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("234s")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 6)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFuuro("234s", 0)
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 5)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("11z")
                .AddAgari("12s", "3s")
                .Resolve(false);
        }
    }
}
