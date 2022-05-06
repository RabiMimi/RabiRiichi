using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Event.InGame;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Pattern {
    /// <summary> 用于包牌计算 </summary>
    public static class PaoUtil {
        public static bool TryGetPaoPlayer(MenLike group, out int paoPlayer) {
            paoPlayer = -1;
            if (group.IsClose) {
                return false;
            }
            var fuuro = group.Find(tile => !tile.IsTsumo);
            if (fuuro == null) {
                return false;
            }
            paoPlayer = fuuro.discardInfo.from;
            return true;
        }
        public static bool ApplyPao(int toPlayer, int paoPlayer, int paoPoints, ScoreTransferList scoreTransfers) {
            var transaction = scoreTransfers.Find(st => st.to == toPlayer &&
                (st.reason == ScoreTransferReason.Ron || st.reason == ScoreTransferReason.Tsumo));
            if (transaction == null || paoPoints <= 0) {
                return false;
            }
            if (transaction.reason == ScoreTransferReason.Tsumo) {
                transaction.from = paoPlayer;
                transaction.reason = ScoreTransferReason.Pao;
            } else {
                if (transaction.from == paoPlayer) {
                    return false;
                }
                transaction.points -= paoPoints;
                scoreTransfers.Add(new ScoreTransfer(paoPlayer, toPlayer, paoPoints, ScoreTransferReason.Pao));
            }
            return true;
        }
    }

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
        public virtual string name => GetType().Name;

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
        /// <summary>
        /// 满足该役种并计算得分后，最后对分数转移表进行修正
        /// 一般用于包牌计算
        /// </summary>
        /// <returns>是否影响分数计算</returns>
        public virtual bool OnScoreTransfer(Player player, ScoreTransferList scoreTransfers) => false;
        protected virtual bool ApplyPao(Player toPlayer, int paoPlayer, int ronPaoPoints, ScoreTransferList scoreTransfers)
            => PaoUtil.ApplyPao(toPlayer.id, paoPlayer,
                toPlayer.IsDealer ? (ronPaoPoints * 3 / 2).CeilTo100() : ronPaoPoints,
                scoreTransfers);
    }
}
