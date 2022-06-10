using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class RyanpeikouTest {
        protected StdPattern V { get; set; } = new Ryanpeikou(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("45m", "6m")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("22m")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddCalled("456m", 0)
                .AddFree("22m")
                .AddAgari("45m", "6m")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("22m")
                .AddAgari("23m", "4m")
                .Resolve(false);
        }
    }
}
