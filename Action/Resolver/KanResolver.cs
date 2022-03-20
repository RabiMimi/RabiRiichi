using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Action.Resolver {
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
            var current = new List<GameTile> { incoming };
            var result = new List<GameTiles>();

            // 大明杠或暗杠
            CheckCombo(hand.freeTiles, result, current, tile, tile, tile);
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
            // 加杠
            if (incoming.IsTsumo) {
                var groups = hand.called.Where(g => g is Kou && g.First.tile.IsSame(incoming.tile));
                foreach (var group in groups) {
                    current.AddRange(new GameTiles(group.Append(incoming)));
                }
            }

            if (result.Count == 0) {
                return false;
            }
            output.Add(new KanAction(player.id, result, incoming, -incoming.discardInfo!.fromPlayer.Dist(player)));
            return true;
        }
    }
}
