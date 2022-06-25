using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Core;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class MenzenchinTsumohouTest {
        protected StdPattern V { get; set; } = new MenzenchinTsumohou(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
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
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddCalled("345p", 0)
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
