using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class PinfuTest {
        protected StdPattern V { get; set; } = new Pinfu(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("23s", "1s", true)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .ExpectScoring(ScoringType.Fu, 20)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("23s", "1s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .ExpectScoring(ScoringType.Fu, 30)
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
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("23s", "1s", true)
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("123m")
                .AddAgari("2s", "2s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("13s", "2s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("66z")
                .AddAgari("23s", "1s")
                .Resolve(false);
        }
    }
}
