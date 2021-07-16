using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    [TestClass]
    public class Base72Test {
        private readonly Base72 V = new Base72();

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
            Assert.IsFalse(Run("1112233557799s", "1s", out _));
            Assert.IsFalse(Run("12233557799s", "1s", out _, "11s"));
            Assert.IsFalse(Run("11335577s1133557p", "7p", out _));
            Assert.IsFalse(Run("1s", "1s", out _));
            Assert.IsFalse(Run("1133445p", "5p", out _, "567s", "567s"));
            Assert.IsFalse(Run("113344p1s2233z", "1s", out _, "5555s"));
        }

        [TestMethod]
        public void TestValid() {
            Assert.IsTrue(Run("1122334455667p", "7p", out _));
            Assert.IsTrue(Run("11335r577p11335r5z", null, out _));
            Assert.IsTrue(Run("119p1199m1199s22z", "9p", out _));
        }

        [TestMethod]
        public void TestFrenqy() {
            // TODO(Frenqy)
        }

        [TestMethod]
        public void TestZ() {
            Assert.IsTrue(Run("1123344556677z", "2z", out _));
        }
    }
}
