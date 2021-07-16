using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base13_1Test {
        private readonly Base13_1 V = new Base13_1();

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
            Assert.IsFalse(Run("1357s1357p1357m1z", "7z", out _));
            Assert.IsFalse(Run("19s19m19p1234567z", "2s", out _));
            Assert.IsFalse(Run("19s19m19p1234567z", null, out _));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Run("19s19m19p1234567z", "7z", out _));
            Assert.IsTrue(Run("19s19m19p1234577z", "6z", out _));
            Assert.IsTrue(Run("9s19m19p234567z", "1z", out _, "11s"));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }
    }
}
