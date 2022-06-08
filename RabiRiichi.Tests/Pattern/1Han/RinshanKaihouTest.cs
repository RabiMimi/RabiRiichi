using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichi.Tests.Pattern {
    [TestClass]
    public class RinshanKaihouTest {
        protected StdPattern V { get; set; } = new RinshanKaihou(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("4444m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true, TileSource.Wanpai)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("4444m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true)
                .Resolve(false);
        }
    }
}
