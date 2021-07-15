using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class HaiTest {
        [TestMethod]
        public void TestHai() {
            var hai = new Tile("3s");
            Assert.AreEqual(3, hai.Num);
            Assert.AreEqual(Group.S, hai.Gr);
            Assert.AreEqual(false, hai.Akadora);
            Assert.AreEqual(new Tile("3s"), hai);
            Assert.AreNotEqual(new Tile("r3s"), hai);
        }
    }

    [TestClass]
    public class HaisTest {
        [TestMethod]
        public void TestHais() {
            var hais = new Tiles("345s678p333m1122z");
            Assert.AreEqual(13, hais.Count);
            Assert.AreEqual("345s678p333m1122z", hais.ToString());
            Assert.AreEqual(new Tile("6p"), hais[3]);
        }
    }
}
