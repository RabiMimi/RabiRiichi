using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class ShiiaruRaotaiTest {
        protected StdPattern V { get; set; } = new ShiiaruRaotai(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddCalled("234s", 0)
                .AddCalled("333p", 1)
                .AddCalled("789m", 2)
                .AddCalled("2222m", 3)
                .AddAgari("2s", "2s", true)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();

            new StdTestBuilder(V)
                .AddCalled("234s", 0)
                .AddCalled("333p", 1)
                .AddCalled("789m", 2)
                .AddCalled("2222m", 3)
                .AddAgari("2s", "2s", false)
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddCalled("234s", 0)
                .AddCalled("333p", 1)
                .AddFree("789m")
                .AddCalled("2222m", 2)
                .AddAgari("2s", "2s", true)
                .Resolve(false);

            new StdTestBuilder(V)
                .AddFree("234s")
                .AddCalled("333p", 1)
                .AddCalled("789m", 0)
                .AddCalled("2222m", 2)
                .AddAgari("2s", "2s", false)
                .Resolve(false);
        }
    }
}
