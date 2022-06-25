using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class ShousuushiiTest {
        protected StdPattern V { get; set; } = new Shousuushii(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("44z")
                .AddAgari("55z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("55z")
                .AddAgari("44z", "4z")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("111z")
                .AddFree("111z")
                .AddFree("11z")
                .AddAgari("11z", "1z")
                .Resolve(false);
        }
    }
}
