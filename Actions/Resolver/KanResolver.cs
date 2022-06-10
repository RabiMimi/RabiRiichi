using RabiRiichi.Core;
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
                foreach (var p in player.game.players.Where(p => !p.SamePlayer(player)))
                    yield return p;
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
            CheckCombo(hand.freeTiles, result, new List<GameTile> { incoming }, tile, tile, tile);
            if (hand.riichi) {
                var tenpai = hand.Tenpai;
                // 立直时，检查暗杠不会影响听牌
                result.RemoveAll((gameTiles) => {
                    // 假装杠了
                    hand.freeTiles.RemoveAll(tile => gameTiles.Contains(tile));
                    var kan = new Kan(gameTiles, TileSource.AnKan);
                    hand.called.Add(kan);
                    // 检查听牌
                    var newTenpai = hand.Tenpai;
                    bool validKan = tenpai.SequenceEqual(newTenpai);
                    // 还原
                    hand.called.Remove(kan);
                    hand.freeTiles.AddRange(gameTiles.Where(tile => tile != incoming));
                    return !validKan;
                });
            }
            if (incoming.IsTsumo) {
                // 加杠
                var groups = hand.called.OfType<Kou>();
                foreach (var group in groups) {
                    var groupKey = group.First.tile;
                    if (incoming.tile.IsSame(groupKey)) {
                        result.Add(group.Append(incoming).ToList());
                    }
                    foreach (var handTile in hand.freeTiles.Where(t => t.tile.IsSame(groupKey))) {
                        result.Add(group.Append(handTile).ToList());
                    }
                }
                // 暗杠
                var grs = hand.freeTiles.GroupBy(t => t.tile.WithoutDora);
                foreach (var gr in grs) {
                    if (gr.Count() >= 4) {
                        var list = gr.ToList();
                        var key = gr.Key;
                        CheckCombo(list, result, new List<GameTile>(), key, key, key, key);
                    }
                }
            }

            result.RemoveAll(tiles => !HasMoveAfterClaim(hand.freeTiles, player.game.config, tiles, incoming));

            if (result.Count == 0) {
                return false;
            }
            output.Add(new KanAction(player.id, result, -incoming.discardInfo?.fromPlayer.Dist(player) ?? 0));
            return true;
        }
    }
}
