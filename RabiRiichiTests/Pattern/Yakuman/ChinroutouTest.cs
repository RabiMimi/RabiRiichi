using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class ChinroutouTest {
        protected StdPattern V { get; set; } = new Chinroutou(null, null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("111p")
                .AddFree("111m")
                .AddFree("99p")
                .AddAgari("99m", "9m", true)
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111s")
                .AddFree("1111p")
                .AddFree("1111m")
                .AddFree("9999p")
                .AddAgari("9m", "9m", true)
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }
    }
}
