using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class ChankanTest {
        protected StdPattern V { get; set; } = new Chankan(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "1s", false, reason: DiscardReason.Chankan)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("11s", "1s", true)
                .Resolve(false);
        }
    }
}
