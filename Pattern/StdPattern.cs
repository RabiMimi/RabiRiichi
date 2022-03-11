using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;


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

        /// <summary> 可以触发该役种的底和 </summary>
        public BasePattern[] basePatterns { get; protected set; } = Array.Empty<BasePattern>();

        /// <summary> 满足这些pattern后，才会计算该pattern </summary>
        public StdPattern[] dependOnPatterns { get; protected set; } = Array.Empty<StdPattern>();

        /// <summary> 计算这些pattern后，才会计算该pattern。不保证这些pattern一定被满足 </summary>
        public StdPattern[] afterPatterns { get; protected set; } = Array.Empty<StdPattern>();

        protected StdPattern BaseOn(IEnumerable<BasePattern> basePatterns) {
            if (basePatterns != null) {
                this.basePatterns = basePatterns.ToArray();
            }
            return this;
        }
        protected StdPattern BaseOn(params BasePattern[] basePatterns) {
            this.basePatterns = basePatterns;
            return this;
        }

        protected StdPattern DependOn(params StdPattern[] dependOnPatterns) {
            this.dependOnPatterns = dependOnPatterns;
            return this;
        }

        protected StdPattern After(params StdPattern[] afterPatterns) {
            this.afterPatterns = afterPatterns;
            return this;
        }

        public abstract bool Resolve(List<MenOrJantou> groups, Hand hand, GameTile incoming, Scorings scorings);
    }
}
