using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System.Collections.Generic;
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

        public static void AssertEquals(this Tile tile, string str) {
            Assert.AreEqual(tile.ToString(), str);
        }

        public static void AssertEquals(this Tiles tiles, string str) {
            var newTiles = new Tiles(str);
            tiles.Sort();
            newTiles.Sort();
            Assert.AreEqual(newTiles.ToString(), tiles.ToString());
        }

        public static void AssertContains(this IEnumerable<Tiles> tiles, string str) {
            var newTiles = new Tiles(str);
            Assert.IsTrue(tiles.Any(t => t.SequenceEqualAfterSort(newTiles)));
        }

        public static void AssertContains(this IEnumerable<IEnumerable<GameTile>> tiles, string str)
            => tiles.Select(t => t.ToTiles()).AssertContains(str);
    }
}
