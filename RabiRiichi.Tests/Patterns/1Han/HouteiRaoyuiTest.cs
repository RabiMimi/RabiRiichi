using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns;

namespace RabiRiichi.Tests.Patterns {
    [TestClass]
    public class HouteiRaoyuiTest {
        protected StdPattern V { get; set; } = new HouteiRaoyui(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .MockWall(wall => wall.SetIsHaitei())
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true)
                .MockWall(wall => wall.SetIsHaitei())
                .Resolve(false);
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s")
                .Resolve(false);
        }
    }
}
