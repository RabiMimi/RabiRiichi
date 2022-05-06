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
                .AddCalled("5555p", 0)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 50);

            new StdTestBuilder(V)
                .AddAgari("7z", "7z", true)
                .AddFree("666p")
                .AddCalled("456m", 0)
                .AddCalled("555m", 0)
                .AddCalled("7777p", 3)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 40);

            new StdTestBuilder(V)
                .AddAgari("8m", "8m", true)
                .AddCalled("567p", 0)
                .AddCalled("222m", 2)
                .AddCalled("567s", 0)
                .AddCalled("3333z", 2)
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 50);

            new StdTestBuilder(V)
                .AddAgari("8p", "8p")
                .AddFree("333p")
                .AddFree("222s")
                .AddFree("234p")
                .AddFree("4444m")
                .Resolve(true)
                .ExpectScoring(ScoringType.Fu, 60);
        }
    }
}