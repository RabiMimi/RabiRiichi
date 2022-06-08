using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;

namespace RabiRiichi.Tests.Core {
    [TestClass]
    public class ExtensionTests {
        [TestMethod]
        public void TestTileToString() {
            Assert.AreEqual("🀜", new Tile("4p").ToUnicode());
        }

        [TestMethod]
        public void TestTilesToString() {
            Assert.AreEqual("🀈🀒🀜🀆", new Tiles("2m3s4p5z").ToUnicode());
        }
    }
}
