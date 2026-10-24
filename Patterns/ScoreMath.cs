using RabiRiichi.Core.Config;
using RabiRiichi.Patterns.Compiled;
using System;
using System.Numerics;

namespace RabiRiichi.Patterns {
  internal static class ScoreMath {
    private const int KazoeHan = ScoreCalcResult.KAZOE_YAKUMAN_HAN;

    // The wire format and player balances are signed 64-bit values. Search
    // comparisons remain exact; values crossing that boundary saturate.
    internal static long Clamp(BigInteger value) => value > long.MaxValue ? long.MaxValue
        : value < long.MinValue ? long.MinValue : (long)value;

    internal static long Multiply(long value, int factor) => Clamp((BigInteger)value * factor);

    internal static long BaseScore(CompactScore s, ScoringOption option) {
      int yakuman = s.Yakuman + s.BonusYakuman;
      if ((option & ScoringOption.Yakuman) == 0) {
        long exponent = (long)s.Han + (long)KazoeHan * yakuman + 2;
        if (s.Fu == 0) return 0;
        if (exponent >= 63 || exponent >= 0 && s.Fu > (long.MaxValue >> (int)exponent)) return long.MaxValue;
        return exponent < 0 ? s.Fu >> (int)Math.Min(63, -exponent) : (long)s.Fu << (int)exponent;
      }
      int final = (option & ScoringOption.MultipleYakuman) != 0 ? yakuman : Math.Min(1, yakuman);
      if ((option & ScoringOption.KazoeYakuman) != 0 && s.Han >= KazoeHan) final = Math.Max(final, 1);
      if (final > 0) return final * 8000L;
      if (s.Han >= 11) return 6000;
      if (s.Han >= 8) return 4000;
      if (s.Han >= 6) return 3000;
      if (s.Han >= 5) return 2000;
      long score = (long)s.Fu << (s.Han + 2);
      if ((option & ScoringOption.KiriageMangan) != 0 && score is > 1900 and < 2000) score = 2000;
      return Math.Min(2000, score);
    }

    // Compare fu * 2^exponent exactly without materializing huge integers.
    internal static int CompareUncapped(CompactScore a, CompactScore b) {
      if (a.Fu == 0 || b.Fu == 0) return a.Fu.CompareTo(b.Fu);
      long ae = (long)a.Han + (long)KazoeHan * (a.Yakuman + a.BonusYakuman) + 2;
      long be = (long)b.Han + (long)KazoeHan * (b.Yakuman + b.BonusYakuman) + 2;
      int ab = 32 - BitOperations.LeadingZeroCount((uint)a.Fu), bb = 32 - BitOperations.LeadingZeroCount((uint)b.Fu);
      int size = (ae + ab).CompareTo(be + bb);
      if (size != 0) return size;
      return ((ulong)a.Fu << (32 - ab)).CompareTo((ulong)b.Fu << (32 - bb));
    }

    internal static int Compare(CompactScore a, CompactScore b, ScoringOption option) {
      int score = (option & ScoringOption.Yakuman) == 0 ? CompareUncapped(a, b) : BaseScore(a, option).CompareTo(BaseScore(b, option));
      if (score != 0) return score;
      int han = a.Han.CompareTo(b.Han);
      return han != 0 ? han : a.Fu.CompareTo(b.Fu);
    }
  }
}
