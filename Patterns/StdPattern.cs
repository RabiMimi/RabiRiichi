using RabiRiichi.Communication;
using RabiRiichi.Core;
using RabiRiichi.Events.InGame;
using RabiRiichi.Util;
using System;
using System.Collections.Generic;
using System.Linq;


namespace RabiRiichi.Patterns {
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
        public static bool ApplyPao(Player toPlayer, int paoPlayer, int paoPoints, ScoreTransferList scoreTransfers) {
            bool ret = false;
            // Find all transactions related to toPlayer
            var transaction = scoreTransfers.Find(
                st => st.to == toPlayer.id && st.reason == ScoreTransferReason.Ron);
            if (transaction != null) {
                if (transaction.from != paoPlayer) {
                    int halfPaoPoints = (paoPoints >> 1).CeilTo100();
                    transaction.points -= halfPaoPoints;
                    scoreTransfers.Add(new ScoreTransfer(paoPlayer, toPlayer.id, halfPaoPoints, ScoreTransferReason.Pao));
                    ret = true;
                }
            } else {
                int paidPoints = 0;
                foreach (var t in scoreTransfers
                    .Where(st => st.to == toPlayer.id
                        && st.from != paoPlayer
                        && st.reason == ScoreTransferReason.Tsumo)) {
                    int delta = toPlayer.game.Dealer.id == t.from ? paoPoints / 2 : paoPoints / 4;
                    t.points -= delta;
                    paidPoints += delta;
                }
                scoreTransfers.Add(new ScoreTransfer(paoPlayer, toPlayer.id, paidPoints, ScoreTransferReason.Pao));
            }
            // Pao player should pay honba points
            foreach (var transfer in scoreTransfers
                .Where(st => st.to == toPlayer.id
                    && st.from != paoPlayer
                    && st.reason == ScoreTransferReason.Honba)) {
                transfer.from = paoPlayer;
                transfer.reason = ScoreTransferReason.Pao;
                ret = true;
            }
            return ret;
        }
    }

    public enum ScoringType {
        /// <summary> 番 </summary>
        Han,
        /// <summary> 奖励番 </summary>
        BonusHan,
        /// <summary> 符 </summary>
        Fu,
        /// <summary> 役满 </summary>
        Yakuman,
    }

    [Flags]
    public enum PatternMask {
        None = 0,
        /// <summary> 一般役种 </summary>
        Regular = 1 << 0,
        /// <summary> 奖励役种 </summary>
        Bonus = 1 << 1,
        /// <summary> 运气役种 </summary>
        Luck = 1 << 2,
        /// <summary> 全部役种 </summary>
        All = Regular | Bonus | Luck,
    }

    [RabiMessage]
    public class Scoring {
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
        public virtual PatternMask type => PatternMask.Regular;

        /// <summary> 可以触发该役种的底和 </summary>
        public BasePattern[] basePatterns { get; private set; } = Array.Empty<BasePattern>();

        /// <summary> 检查该pattern的依赖 </summary>
        public Predicate<ICollection<StdPattern>> predicate { get; private set; } = _ => true;

        /// <summary> 计算这些pattern后，才会计算该pattern。不保证这些pattern一定被满足 </summary>
        public readonly List<StdPattern> afterPatterns = new();

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

        /// <summary>
        /// 设置依赖条件。
        /// 注意：使用本方法时，必须保证所有依赖的pattern都已经被添加到afterPatterns中。
        /// </summary>
        protected StdPattern DependOn(Predicate<ICollection<StdPattern>> predicate) {
            this.predicate = predicate;
            return this;
        }

        protected StdPattern DependOn(params StdPattern[] dependOnPatterns) {
            After(dependOnPatterns);
            DependOn(dependOnPatterns.Aggregate(
                predicate,
                (p, pattern) => p.And(pattern)));
            return this;
        }

        protected StdPattern After(params StdPattern[] afterPatterns) {
            this.afterPatterns.AddRange(afterPatterns);
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
            => PaoUtil.ApplyPao(toPlayer, paoPlayer,
                toPlayer.IsDealer ? (ronPaoPoints * 3 / 2).CeilTo100() : ronPaoPoints,
                scoreTransfers);
    }
}
