using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core.Config;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class TanyaoTest {
        protected StdPattern V { get; set; } = new Tanyao(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            new StdTestBuilder(V)
                .AddFree("22s")
                .AddFree("33p")
                .AddFree("44m")
                .AddFree("22m")
                .AddFree("33s")
                .AddFree("44p")
                .AddAgari("8s", "8s")
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("123s")
                .AddFree("456s")
                .AddFree("789s")
                .AddFree("11m")
                .AddAgari("12s", "3s")
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("22s")
                .AddFree("33p")
                .AddFree("44m")
                .AddFree("22m")
                .AddFree("33s")
                .AddFree("44p")
                .AddAgari("1s", "1s")
                .Resolve(false);
            new StdTestBuilder(V)
                .WithConfig(config => config.agariOption &= ~AgariOption.Kuitan)
                .AddCalled("234s", 0)
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
