using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class HaiTest {
        [TestMethod]
        public void TestHai() {
            var hai = new Hai("3s");
            Assert.AreEqual(3, hai.Num);
            Assert.AreEqual(Group.S, hai.Gr);
            Assert.AreEqual(false, hai.Akadora);
            Assert.AreEqual(new Hai("3s"), hai);
            Assert.AreNotEqual(new Hai("r3s"), hai);
        }
    }

    [TestClass]
    public class HaisTest {
        [TestMethod]
        public void TestHais() {
            var hais = new Hais("345s678p333m1122z");
            Assert.AreEqual(13, hais.Count);
            Assert.AreEqual("345s678p333m1122z", hais.ToString());
            Assert.AreEqual(new Hai("6p"), hais[3]);
        }
    }
}
