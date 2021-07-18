using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    public class BaseTest {
        protected virtual BasePattern V { get; set; }

        protected bool Resolve(string hand, string incoming, out List<List<GameTiles>> output, params string[] groups) {
            var handV = TestHelper.CreateHand(hand, groups);
            return V.Resolve(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile {
                    tile = new Tile(incoming)
                }, out output);
        }

        protected int Shanten(string hand, string incoming, out Tiles output, int maxShanten = 8, params string[] groups) {
            var handV = TestHelper.CreateHand(hand, groups);
            return V.Shanten(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile {
                    tile = new Tile(incoming)
                }, out output, maxShanten);
        }
    }
}
