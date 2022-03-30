using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Core;
using RabiRiichiTests.Helper;
using System;
using System.Linq;

namespace RabiRiichiTests.Core {
    [TestClass]
    public class TileTest {
        [TestMethod]
        public void TestTile() {
            var tile = new Tile("3s");
            Assert.AreEqual(3, tile.Num);
            Assert.AreEqual(TileSuit.S, tile.Suit);
            Assert.AreEqual(false, tile.Akadora);
            Assert.AreEqual(new Tile("3s"), tile);
            Assert.AreEqual(new Tile("r5s"), new Tile("0s"));
            Assert.AreNotEqual(new Tile("r3s"), tile);
        }

        [TestMethod]
        public void TestInvalidTile() {
            Assert.ThrowsException<ArgumentException>(() => new Tile("3"));
            Assert.ThrowsException<ArgumentException>(() => new Tile("zz"));
            Assert.ThrowsException<ArgumentException>(() => new Tile("r18"));
            Assert.ThrowsException<ArgumentException>(() => new Tile("8z"));
        }

        [TestMethod]
        public void TestEquality() {
            Assert.AreNotEqual(new Tile("3s"), "3s");
            Assert.IsTrue(new Tile("3s") != new Tile("r3s"));
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

            new Tiles("0r5s").AssertEquals("00s");
        }

        [TestMethod]
        public void TestInvalidTiles() {
            Assert.ThrowsException<ArgumentException>(() => new Tiles("rr5s"));
            Assert.ThrowsException<ArgumentException>(() => new Tiles("r18"));
            Assert.ThrowsException<ArgumentException>(() => new Tiles("678z"));
        }

        [TestMethod]
        public void TestRemove() {
            var tiles = new Tiles("1112233r5s123z");
            tiles.Remove(new Tiles("123s"));
            tiles.AssertEquals("1123r5s123z");
        }

        [TestMethod]
        public void TestTileIsShun() {
            Assert.IsTrue(new Tiles("34r5s").IsShun);
            Assert.IsTrue(new Tiles("345s").IsShun);
            Assert.IsFalse(new Tiles("345z").IsShun);
            Assert.IsFalse(new Tiles("3456s").IsShun);
            Assert.IsFalse(new Tiles("129s").IsShun);
        }

        [TestMethod]
        public void TestTileIsKou() {
            Assert.IsTrue(new Tiles("55r5s").IsKou);
            Assert.IsTrue(new Tiles("555s").IsKou);
            Assert.IsFalse(new Tiles("3s3m3p").IsKou);
            Assert.IsFalse(new Tiles("223z").IsKou);
            Assert.IsFalse(new Tiles("2222z").IsKou);
        }

        [TestMethod]
        public void TestTileIsKan() {
            Assert.IsTrue(new Tiles("55r55s").IsKan);
            Assert.IsTrue(new Tiles("4444s").IsKan);
            Assert.IsFalse(new Tiles("3s33m3p").IsKan);
            Assert.IsFalse(new Tiles("2223z").IsKan);
            Assert.IsFalse(new Tiles("222z").IsKan);
        }

        [TestMethod]
        public void TestTileIsJan() {
            Assert.IsTrue(new Tiles("r55s").IsJan);
            Assert.IsTrue(new Tiles("44s").IsJan);
            Assert.IsFalse(new Tiles("3s3m").IsJan);
            Assert.IsFalse(new Tiles("222z").IsJan);
            Assert.IsFalse(new Tiles("23s").IsJan);
        }
    }
}
