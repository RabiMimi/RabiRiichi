using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class PatternTest {
        private MenOrJantou Create(string tiles) => MenOrJantou.From(new Tiles(tiles));

        [TestMethod]
        public void TestFrom() {
            Assert.IsTrue(Create("9s") is Musou);
            Assert.IsTrue(Create("999s") is Kou);
            Assert.IsTrue(Create("9999s") is Kan);
            Assert.IsTrue(Create("789s") is Shun);
            Assert.IsTrue(Create("99s") is Jantou);
        }

        [TestMethod]
        public void TestIsSame() {
            // Kan and Kou
            var kou1 = Create("111p");
            var kan1 = Create("1111p");
            var kou2 = Create("222p");
            var kan2 = Create("2222p");
            Assert.IsTrue(kou1.IsSame(kou1));
            Assert.IsTrue(kou1.IsSame(kan1));
            Assert.IsTrue(kan1.IsSame(kou1));
            Assert.IsTrue(kan1.IsSame(kan1));
            Assert.IsFalse(kou1.IsSame(kou2));
            Assert.IsFalse(kou1.IsSame(kan2));
            Assert.IsFalse(kan1.IsSame(kou2));
            Assert.IsFalse(kan1.IsSame(kan2));

            // Shun
            var shun1 = Create("123p");
            var shun2 = Create("123s");
            Assert.IsTrue(shun1.IsSame(shun1));
            Assert.IsFalse(shun1.IsSame(shun2));
            Assert.IsFalse(shun1.IsSame(kou1));
            Assert.IsFalse(kou1.IsSame(shun1));

            // Jantou
            var jantou1 = Create("11p");
            Assert.IsTrue(jantou1.IsSame(jantou1));
            Assert.IsFalse(jantou1.IsSame(kou1));
            Assert.IsFalse(kou1.IsSame(jantou1));
            Assert.IsFalse(jantou1.IsSame(shun1));
            Assert.IsFalse(shun1.IsSame(jantou1));
            Assert.IsFalse(jantou1.IsSame(kan1));
            Assert.IsFalse(kan1.IsSame(jantou1));

            // Musou
            var musou1 = Create("1p");
            Assert.IsTrue(musou1.IsSame(musou1));
            Assert.IsFalse(musou1.IsSame(kou1));
            Assert.IsFalse(kou1.IsSame(musou1));
            Assert.IsFalse(musou1.IsSame(shun1));
            Assert.IsFalse(shun1.IsSame(musou1));
            Assert.IsFalse(musou1.IsSame(kan1));
            Assert.IsFalse(kan1.IsSame(musou1));
            Assert.IsFalse(musou1.IsSame(jantou1));
            Assert.IsFalse(jantou1.IsSame(musou1));
        }
    }
}