using System.Collections.Generic;
using RabiRiichi.Action;
using RabiRiichi.Riichi;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 生成切牌表
    /// </summary>
    public class PlayTileResolver : ResolverBase {
        public override bool ResolveAction(Hand hand, GameTile incoming, MultiPlayerAction output) {
            var tiles = new List<GameTile>();
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
            output.Add(new ChooseTileAction(hand.player, tiles));
            return true;
        }
    }
}
