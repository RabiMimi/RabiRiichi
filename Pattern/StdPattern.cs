using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public enum ScoringType {
        /// <summary> 额外得分，不含本场棒/立直棒。 </summary>
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

        public Scoring(ScoringType type, int val, StdPattern source) {
            Type = type;
            Val = val;
            Source = source;
        }
    }

    /// <summary> 标准役种 </summary>
    public abstract class StdPattern {
        protected static readonly Type[] NoPattern = Array.Empty<Type>();
        protected static readonly Type[] Only33332 = new Type[] { typeof(Base33332) };
        protected static readonly Type[] Only72 = new Type[] { typeof(Base72) };
        protected static readonly Type[] Only13_1 = new Type[] { typeof(Base13_1) };
        protected static readonly Type[] AllBasePatterns = new Type[] {
            typeof(Base33332),
            typeof(Base13_1),
            typeof(Base72)
        };

        /// <summary> 满足这些pattern后，才会计算该pattern </summary>
        public virtual Type[] dependOnPatterns => NoPattern;

        /// <summary> 可以触发该役种的底和 </summary>
        public abstract Type[] basePatterns { get; }

        /// <summary> 计算这些pattern后，才会计算该pattern。不保证这些pattern一定被满足 </summary>
        public virtual Type[] afterPatterns => NoPattern;
        public abstract bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings);
    }
}
