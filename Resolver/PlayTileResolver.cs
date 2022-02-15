using RabiRiichi.Event;
using RabiRiichi.Riichi;
using System.Linq;

namespace RabiRiichi.Resolver {
    /// <summary>
    /// 生成切牌表
    /// </summary>
    public class PlayTileResolver : ResolverBase {
        public override bool ResolveAction(Hand hand, GameTile incoming, out PlayerActions output) {
            var tiles = new Tiles();
            if (incoming != null) {
                tiles.Add(incoming.tile);
            }
            if (incoming == null || !hand.riichi) {
                tiles.AddRange(hand.hand.ToTiles());
            }
            if (tiles.Count <= 0 || !incoming.IsTsumo) {
                return Reject(out output);
            }
            tiles = new Tiles(tiles.Distinct());
            tiles.Sort();
            var tileStr = tiles.Select(tile => tile.ToString()).ToList();
            var str = string.Join("/", tileStr);
            output = new PlayerActions() {
                new PlayerAction {
                    priority = PlayerAction.Priority.PLAY,
                    player = hand.player,
                    options = tileStr,
                    trigger = (PlayerAction action) => {
                        /*
                        hand.game.eventBus.Queue(new PlayTileEvent {
                            player = hand.player,
                            tile = hand.GetTile(tiles[action.choice]),
                            riichi = false,
                        });*/
                    }
                }
            };
            return true;
        }
    }
}
