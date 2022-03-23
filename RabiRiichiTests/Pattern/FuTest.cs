using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Fu33332Test {
        protected StdPattern V { get; set; } = new Fu33332(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddAgari("4z", "4z")
                .AddFree("333p")
                .AddFree("234p")
                .AddFree("8888s")
                .AddFuuro("5555p", 0)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 50);

            new StdTestBuilder(V)
                .AddAgari("7z", "7z", true)
                .AddFree("666p")
                .AddFuuro("456m", 0)
                .AddFuuro("555m", 0)
                .AddFuuro("7777p", 3)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 40);

            new StdTestBuilder(V)
                .AddAgari("8m", "8m", true)
                .AddFuuro("567p", 0)
                .AddFuuro("222m", 2)
                .AddFuuro("567s", 0)
                .AddFuuro("3333z", 2)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 50);
        }
    }
}