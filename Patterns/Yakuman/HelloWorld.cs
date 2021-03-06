using RabiRiichi.Core;
using RabiRiichi.Generated.Patterns;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
    public class HelloWorld : StdPattern {

        // 22266667778999
        private const string HELLO_WORLD = "//HELLO WORLD.";
        private static readonly byte[] MATCH_ARR = HELLO_WORLD.Select(c => (byte)(c % 10)).OrderBy(i => i).ToArray();

        public HelloWorld(Base33332 base33332, Chinitsu chinitsu) {
            BaseOn(base33332);
            DependOn(chinitsu);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.menzen && groups.SelectMany(gr => gr).Select(t => t.tile.Num).SequenceEqualAfterSort(MATCH_ARR)) {
                scores.Remove(afterPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 3, this));
                return true;
            }
            return false;
        }
    }
}
