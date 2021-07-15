using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;
using System;
using System.Linq;
using System.Text;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class ExtensionTests {
        [TestMethod]
        public void TestHaiToString() {
            Assert.AreEqual("🀜", new Tile("4p").ToUnicode());
        }

        [TestMethod]
        public void TestHaisToString() {
            Assert.AreEqual("🀈🀒🀜🀆", new Tiles("2m3s4p5z").ToUnicode());
        }
    }
}
