using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class HonitsuTest {
        protected StdPattern V { get; set; } = new Honitsu(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11z")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddCalled("789s", 0)
                .AddFree("11z")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 2)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("55s")
                .AddAgari("12s", "3s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("111s")
                .AddFree("222s")
                .AddFree("333s")
                .AddFree("55s")
                .AddAgari("44s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 3)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("123s")
                .AddFree("11z")
                .AddAgari("12p", "3p")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("111z")
                .AddFree("222z")
                .AddFree("333z")
                .AddFree("55z")
                .AddAgari("44z", "4z")
                .Resolve(false);
        }
    }
}
