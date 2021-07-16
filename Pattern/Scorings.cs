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

        public bool IsValid => han > 0 || yakuman > 0;

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
            score = (score + 99) / 100 * 100;
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
                        fu += score.Val;
                        break;
                    case ScoringType.Han:
                        han += score.Val;
                        break;
                    case ScoringType.Yakuman:
                        yakuman += score.Val;
                        break;
                    case ScoringType.Ryuukyoku:
                        HUtil.Warn($"Ryuukyoku should not be handled in pattern recognition");
                        break;
                    default:
                        HUtil.Warn($"Unknown scoring: {score.Type}");
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
    }
}
