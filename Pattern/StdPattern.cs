using RabiRiichi.Communication;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Pattern {
    public enum ScoringType {
        /// <summary> 番 </summary>
        Han,
        /// <summary> 符 </summary>
        Fu,
        /// <summary> 役满 </summary>
        Yakuman,
    }

    public class Scoring : IRabiMessage {
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] public ScoringType Type;
        [RabiBroadcast] public int Val;
        public StdPattern Source;
        [RabiBroadcast] public string Src => Source.name;

        public Scoring(ScoringType type, int val, StdPattern source) {
            Type = type;
            Val = val;
            Source = source;
        }
    }

    /// <summary> 标准役种 </summary>
    public abstract class StdPattern {
        // TODO: (Frenqy) 役种名 罗马音 自己查wiki 最好把class名也改了
        public abstract string name { get; }

        /// <summary> 可以触发该役种的底和 </summary>
        public BasePattern[] basePatterns { get; private set; } = Array.Empty<BasePattern>();

        /// <summary> 满足这些pattern后，才会计算该pattern </summary>
        public StdPattern[] dependOnPatterns { get; private set; } = Array.Empty<StdPattern>();

        /// <summary> 计算这些pattern后，才会计算该pattern。不保证这些pattern一定被满足 </summary>
        public StdPattern[] afterPatterns { get; private set; } = Array.Empty<StdPattern>();

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

        public abstract bool Resolve(List<MenLike> groups, Hand hand, GameTile incoming, ScoreStorage scores);
    }
}
