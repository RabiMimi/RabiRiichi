using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class DaisuushiiTest {
        protected StdPattern V { get; set; } = new Daisuushii(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("444z")
                .AddAgari("5z", "5z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("1111z")
                .AddFree("2222z")
                .AddFree("3333z")
                .AddFree("4444z")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("4444z")
                .AddAgari("4z", "4z")
                .Resolve(true)
                .ExpectScoring(ScoringType.Yakuman, 2)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("123s")
                .AddAgari("4z", "4z")
                .Resolve(false);
        }
    }
}
