using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class HonroutouTest {
        protected StdPattern V { get; set; } = new Honroutou(null, null);

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
                .AddFree("111s")
                .AddFree("111p")
                .AddFree("111m")
                .AddFree("99s")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("11s")
                .AddFree("11p")
                .AddFree("11m")
                .AddFree("99s")
                .AddFree("99p")
                .AddFree("99m")
                .AddAgari("1z", "1z")
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
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("111p")
                .AddFree("222m")
                .AddFree("99s")
                .AddAgari("99p", "9p")
                .Resolve(false);
        }

    }
}
