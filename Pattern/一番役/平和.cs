using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class 平和 : StdPattern {
        public 平和(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, Scorings scorings) {
            if (!hand.menzen || groups.Any(gr => gr is not (Jantou or Shun)))
                return false;
            var gr = groups.Find(gr => gr.Contains(incoming));
            if (gr is not Shun)
                return false;
            // 确认两面听
            if (gr.Any(tile => tile.tile.Is19Z && tile != incoming))
                return false;
            gr.Sort();
            if (incoming == gr[1])
                return false;
            var jantou = groups.Find(gr => gr is Jantou);
            if (jantou == null || hand.player.IsYaku(jantou.First.tile))
                return false;
            scorings.Add(new Scoring(ScoringType.Han, 1, this));
            return true;
        }
    }
}
