using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class JunchanTaiyaoTest {
        protected StdPattern V { get; set; } = new JunchanTaiyao(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("11s")
                .AddAgari("78p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFuuro("123m", 0)
                .AddFree("11s")
                .AddAgari("78p", "9p")
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
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123p")
                .AddFree("123m")
                .AddFree("11z")
                .AddAgari("78p", "9p")
                .Resolve(false);
        }
    }
}
