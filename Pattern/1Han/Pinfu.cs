using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Pinfu : StdPattern {
        public Pinfu(Base33332 base33332, Fu33332 fu33332) {
            BaseOn(base33332);
            After(fu33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (!hand.menzen || groups.Any(gr => gr is not (Jantou or Shun)))
                return false;
            var gr = groups.Find(gr => gr.Contains(incoming));
            if (gr is not Shun)
                return false;
            // 确认两面听
            if (gr.Any(tile => tile.tile.Is19Z && tile != incoming))
                return false;
            if (incoming == gr[1])
                return false;
            var jantou = groups.Find(gr => gr is Jantou);
            if (hand.player.IsYaku(jantou.First.tile))
                return false;

            scores.Add(new Scoring(ScoringType.Han, 1, this));
            scores.Remove(afterPatterns);
            scores.Add(new Scoring(ScoringType.Fu, incoming.IsTsumo ? 20 : 30, this));
            return true;
        }
    }
}
