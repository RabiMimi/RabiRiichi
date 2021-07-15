using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;

namespace RabiRiichiTests.Riichi {
    [TestClass]
    public class TileTest {
        [TestMethod]
        public void TestTile() {
            var tile = new Tile("3s");
            Assert.AreEqual(3, tile.Num);
            Assert.AreEqual(Group.S, tile.Gr);
            Assert.AreEqual(false, tile.Akadora);
            Assert.AreEqual(new Tile("3s"), tile);
            Assert.AreNotEqual(new Tile("r3s"), tile);
        }
    }

    [TestClass]
    public class TilesTest {
        [TestMethod]
        public void TestTiles() {
            var tiles = new Tiles("345s678p333m1122z");
            Assert.AreEqual(13, tiles.Count);
            Assert.AreEqual("345s678p333m1122z", tiles.ToString());
            Assert.AreEqual(new Tile("6p"), tiles[3]);
        }
    }
}
