using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;

namespace RabiRiichi.Patterns {
    public class SuuankouTanki : StdPattern {
        public SuuankouTanki(Base33332 base33332, Suuankou suuankou) {
            BaseOn(base33332);
            DependOn(suuankou);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (groups.Find(gr => gr.Contains(incoming)) is Jantou) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 2, this));
                return true;
            }
            return false;
        }
    }
}
