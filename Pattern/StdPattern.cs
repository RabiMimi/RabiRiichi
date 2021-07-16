using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public enum ScoringType {
        /// <summary> 额外得分，例如本场棒。不含立直棒。 </summary>
        Point,
        /// <summary> 番 </summary>
        Han,
        /// <summary> 符 </summary>
        Fu,
        /// <summary> 役满 </summary>
        Yakuman,
        /// <summary> 流局 </summary>
        Ryuukyoku
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
