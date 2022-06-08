using RabiRiichi.Communication;
using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System;

namespace RabiRiichi.Pattern {
    public class ScoreCalcResult : IComparable<ScoreCalcResult>, IRabiMessage {
        public const int KAZOE_YAKUMAN_HAN = 13;
        public RabiMessageType msgType => RabiMessageType.Unnecessary;
        /// <summary> 番 </summary>
        [RabiBroadcast] public int han;
        /// <summary> 记作役的番数 </summary>
        [RabiBroadcast] public int yaku;
        /// <summary> 符 </summary>
        [RabiBroadcast] public int fu;
        /// <summary> 役满数，不包含累计役满 </summary>
        [RabiBroadcast] public int yakuman;
        /// <summary> 计分选项 </summary>
        public ScoringOption scoringOption;

        public ScoreCalcResult(ScoringOption option) {
            scoringOption = option;
        }

        /// <summary> 基本点 </summary>
        public long BaseScore {
            get {
                if (!scoringOption.HasAnyFlag(ScoringOption.Yakuman)) {
                    // 青天井
                    return fu * (1L << (han + yakuman * KAZOE_YAKUMAN_HAN + 2));
                }
                int finalYakuman = FinalYakuman;
                if (finalYakuman > 0) {
                    return finalYakuman * 8000L;
                }
                if (han >= 11) // 三倍满
                    return 6000;
                if (han >= 8) // 倍满
                    return 4000;
                if (han >= 6) // 跳满
                    return 3000;
                if (han >= 5) // 满贯
                    return 2000;
                long score = fu * (1L << (han + 2));
                if (scoringOption.HasAnyFlag(ScoringOption.KiriageMangan)) {
                    if (score > 1900 && score < 2000) {
                        score = 2000;
                    }
                }
                return Math.Min(2000, score);
            }
        }

        [RabiBroadcast]
        public int KazoeYakuman {
            get {
                if (!scoringOption.HasAllFlags(ScoringOption.Yakuman | ScoringOption.KazoeYakuman)) {
                    return 0;
                }
                return han >= KAZOE_YAKUMAN_HAN ? 1 : 0;
            }
        }

        /// <summary>
        /// 役满数，为累计役满和单独役满的较大值
        /// </summary>
        [RabiBroadcast]
        public int FinalYakuman => scoringOption.HasAnyFlag(ScoringOption.Yakuman)
            ? Math.Max(
                scoringOption.HasAnyFlag(ScoringOption.MultipleYakuman) ? yakuman : Math.Min(1, yakuman),
                KazoeYakuman) : 0;

        public bool IsValid(int minHan) => yaku + yakuman * KAZOE_YAKUMAN_HAN >= minHan;

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
}