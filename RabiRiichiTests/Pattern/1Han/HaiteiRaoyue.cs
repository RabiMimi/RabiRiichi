using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class HaiteiRaoyueTest {
        protected StdPattern V { get; set; } = new HaiteiRaoyue(null);

        [TestMethod]
        public void TestResolved() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true)
                .MockWall(wall => wall.SetIsHaitei())
                .Resolve(true)
                .ExpectScoring(ScoringType.Han, 1)
                .NoMore();
            // TODO(Frenqy): Add More
        }

        [TestMethod]
        public void TestFailed() {
            new StdTestBuilder(V)
                .AddFree("234s")
                .AddFree("345p")
                .AddFree("456m")
                .AddFree("22m")
                .AddAgari("23s", "4s", true)
                .Resolve(false);
            // TODO(Frenqy): Add More
        }
    }
}
