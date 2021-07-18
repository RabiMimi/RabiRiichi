﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base72Test : BaseTest {
        protected override BasePattern V { get; set; } = new Base72();

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Resolve("1112233557799s", "1s"));
            Assert.IsFalse(Resolve("12233557799s", "1s", "11s"));
            Assert.IsFalse(Resolve("11335577s1133557p", "7p"));
            Assert.IsFalse(Resolve("1s", "1s"));
            Assert.IsFalse(Resolve("1133445p", "5p", "567s", "567s"));
            Assert.IsFalse(Resolve("113344p1s2233z", "1s", "5555s"));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Resolve("1122334455667p", "7p"));
            Assert.IsTrue(Resolve("11335r577p11335r5z", null));
            Assert.IsTrue(Resolve("119p1199m1199s22z", "9p"));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsTrue(Resolve("1123344556677z", "2z"));
        }
    }
}
