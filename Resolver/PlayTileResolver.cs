using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Resolver {
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
            output.Add(new ChooseTileAction(player, tiles));
            return true;
        }
    }
}
