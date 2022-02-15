using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 平和 : StdPattern {
        public override Type[] dependOnPatterns => Only33332;

        public override bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen || groups.Any(gr => gr.IsKan || gr.IsKou))
                return false;
            var gr = groups.Find(gr => gr.Contains(incoming));
            if (!gr.IsShun)
                return false;
            // 确认两面听
            if (gr.Any(tile => tile.tile.Is19Z && tile != incoming))
                return false;
            gr.Sort();
            if (incoming == gr[1])
                return false;
            // TODO：确认雀头没有+符
            scorings.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
