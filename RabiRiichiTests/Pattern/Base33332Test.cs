using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base33332Test {
        private readonly Base33332 V = new Base33332();

        private bool Run(string hand, string incoming, out List<List<GameTiles>> output, params string[] groups) {
            var handV = TestHelper.CreateHand(hand);
            foreach (var group in groups) {
                handV.AddGroup(new GameTiles(new Tiles(group)));
            }
            return V.Resolve(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile {
                tile = new Tile(incoming)
            }, out output);
        }

        [TestMethod]
        public void TestInvalid() {
            Assert.IsFalse(Run("122334s5r566777p", null, out _));
            Assert.IsFalse(Run("122334s5r5667777p", "7p", out _));
            Assert.IsFalse(Run("1s", "7p", out _));
        }

        [TestMethod]
        public void TestShun() {
            Assert.IsTrue(Run("122334s5r566777p", "7p", out _));
            Assert.IsTrue(Run("122334s5r5667p", "7p", out _, "77p"));
            Assert.IsFalse(Run("122334s5677p", "7p", out _, "55p"));
            Assert.IsTrue(Run("122334s5r677p", "7p", out _, "567p"));
            Assert.IsFalse(Run("122334s5567p", "7p", out _, "567p"));
        }

        [TestMethod]
        public void Test9Lian() {
            Assert.IsTrue(Run("1112345678999s", "1s", out _));
            Assert.IsTrue(Run("1112345678999s", "2s", out _));
            Assert.IsTrue(Run("1112345678999s", "3s", out _));
            Assert.IsTrue(Run("1112345678999s", "4s", out _));
            Assert.IsTrue(Run("1112345678999s", "5s", out _));
            Assert.IsTrue(Run("1112345678999s", "6s", out _));
            Assert.IsTrue(Run("1112345678999s", "7s", out _));
            Assert.IsTrue(Run("1112345678999s", "8s", out _));
            Assert.IsTrue(Run("1112345678999s", "9s", out _));
        }

        [TestMethod]
        public void TestFrenqy() {
            Assert.IsTrue(Run("2223344556677s", "2s", out _));
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsFalse(Run("2234556677s123z", "2s", out _));
        }
    }
}
