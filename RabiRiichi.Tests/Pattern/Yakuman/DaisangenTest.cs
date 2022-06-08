using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichi.Tests.Pattern {
    [TestClass]
    public class DaisangenTest {
        protected StdPattern V { get; set; } = new Daisangen(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("555z")
                .AddFree("666z")
                .AddFree("777z")
                .AddFree("567s")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("5555z")
                .AddFree("6666z")
                .AddFree("7777z")
                .AddFree("567s")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("5555z")
                .AddFree("6666z")
                .AddFree("7777z")
                .AddFree("567s")
                .AddAgari("7z", "7z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("555z")
                .AddFree("666z")
                .AddFree("234s")
                .AddFree("567s")
                .AddAgari("7z", "7z")
                .Resolve(false);
        }
    }
}
