using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class IttsuTest {
        protected StdPattern V { get; set; } = new Ittsu(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "1s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123p")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddCalled("123s", 0)
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddCalled("123s", 0)
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("22m")
                .AddAgari("22s", "2s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456p")
                .AddFree("789m")
                .AddFree("11m")
                .AddAgari("12s", "3s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("777s")
                .AddFree("11m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
