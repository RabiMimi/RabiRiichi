using RabiRiichi.Communication;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System;
using System.Collections;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class ScoreStorage : IComparable<ScoreStorage>, IEnumerable<Scoring>, IRabiMessage {
        internal class Refrigerator : IDisposable {
            private readonly ScoreStorage scores;
            private readonly bool oldValue;
            public Refrigerator(ScoreStorage scores, bool newValue) {
                this.scores = scores;
                oldValue = scores.isFrozen;
                scores.isFrozen = newValue;
            }

            public void Dispose() {
                scores.isFrozen = oldValue;
            }
        }

        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        [RabiBroadcast] private readonly List<Scoring> items = new();

        /// <summary> 累计役满需要番数 </summary>
        public const int KAZOE_YAKUMAN = 13;

        /// <summary> 是否已冻结。已冻结的Scoring将无视所有修改操作 </summary>
        public bool isFrozen { get; private set; }

        /// <summary> Scoring数量 </summary>
        public int Count => items.Count;

        [RabiBroadcast] public ScoreCalcResult result = null;

        public ScoreStorage() { }
        public ScoreStorage(IEnumerable<Scoring> scores) {
            items.AddRange(scores);
        }
        public ScoreCalcResult Calc(ScoringOption option) {
            result = new ScoreCalcResult(option);
            foreach (var score in items) {
                switch (score.Type) {
                    case ScoringType.Fu:
                        if (result.fu != 0) {
                            Logger.Warn("检测到了多个符数计算结果，可能是一个bug");
                        }
                        result.fu += score.Val;
                        break;
                    case ScoringType.Han:
                        result.yaku += score.Val;
                        result.han += score.Val;
                        break;
                    case ScoringType.BonusHan:
                        result.han += score.Val;
                        break;
                    case ScoringType.Yakuman:
                        result.yakuman += score.Val;
                        break;
                    default:
                        Logger.Warn($"未知的计分类型: {score.Type}");
                        break;
                }
            }
            return result;
        }

        /// <summary> 冻结当前的Scoring（临时变为只读） </summary>
        internal Refrigerator Freeze(bool shouldFreeze = true) {
            if (isFrozen == shouldFreeze) {
                return null;
            }
            return new Refrigerator(this, shouldFreeze);
        }

        public void Add(Scoring scoring) {
            if (isFrozen)
                return;
            items.Add(scoring);
        }


        public Scoring Find(Predicate<Scoring> match) {
            return items.Find(match);
        }

        public void Remove(StdPattern pattern) {
            if (isFrozen)
                return;
            items.RemoveAll(s => s.Source == pattern);
        }

        public void Remove(Scoring s) {
            if (isFrozen)
                return;
            items.Remove(s);
        }

        public void Remove(IEnumerable<StdPattern> patterns) {
            if (isFrozen)
                return;
            foreach (var pattern in patterns) {
                Remove(pattern);
            }
        }

        public int CompareTo(ScoreStorage other) {
            return result.CompareTo(other.result);
        }

        public IEnumerator<Scoring> GetEnumerator() {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return items.GetEnumerator();
        }

        public static bool operator <(ScoreStorage lhs, ScoreStorage rhs) {
            return lhs.CompareTo(rhs) < 0;
        }

        public static bool operator >(ScoreStorage lhs, ScoreStorage rhs) {
            return lhs.CompareTo(rhs) > 0;
        }
    }
}
