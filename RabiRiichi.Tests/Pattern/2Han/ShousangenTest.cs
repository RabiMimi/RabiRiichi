using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichi.Tests.Pattern {
    [TestClass]
    public class ShousangenTest {
        protected StdPattern V { get; set; } = new Shousangen(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("555z")
                .AddFree("666z")
                .AddFree("111m")
                .AddFree("77z")
                .AddAgari("99p", "9p")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("555z")
                .AddFree("666z")
                .AddFree("111m")
                .AddFree("99p")
                .AddAgari("77z", "7z")
                .Resolve(false);
        }
    }
}
