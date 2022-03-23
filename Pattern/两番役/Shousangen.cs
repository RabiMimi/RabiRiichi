using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class Shousangen : StdPattern {
        public Shousangen(Base33332 base33332) {
            BaseOn(base33332);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            bool jantouFlag = groups.Any(gr => gr is Jantou && gr.First.tile.IsSangen);
            bool kouFlag = groups
                .Where(gr => gr is not Jantou)
                .Subset(2)
                .Any(grs => grs.All(gr => gr.First.tile.IsSangen));
            if (jantouFlag && kouFlag) {
                scores.Add(new Scoring(ScoringType.Han, 2, this));
                return true;
            }
            return false;
        }
    }
}