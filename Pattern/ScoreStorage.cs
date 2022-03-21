using RabiRiichi.Util;
using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class ScoreStorage : IComparable<ScoreStorage> {
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

        public class ScoreCalcResult : IComparable<ScoreCalcResult> {
            /// <summary> 基本点 </summary>
            public int BaseScore {
                get {
                    if (IsYakuman) {
                        return yakuman * 8000;
                    }
                    if (IsKazoeYakuman) {
                        return 8000;
                    }
                    if (han >= 11) {
                        return 6000;
                    }
                    if (han >= 8) {
                        return 4000;
                    }
                    if (han >= 6) {
                        return 3000;
                    }
                    if (han >= 5) {
                        return 2000;
                    }
                    int score = fu * (1 << (han + 2));
                    return Math.Min(2000, score);
                }
            }
            /// <summary> 番 </summary>
            public int han;
            /// <summary> 符 </summary>
            public int fu;
            /// <summary> 役满数 </summary>
            public int yakuman;

            public bool IsKazoeYakuman => han >= KAZOE_YAKUMAN;
            public bool IsYakuman => yakuman > 0 || IsKazoeYakuman;
            public bool IsValid(int minHan) => han + yakuman * KAZOE_YAKUMAN >= minHan;

            public int CompareTo(ScoreCalcResult other) {
                if (BaseScore != other.BaseScore) {
                    return BaseScore.CompareTo(other.BaseScore);
                }
                if (han != other.han) {
                    return han.CompareTo(other.han);
                }
                return fu.CompareTo(other.fu);
            }
            public static bool operator <(ScoreCalcResult lhs, ScoreCalcResult rhs) {
                return lhs.CompareTo(rhs) < 0;
            }

            public static bool operator >(ScoreCalcResult lhs, ScoreCalcResult rhs) {
                return lhs.CompareTo(rhs) > 0;
            }
        }

        private readonly List<Scoring> items = new();

        /// <summary> 累计役满需要番数 </summary>
        public const int KAZOE_YAKUMAN = 13;
        /// <summary> 是否已冻结。已冻结的Scoring将无视所有修改操作 </summary>
        public bool isFrozen { get; private set; }

        /// <summary> Scoring数量 </summary>
        public int Count => items.Count;

        public ScoreCalcResult cachedResult = null;

        public ScoreStorage() { }
        public ScoreStorage(IEnumerable<Scoring> scores) {
            items.AddRange(scores);
        }
        public ScoreCalcResult Calc() {
            cachedResult = new ScoreCalcResult();
            foreach (var score in items) {
                switch (score.Type) {
                    case ScoringType.Fu:
                        if (cachedResult.fu != 0) {
                            Logger.Warn("检测到了多个符数计算结果，可能是一个bug");
                        }
                        cachedResult.fu += score.Val;
                        break;
                    case ScoringType.Han:
                        cachedResult.han += score.Val;
                        break;
                    case ScoringType.Yakuman:
                        cachedResult.yakuman += score.Val;
                        break;
                    default:
                        Logger.Warn($"未知的计分类型: {score.Type}");
                        break;
                }
            }
            return cachedResult;
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
            return cachedResult.CompareTo(other.cachedResult);
        }

        public static bool operator <(ScoreStorage lhs, ScoreStorage rhs) {
            return lhs.CompareTo(rhs) < 0;
        }

        public static bool operator >(ScoreStorage lhs, ScoreStorage rhs) {
            return lhs.CompareTo(rhs) > 0;
        }
    }
}
