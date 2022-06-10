using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class IipeikouTest {
        protected StdPattern V { get; set; } = new Iipeikou(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("22m")
                .AddAgari("23m", "4m")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("22m")
                .AddAgari("23s", "1s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddCalled("456m", 0)
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
