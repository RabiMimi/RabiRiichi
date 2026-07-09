using RabiRiichi.Core;
using RabiRiichi.Core.Config;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 判定是否可以拔北（三麻北风处理）。
  /// </summary>
  public class NukiDoraResolver : ResolverBase {
    protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
      // 只能在自己摸牌后拔北
      if (incoming.IsTsumo) {
        yield return player;
      }
    }

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      var game = player.game;
      if (!game.config.doraOption.HasFlag(DoraOption.Nukidora)) {
        return false;
      }
      var wall = game.wall;
      // 拔北需要从岭上补牌，海底或岭上耗尽时不可拔北
      if (wall.IsHaitei || wall.rinshan.Count == 0) {
        return false;
      }

      var hand = player.hand;
      // 立直后只能拔刚摸到的北（不能动被锁定的手牌）
      var candidates = hand.riichi
          ? (incoming.tile.IsSame(Tile.North) ? [incoming] : new List<GameTile>())
          : CollectNorths(hand, incoming);
      if (candidates.Count == 0) {
        return false;
      }

      output.Add(new NukiDoraAction(player.id, candidates.Select(t => new List<GameTile> { t }).ToList()));
      return true;
    }

    /// <summary> 收集可拔的北：刚摸到的北 + 手牌里的北 </summary>
    private static List<GameTile> CollectNorths(Hand hand, GameTile incoming) {
      var result = new List<GameTile>();
      if (incoming.tile.IsSame(Tile.North)) {
        result.Add(incoming);
      }
      result.AddRange(hand.freeTiles.Where(t => t.tile.IsSame(Tile.North)));
      return result;
    }
  }
}
