using System;
using System.Collections.Generic;
using HUtil = HoshinoSharp.Runtime.Util;

namespace RabiRiichi.Pattern {
    public class Scorings: List<Scoring>, IComparable<Scorings> {
        public const int KAZOE_YAKUMAN = 13;
        public int han;
        public int fu;
        public int yakuman;
        public int point;

        public int baseScore;

        public bool IsValid(int minHan) => han % KAZOE_YAKUMAN + yakuman * KAZOE_YAKUMAN >= minHan;

        private int GetBaseScore() {
            if (yakuman > 0) {
                return yakuman * 8000;
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
        public Scorings(IEnumerable<Scoring> scores): base(scores) { }
        public void Calc() {
            han = 0;
            fu = 0;
            yakuman = 0;
            point = 0;
            baseScore = 0;
            foreach (var score in this) {
                switch (score.Type) {
                    case ScoringType.Point:
                        point += score.Val;
                        break;
                    case ScoringType.Fu:
                        if (fu != 0) {
                            HUtil.Warn("检测到了多个符数计算结果，可能是一个bug");
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
                        HUtil.Warn($"和牌结果中发现了不合法的流局计算结果");
                        break;
                    default:
                        HUtil.Warn($"未知的计分类型: {score.Type}");
                        break;
                }
            }
            if (han >= KAZOE_YAKUMAN) {
                yakuman += han / KAZOE_YAKUMAN;
            }
            baseScore = GetBaseScore() + point;
        }

        public int CompareTo(Scorings other) {
            return baseScore.CompareTo(other.baseScore);
        }

        public static bool operator < (Scorings lhs, Scorings rhs) {
            return lhs.CompareTo(rhs) < 0;
        }

        public static bool operator > (Scorings lhs, Scorings rhs) {
            return lhs.CompareTo(rhs) > 0;
        }
    }
}
