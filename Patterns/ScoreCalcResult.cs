using RabiRiichi.Communication;
using RabiRiichi.Core.Config;
using RabiRiichi.Generated.Patterns;
using RabiRiichi.Patterns.Compiled;
using RabiRiichi.Utils;
using System;

namespace RabiRiichi.Patterns {
  [RabiMessage]
  public class ScoreCalcResult(ScoringOption option) : IComparable<ScoreCalcResult> {
    public const int KAZOE_YAKUMAN_HAN = 13;
    /// <summary> 番 </summary>
    [RabiBroadcast] public int han;
    /// <summary> 记作役的番数 </summary>
    [RabiBroadcast] public int yaku;
    /// <summary> 符 </summary>
    [RabiBroadcast] public int fu;
    /// <summary> 役满数，不包含累计役满与奖励役满 </summary>
    [RabiBroadcast] public int yakuman;
    /// <summary> 奖励役满数，计入点数但不满足番缚。与<see cref="han"/>和<see cref="yaku"/>的关系同理 </summary>
    [RabiBroadcast] public int bonusYakuman;
    /// <summary> 计入点数的役满总数 </summary>
    public int TotalYakuman => yakuman + bonusYakuman;
    /// <summary> 计分选项 </summary>
    public ScoringOption scoringOption = option;

    private CompactScore Compact => new(han, yaku, fu, yakuman, bonusYakuman);

    /// <summary> 基本点。青天井下饱和于 long.MaxValue 而非溢出，比较仍然精确 </summary>
    public long BaseScore => ScoreMath.BaseScore(Compact, scoringOption);

    [RabiBroadcast]
    public int KazoeYakuman {
      get {
        return !scoringOption.HasAllFlags(ScoringOption.Yakuman | ScoringOption.KazoeYakuman) ? 0 : han >= KAZOE_YAKUMAN_HAN ? 1 : 0;
      }
    }

    /// <summary>
    /// 役满数，为累计役满和单独役满的较大值
    /// </summary>
    [RabiBroadcast]
    public int FinalYakuman => scoringOption.HasAnyFlag(ScoringOption.Yakuman)
        ? Math.Max(
            scoringOption.HasAnyFlag(ScoringOption.MultipleYakuman) ? TotalYakuman : Math.Min(1, TotalYakuman),
            KazoeYakuman) : 0;

    /// <summary> 是否满足番缚。奖励番与奖励役满（宝牌、八连荘）不计入 </summary>
    public bool IsValid(int minHan) {
      return yaku + (yakuman * KAZOE_YAKUMAN_HAN) >= minHan;
    }

    public int CompareTo(ScoreCalcResult other) {
      if (scoringOption == other.scoringOption) {
        return ScoreMath.Compare(Compact, other.Compact, scoringOption);
      }
      int score = BaseScore.CompareTo(other.BaseScore);
      return score != 0 ? score : han != other.han ? han.CompareTo(other.han) : fu.CompareTo(other.fu);
    }

    public static bool operator <(ScoreCalcResult lhs, ScoreCalcResult rhs) {
      return lhs.CompareTo(rhs) < 0;
    }

    public static bool operator >(ScoreCalcResult lhs, ScoreCalcResult rhs) {
      return lhs.CompareTo(rhs) > 0;
    }

    public ScoreCalcResultMsg ToProto() {
      return new ScoreCalcResultMsg {
        Han = han,
        Yaku = yaku,
        Fu = fu,
        Yakuman = yakuman,
        BonusYakuman = bonusYakuman,
        KazoeYakuman = KazoeYakuman,
        FinalYakuman = FinalYakuman,
      };
    }
  }
}
