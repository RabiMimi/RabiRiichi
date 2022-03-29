using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class SanshokuDoujunTest {
        protected StdPattern V { get; set; } = new SanshokuDoujun(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("11z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFuuro("123s", 0)
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("11z")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123m")
                .AddFree("111p")
                .AddFree("99m")
                .AddAgari("99s", "9s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("99m")
                .AddAgari("99s", "9s")
                .Resolve(false);
        }
    }
}
