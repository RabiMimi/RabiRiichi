using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class ChantaiyaoTest {
        protected StdPattern V { get; set; } = new Chantaiyao(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("11z")
                .AddAgari("78p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddCalled("123m", 0)
                .AddFree("11z")
                .AddAgari("78p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("99m")
                .AddAgari("78p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
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
                .AddFree("99m")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("99m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
