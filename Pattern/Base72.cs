using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {

    public class Base72 : BasePattern {
        public override bool Resolve(Hand hand, GameTile incoming, out List<List<GameTiles>> output) {
            output = null;
            // Check tile count
            if (hand.Count != (incoming == null ? Game.HandSize + 1 : Game.HandSize)) {
                return false;
            }
            // Check groups
            if (hand.groups.Any(gr => !gr.IsJan)) {
                return false;
            }
            // Check hand & groups valid
            var tileGroups = GetTileGroups(hand, incoming, true);
            var ret = new List<GameTiles>();
            foreach (var gr in tileGroups) {
                if (gr.Count == 0) {
                    continue;
                }
                if (gr.Count != 2) {
                    return false;
                }
                ret.Add(gr);
            }
            output = new List<List<GameTiles>> { ret };
            return true;
        }
    }
}
