using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {

    public class Base13_1 : BasePattern {
        public override bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check hand & groups valid
            var tileGroups = GetTileGroups(hand, incoming, true);
            List<GameTiles> ret = new List<GameTiles>();
            bool has2 = false;
            foreach (var gr in tileGroups) {
                if (gr.Count == 0) {
                    continue;
                }
                if (gr.Count > 2 || !gr[0].tile.Is19Z) {
                    return false;
                }
                if (gr.Count == 2) {
                    if (has2) {
                        return false;
                    }
                    has2 = true;
                }
                ret.Add(gr);
            }
            output = new List<List<GameTiles>> { ret };
            return true;
        }
    }
}
