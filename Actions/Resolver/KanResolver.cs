using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Actions.Resolver {
  /// <summary>
  /// 判定是否能杠
  /// </summary>
  public class KanResolver : ResolverBase {
    protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile tile) {
      if (tile.IsTsumo) {
        // 暗杠或加杠
        yield return player;
      } else {
        // 来自别的玩家的牌，大明杠
        foreach (var p in player.game.players.Where(p => !p.SamePlayer(player))) {
          yield return p;
        }
      }
    }

    protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
      var wall = player.game.wall;
      if (wall.IsHaitei || wall.rinshan.Count == 0) {
        return false;
      }
      var hand = player.hand;
      if (!incoming.IsTsumo && hand.riichi) {
        return false;
      }
      var tile = incoming.tile.WithoutDora;
      var result = new List<List<GameTile>>();

      // 大明杠或暗杠
      CheckCombo(hand.freeTiles, result, [incoming], tile, tile, tile);
      if (incoming.IsTsumo) {
        // 加杠
        var groups = hand.called.OfType<Kou>();
        foreach (var group in groups) {
          var groupKey = group.First.tile;
          if (incoming.tile.IsSame(groupKey)) {
            result.Add([.. group, incoming]);
          }
          foreach (var handTile in hand.freeTiles.Where(t => t.tile.IsSame(groupKey))) {
            result.Add([.. group, handTile]);
          }
        }
        // 暗杠
        var grs = hand.freeTiles.GroupBy(t => t.tile.WithoutDora);
        foreach (var gr in grs) {
          if (gr.Count() >= 4) {
            var list = gr.ToList();
            var key = gr.Key;
            CheckCombo(list, result, [], key, key, key, key);
          }
        }
      }

      if (hand.riichi) {
        // 立直后只能暗杠，且必须满足：(a) 杠的是刚摸到的这张牌，
        // (b) 杠后听牌不变（不能改变听口，更不能因此不听）。
        result.RemoveAll(gameTiles => !IsValidRiichiKan(hand, gameTiles, incoming));
      }

      result.RemoveAll(tiles => !HasMoveAfterClaim(hand.freeTiles, player.game.config, tiles, incoming));

      if (result.Count == 0) {
        return false;
      }
      output.Add(new KanAction(player.id, result, -incoming.discardInfo?.fromPlayer.Dist(player) ?? 0));
      return true;
    }

    /// <summary>
    /// 立直后暗杠是否合法：必须包含刚摸到的牌，且杠后听牌完全不变。
    /// </summary>
    private static bool IsValidRiichiKan(Hand hand, List<GameTile> gameTiles, GameTile incoming) {
      // 只能杠刚摸到的这张牌（不能杠手里原有的四张）。
      if (!gameTiles.Any(t => t.IsSame(incoming))) {
        return false;
      }

      var tenpai = hand.Tenpai;

      // 模拟暗杠：incoming 尚未加入 freeTiles，将其余三张从 freeTiles 移除，
      // 并把这个杠子作为暗杠加入副露，使 hand.Count 仍为 13 以计算听牌。
      var removed = gameTiles.Where(t => t != incoming).ToList();
      hand.freeTiles.RemoveAll(removed.Contains);
      var kan = new Kan(gameTiles, TileSource.Ankan);
      hand.called.Add(kan);

      // 立直中手牌必然听牌，tenpai 非空；若杠后不再听牌，newTenpai 为空，
      // SequenceEqual 自然为 false，因此无需额外判空。
      var newTenpai = hand.Tenpai;
      bool validKan = tenpai.SequenceEqual(newTenpai);

      // 还原
      hand.called.Remove(kan);
      hand.freeTiles.AddRange(removed);

      return validKan;
    }
  }
}