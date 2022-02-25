using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class 一气通贯Test {
        protected StdPattern V { get; set; } = new 一气通贯();

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "1s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2);
            new StdTestBuilder(V)
                .AddFree("123p")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2);
            new StdTestBuilder(V)
                .AddFuuro("123s", 0)
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1);
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456p")
                .AddFree("789m")
                .AddFree("11m")
                .AddAgari("12s", "3s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("777s")
                .AddFree("11m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
