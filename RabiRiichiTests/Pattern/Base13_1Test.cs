using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base13_1Test: BaseTest {
        protected override BasePattern V { get; set; } = new Base13_1();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("1357s1357p1357m1z", "7z", out _));
            Assert.IsFalse(Resolve("19s19m19p1234567z", "2s", out _));
            Assert.IsFalse(Resolve("19s19m19p1234567z", null, out _));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Resolve("19s19m19p1234567z", "7z", out _));
            Assert.IsTrue(Resolve("19s19m19p1234577z", "6z", out _));
            Assert.IsTrue(Resolve("9s19m19p234567z", "1z", out _, "11s"));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }
    }
}
