using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public enum ScoringType {
        Point, Han, Fu, Yakuman, Ryuukyoku
    }
    public class Scoring {
        public ScoringType Type;
        public int Val;
        public StdPattern Source;
    }

    public abstract class StdPattern {
        public abstract Type[] basePatterns { get; }
        public abstract Type[] dependOnPatterns { get; }
        public abstract bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings);
    }
}
