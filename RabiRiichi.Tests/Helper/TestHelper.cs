using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Actions;
using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Tests.Helper {
    static class TestHelperExtensions {
        public static IEnumerable<GameTile> ToGameTiles(this IEnumerable<Tile> tiles) {
            return tiles.Select(t => new GameTile(t, -1));
        }

        public static List<GameTile> ToGameTileList(this IEnumerable<Tile> tiles) {
            return tiles.ToGameTiles().ToList();
        }

        public static MenLike ToMenLike(this IEnumerable<Tile> tiles) {
            return MenLike.From(tiles.ToGameTiles());
        }
    }

    static class TestHelper {

        public static Hand CreateHand(string str, params string[] groups) {
            return new Hand {
                freeTiles = new Tiles(str).ToGameTileList(),
                called = groups.Select(gr => new Tiles(gr).ToMenLike()).ToList(),
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

        public static void AssertEquals(this GameTile tile, string str)
            => tile.tile.AssertEquals(str);

        public static void AssertEquals(this IEnumerable<Tile> tiles, string str) {
            new Tiles(tiles).AssertEquals(str);
        }

        public static void AssertEquals(this IEnumerable<GameTile> tiles, string str)
            => tiles.Select(t => t.tile).AssertEquals(str);

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
