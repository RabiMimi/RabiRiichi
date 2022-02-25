using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class 断幺九Test {
        protected StdPattern V { get; set; } = new 断幺九();

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1);
            new StdTestBuilder(V)
                .AddFree("22s")
                .AddFree("33p")
                .AddFree("44m")
                .AddFree("22m")
                .AddFree("33s")
                .AddFree("44p")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1);
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
                .AddFree("22s")
                .AddFree("33p")
                .AddFree("44m")
                .AddFree("22m")
                .AddFree("33s")
                .AddFree("44p")
                .AddAgari("1s", "1s")
                .Resolve(false);
        }
    }
}
