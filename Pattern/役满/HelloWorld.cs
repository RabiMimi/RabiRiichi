using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Pattern {
    public class HelloWorld : StdPattern {

        private const string HELLO_WORLD = "//HELLO WORLD.";
        private static readonly byte[] MATCH_ARR = HELLO_WORLD.Select(c => (byte)(c % 10)).OrderBy(i => i).ToArray();

        public HelloWorld(Base33332 base33332, 清一色 清一色) {
            BaseOn(base33332);
            DependOn(清一色);
        }

        public override bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores) {
            if (hand.menzen && groups.SelectMany(gr => gr).Select(t => t.tile.Num).OrderBy(t => t).SequenceEqual(MATCH_ARR)) {
                scores.Remove(dependOnPatterns);
                scores.Add(new Scoring(ScoringType.Yakuman, 3, this));
                return true;
            }
            return false;
        }
    }
}
