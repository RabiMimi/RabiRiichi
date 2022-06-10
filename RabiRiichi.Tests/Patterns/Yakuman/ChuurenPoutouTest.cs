using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class ChuurenPoutouTest {
        protected StdPattern V { get; set; } = new ChuurenPoutou(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("999s")
                .AddFree("234s")
                .AddFree("567s")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("999s")
                .AddFree("88s")
                .AddFree("567s")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddAgari("9s", "9s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFree("999s")
                .AddFree("234s")
                .AddFree("567s")
                .AddAgari("8s", "8s")
                .Resolve(false);
        }
    }
}
