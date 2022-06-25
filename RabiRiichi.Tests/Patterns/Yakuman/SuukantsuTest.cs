using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class SuukantsuTest {
        protected StdPattern V { get; set; } = new Suukantsu(null, null, null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("1111z")
                .AddFree("2222z")
                .AddFree("3333z")
                .AddFree("4444z")
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddCalled("1111z", 0)
                .AddCalled("2222z", 1)
                .AddCalled("3333z", 2)
                .AddCalled("4444z", 3)
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("1111z")
                .AddFree("2222z")
                .AddFree("3333z")
                .AddFree("55z")
                .AddAgari("44z", "4z")
                .Resolve(false);
        }
    }
}
