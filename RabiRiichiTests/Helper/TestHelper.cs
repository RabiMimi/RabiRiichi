using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Action;
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

        public static IEnumerable<Tile> ToTiles(this IEnumerable<ActionOption> options) {
            return options.Select(option => ((ChooseTileActionOption)option).tile.tile);
        }

        public static IEnumerable<IEnumerable<Tile>> ToTileLists(this IEnumerable<ActionOption> options) {
            return options.Select(
                option => ((ChooseTilesActionOption)option).tiles
                    .Select(tile => tile.tile));
        }

        public static IEnumerable<string> ToStrings(this IEnumerable<ActionOption> options) {
            return options.ToTileLists().ToStrings();
        }

        public static IEnumerable<string> ToStrings(this IEnumerable<IEnumerable<Tile>> tileLists) {
            return tileLists.Select(tiles => new Tiles(tiles).ToString());
        }

        public static IEnumerable<string> ToStrings(this IEnumerable<IEnumerable<GameTile>> tileLists) {
            return tileLists.Select(tiles => tiles.Select(tile => tile.tile)).ToStrings();
        }

        public static void AssertEquals(this Tile tile, string str) {
            Assert.AreEqual(tile.ToString(), str);
        }

        public static void AssertEquals(this IEnumerable<Tile> tiles, string str) {
            new Tiles(tiles).AssertEquals(str);
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
