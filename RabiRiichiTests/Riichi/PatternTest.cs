using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class PatternTest {
        [TestMethod]
        public void TestOr() {
            Kan kan = new(new Tiles("9999p"), true);
            MenOrJantou men = kan as MenOrJantou;
            Assert.IsTrue(MenOrJantou.IsKan(men));
            Assert.IsFalse(men is not Kou && men is not Kan);
            Assert.IsTrue(men is not Kou or Kan);
            Assert.IsFalse(men is not (Kou or Kan));
        }
    }
}