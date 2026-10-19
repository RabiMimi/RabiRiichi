using RabiRiichi.Core;
using RabiRiichi.Core.Config;

namespace RabiRiichi.Patterns.Compiled {
  public enum CompactMeldKind : byte { Single, Pair, Sequence, Triplet, Quad }

  /// <summary>A resolved group. TileIndex is 0..33; sequences use their lowest tile.</summary>
  public readonly record struct CompactMeld(CompactMeldKind Kind, int TileIndex,
      bool IsClose, bool IsCalled = false) {
    public int Count => Kind switch {
      CompactMeldKind.Single => 1,
      CompactMeldKind.Pair => 2,
      CompactMeldKind.Quad => 4,
      _ => 3,
    };

    public static int Index(Tile tile) => ((int)tile.Suit - 1) * 9 + tile.Num - 1;
    public static Tile TileAt(int index) => new((TileSuit)(index / 9 + 1), index % 9 + 1);
  }

  /// <summary>Bit masks over the 34 natural tile indices, shared by scoring and its bounds.</summary>
  internal static class TileMasks {
    internal const ulong Honor = 127UL << 27;
    internal const ulong Terminal = (1UL << 0) | (1UL << 8) | (1UL << 9) | (1UL << 17) | (1UL << 18) | (1UL << 26);
    internal const ulong Green = (1UL << 19) | (1UL << 20) | (1UL << 21) | (1UL << 23) | (1UL << 25) | (1UL << 32);
    internal const ulong Red = (1UL << 18) | (1UL << 22) | (1UL << 24) | (1UL << 26) | (1UL << 33);
    internal const ulong Black = (1UL << 10) | (1UL << 12) | (1UL << 16) | (15UL << 27);
  }

  /// <summary>Allocation-free totals; yaku excludes bonus han.</summary>
  public readonly record struct CompactScore(int Han, int Yaku, int Fu, int Yakuman, int BonusYakuman) {
    public bool IsValid(int minHan) => Yaku + Yakuman * ScoreCalcResult.KAZOE_YAKUMAN_HAN >= minHan;

    public ScoreCalcResult ToResult(ScoringOption option) => new(option) {
      han = Han,
      yaku = Yaku,
      fu = Fu,
      yakuman = Yakuman,
      bonusYakuman = BonusYakuman,
    };
  }
}
