using System;
using System.Collections.Generic;

namespace RabiRiichi.Pattern {
    public class Scorings : IComparable<Scorings> {
        internal class Refrigerator : IDisposable {
            public readonly Scorings scorings;
            public Refrigerator(Scorings scorings) {
                this.scorings = scorings;
                scorings.isFrozen = true;
            }

            public void Dispose() {
                scorings.isFrozen = false;
            }
        }

        private readonly List<Scoring> items = new();

        /// <summary> 累计役满需要番数 </summary>
        public const int KAZOE_YAKUMAN = 13;

        /// <summary> 番 </summary>
        public int han;

        /// <summary> 符 </summary>
        public int fu;

        /// <summary> 役满数 </summary>
        public int yakuman;

        /// <summary> 是否已冻结。已冻结的Scoring将无视所有修改操作 </summary>
        public bool isFrozen { get; private set; }
        public bool IsKazoeYakuman => han >= KAZOE_YAKUMAN;
        public bool IsYakuman => yakuman > 0 || IsKazoeYakuman;

        /// <summary> Scoring数量 </summary>
        public int Count => items.Count;

        /// <summary> 基本点变动 </summary>
        /// TODO: 研究一下这是个啥玩意儿
        public int point;

        /// <summary> 基本点 </summary>
        public int baseScore;

        public bool IsValid(int minHan) => han + yakuman * KAZOE_YAKUMAN >= minHan;

        private int GetBaseScore() {
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

        public Scorings() { }
        public Scorings(IEnumerable<Scoring> scores) {
            items.AddRange(scores);
        }
        public void Calc() {
            han = 0;
            fu = 0;
            yakuman = 0;
            point = 0;
            baseScore = 0;
            foreach (var score in items) {
                switch (score.Type) {
                    case ScoringType.Point:
                        point += score.Val;
                        break;
                    case ScoringType.Fu:
                        if (fu != 0) {
                            // TODO: Log
                            // HUtil.Warn("检测到了多个符数计算结果，可能是一个bug");
                        }
                        fu += score.Val;
                        break;
                    case ScoringType.Han:
                        han += score.Val;
                        break;
                    case ScoringType.Yakuman:
                        yakuman += score.Val;
                        break;
                    case ScoringType.Ryuukyoku:
                        // TODO: Log
                        // HUtil.Warn($"和牌结果中发现了不合法的流局计算结果");
                        break;
                    default:
                        // TODO: Log
                        // HUtil.Warn($"未知的计分类型: {score.Type}");
                        break;
                }
            }
            baseScore = GetBaseScore() + point;
        }

        /// <summary> 冻结当前的Scoring（临时变为只读） </summary>
        internal Refrigerator Freeze() {
            return new Refrigerator(this);
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

        public int CompareTo(Scorings other) {
            if (baseScore != other.baseScore) {
                return baseScore.CompareTo(other.baseScore);
            }
            if (han != other.han) {
                return han.CompareTo(other.han);
            }
            return fu.CompareTo(other.fu);
        }

        public static bool operator <(Scorings lhs, Scorings rhs) {
            return lhs.CompareTo(rhs) < 0;
        }

        public static bool operator >(Scorings lhs, Scorings rhs) {
            return lhs.CompareTo(rhs) > 0;
        }
    }
}
