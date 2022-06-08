using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Util;

namespace RabiRiichi.Tests.Util {
    [TestClass]
    public class AtomicBoolTest {

        [TestMethod]
        public void ConstructAtomicBool() {
            var b = new AtomicBool(true);
            Assert.IsTrue(b);
            b = new AtomicBool(false);
            Assert.IsFalse(b);
            b = true;
            Assert.IsTrue(b);
            b = false;
            Assert.IsFalse(b);
        }

        [TestMethod]
        public void ExchangeAndSet() {
            var b = new AtomicBool(true);
            Assert.IsTrue(b);
            Assert.IsTrue(b.Exchange(false));
            Assert.IsFalse(b);
            b.Exchange(true);
            Assert.IsTrue(b);
            b.Set(false);
            Assert.IsFalse(b);
            b.Set(true);
            Assert.IsTrue(b);
        }
    }
}