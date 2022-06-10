using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class Suuankou : StdPattern {
        public Suuankou(Base33332 base33332, Sanankou sanankou, Toitoi toitoi) {
            BaseOn(base33332);
            DependOn(sanankou, toitoi);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.Where(gr => (gr is Kou or Kan) && gr.IsClose).Count() == 4) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 1, this));
                return true;
            }
            return false;
        }
    }
}
