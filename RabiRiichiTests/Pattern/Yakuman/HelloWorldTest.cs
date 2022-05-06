using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class HelloWorldTest {
        protected StdPattern V { get; set; } = new HelloWorld(null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("222s")
                .AddFree("666s")
                .AddFree("678s")
                .AddFree("77s")
                .AddAgari("99s", "9s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 3)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddCalled("222s", 0)
                .AddFree("666s")
                .AddFree("678s")
                .AddFree("77s")
                .AddAgari("99s", "9s")
                .Resolve(false);
        }
    }
}
