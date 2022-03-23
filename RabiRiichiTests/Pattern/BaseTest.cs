using RabiRiichi.Core;
using RabiRiichi.Pattern;
using System.Collections.Generic;

namespace RabiRiichiTests.Pattern {
    public class BaseTest {
        protected virtual BasePattern V { get; set; }
        protected Tiles tiles;
        protected List<List<MenLike>> groupList;

        protected bool Resolve(string hand, string incoming, params string[] groups) {
            var handV = TestHelper.CreateHand(hand, groups);
            return V.Resolve(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile(new Tile(incoming)), out groupList);
        }

        protected int Shanten(string hand, string incoming, int maxShanten = int.MaxValue, params string[] groups) {
            var handV = TestHelper.CreateHand(hand, groups);
            return V.Shanten(handV, string.IsNullOrEmpty(incoming)
                ? null : new GameTile(new Tile(incoming)), out tiles, maxShanten);
        }
    }
}
