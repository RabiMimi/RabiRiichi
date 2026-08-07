using RabiRiichi.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Patterns {

  /// <summary> 不塔的底和：起手时任意两张牌都不成对子/搭子/面子，最多允许一组对子 </summary>
  public class BaseButa : BasePattern {
    /// <summary> 两张牌是否能组成对子/搭子/面子的一部分 </summary>
    private static bool AreRelated(Tile lhs, Tile rhs) {
      return lhs.IsSame(rhs)
          || (lhs.IsMPS && lhs.Suit == rhs.Suit && Math.Abs(lhs.Num - rhs.Num) <= 2);
    }

    /// <summary> 是否处于起手。开杠和拔北都会推进巡目，故 IsFirstJun 已涵盖 </summary>
    private static bool IsFirstDraw(Hand hand, GameTile incoming) {
      return incoming != null && incoming.IsTsumo && hand.game.IsFirstJun;
    }

    /// <inheritdoc/>
    public override bool Resolve(Hand hand, GameTile incoming, out List<List<MenLike>> output) {
      output = null;
      if (hand.Count != (incoming == null ? Game.HAND_SIZE + 1 : Game.HAND_SIZE)) {
        return false;
      }
      if (hand.called.Count > 0 || !IsFirstDraw(hand, incoming)) {
        return false;
      }

      var buckets = GetTileGroups(hand, incoming, true).GetAllBuckets().ToList();
      // 最多一组对子，且不能有刻子/杠子
      if (buckets.Count(gr => gr.Count > 1) > 1 || buckets.Any(gr => gr.Count > 2)) {
        return false;
      }
      // 任意两种牌之间都不能有关联
      var distinctTiles = buckets.ConvertAll(gr => gr[0].tile);
      for (int i = 0; i < distinctTiles.Count; i++) {
        for (int j = i + 1; j < distinctTiles.Count; j++) {
          if (AreRelated(distinctTiles[i], distinctTiles[j])) {
            return false;
          }
        }
      }

      output = [buckets.ConvertAll(MenLike.From)];
      return true;
    }

    /// <inheritdoc/>
    public override int Shanten(Hand hand, GameTile incoming, out Tiles output, int maxShanten = 13) {
      // 不塔只在起手成立，无法向它进张
      return Reject(out output);
    }
  }
}
