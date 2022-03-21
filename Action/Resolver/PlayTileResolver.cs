using RabiRiichi.Riichi;
using System.Collections.Generic;

namespace RabiRiichi.Action.Resolver {
    /// <summary>
    /// 生成切牌表
    /// </summary>
    public class PlayTileResolver : ResolverBase {
        protected override IEnumerable<Player> ResolvePlayers(Player player, GameTile incoming) {
            yield return player;
        }

        protected override bool ResolveAction(Player player, GameTile incoming, MultiPlayerInquiry output) {
            var tiles = new List<GameTile>();
            var hand = player.hand;
            if (incoming != null) {
                tiles.Add(incoming);
            }
            if (!hand.riichi) {
                tiles.AddRange(hand.freeTiles);
            }
            if (tiles.Count == 0) {
                return false;
            }
            tiles.Sort();
            output.Add(new PlayTileAction(player.id, tiles, incoming));
            return true;
        }
    }
}
