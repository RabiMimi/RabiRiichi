using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Utils;

namespace RabiRiichi.Tests.Utils {
    [TestClass]
    public class AutoIncrementIntTest {

        [TestMethod]
        public void ConstructAutoIncrementInt() {
            var i = new AutoIncrementInt(10);
            int val = i;
            Assert.AreEqual(10, val);
            Assert.AreEqual(10, i.Value);

            i = 13;
            val = i;
            Assert.AreEqual(13, val);
            Assert.AreEqual(13, i.Value);
        }

        [TestMethod]
        public void ModifyAutoIncrementInt() {
            var i = new AutoIncrementInt(10);
            Assert.AreEqual(11, i.Next);
            Assert.AreEqual(12, i.Next);
            i.Reset(4);
            Assert.AreEqual(5, i.Next);
        }
    }
}