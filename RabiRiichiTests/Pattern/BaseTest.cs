using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    public class BaseTest {
        protected virtual BasePattern V { get; set; }

        protected bool Run(string hand, string incoming, out List<List<GameTiles>> output, params string[] groups) {
            var handV = TestHelper.CreateHand(hand);
            foreach (var group in groups) {
                handV.AddGroup(new GameTiles(new Tiles(group)));
            }
            return V.Resolve(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile {
                    tile = new Tile(incoming)
                }, out output);
        }
    }
}
