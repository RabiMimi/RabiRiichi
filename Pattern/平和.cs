using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 平和 : StdPattern {
        public override Type[] basePatterns => Only33332;

        public override bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen || groups.Any(gr => !(gr is Jantou) && !(gr is Shun)))
                return false;
            var gr = groups.Find(gr => gr.Contains(incoming));
            if (!(gr is Shun))
                return false;
            // 确认两面听
            if (gr.Any(tile => tile.tile.Is19Z && tile != incoming))
                return false;
            gr.Sort();
            if (incoming == gr[1])
                return false;
            var jantou = groups.Find(gr => gr is Jantou);
            if (jantou == null || hand.player.IsYaku(jantou[0].tile))
                return false;
            scorings.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
