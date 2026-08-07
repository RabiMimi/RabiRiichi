using RabiRiichi.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {
  /// <summary> 同一花色、序数成等差数列的刻子组判定，三連刻/跳牌刻/筋牌刻/四連刻共用 </summary>
  internal static class KouSeries {
    /// <summary> 是否存在<paramref name="count"/>个同花色刻子，序数公差为<paramref name="step"/> </summary>
    public static bool Exists(IEnumerable<MenLike> groups, int count, int step) {
      return groups
          .Where(gr => gr is Kou or Kan && gr.First.tile.IsMPS)
          .GroupBy(gr => gr.Suit)
          .Any(bySuit => {
            var nums = bySuit.Select(gr => (int)gr.First.tile.Num).ToHashSet();
            return nums.Any(start =>
                Enumerable.Range(0, count).All(i => nums.Contains(start + (i * step))));
          });
    }
  }
}
