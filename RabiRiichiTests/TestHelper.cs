﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabiRiichi.Riichi;
using System.Linq;

namespace RabiRiichiTests {
    static class TestHelper {
        public static Hand CreateHand(string str, params string[] groups) {
            return new Hand {
                freeTiles = new GameTiles(new Tiles(str)),
                fuuro = groups.Select(gr => MenLike.From(new Tiles(gr))).ToList(),
            };
        }

        public static void AssertEq(this Tiles tiles, string str) {
            var newTiles = new Tiles(str);
            tiles.Sort();
            newTiles.Sort();
            Assert.AreEqual(newTiles.ToString(), tiles.ToString());
        }
    }
}
