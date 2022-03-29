using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using System.Linq;

namespace RabiRiichiTests.Helper {
    static class TestHelper {
        public static Hand CreateHand(string str, params string[] groups) {
            return new Hand {
                freeTiles = new Tiles(str).ToGameTileList(),
                called = groups.Select(gr => MenLike.From(new Tiles(gr))).ToList(),
            };
        }

        public static Game CreateGame() => new(new GameConfig {
            actionCenter = new JsonStringActionCenter(null)
        });

        public static void AssertEq(this Tiles tiles, string str) {
            var newTiles = new Tiles(str);
            tiles.Sort();
            newTiles.Sort();
            Assert.AreEqual(newTiles.ToString(), tiles.ToString());
        }
    }
}
