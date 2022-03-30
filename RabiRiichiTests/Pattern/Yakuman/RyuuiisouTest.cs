using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class RyuuiisouTest {
        protected StdPattern V { get; set; } = new Ryuuiisou(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("222s")
                .AddFree("666s")
                .AddFree("333s")
                .AddFree("44s")
                .AddAgari("88s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("22r2s")
                .AddFree("666s")
                .AddFree("333s")
                .AddFree("44s")
                .AddAgari("88s", "8s")
                .Resolve(false);
        }
    }
}
