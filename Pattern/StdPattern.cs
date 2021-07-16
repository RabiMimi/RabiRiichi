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
        protected static Type[] NoDependency = new Type[0];
        protected static Type[] Only33332 = new Type[] { typeof(Base33332) };
        protected static Type[] AllBasePatterns = new Type[] {
            typeof(Base33332),
            typeof(Base13_1),
            typeof(Base72)
        };

        public abstract Type[] basePatterns { get; }
        public virtual Type[] dependOnPatterns => NoDependency;
        public abstract bool Resolve(List<GameTiles> groups, Hand hand, GameTile incoming, Scorings scorings);
    }
}
