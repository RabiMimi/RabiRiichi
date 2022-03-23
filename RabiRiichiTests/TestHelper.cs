using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Linq;

namespace RabiRiichiTests {
    static class TestHelper {
        public static Hand CreateHand(string str, params string[] groups) {
            return new Hand {
                freeTiles = new GameTiles(new Tiles(str)),
                called = groups.Select(gr => MenLike.From(new Tiles(gr))).ToList(),
            };
        }

        private static Game cachedGame;

        public static Game GetGame() {
            if (cachedGame == null) {
                cachedGame = new Game(new GameConfig {
                    actionCenter = new JsonStringActionCenter(null)
                });
            }
            return cachedGame;
        }

        public static void AssertEq(this Tiles tiles, string str) {
            var newTiles = new Tiles(str);
            tiles.Sort();
            newTiles.Sort();
            Assert.AreEqual(newTiles.ToString(), tiles.ToString());
        }
    }
}
