using HoshinoSharp.Hoshino.Message;
using RabiRiichi.Event;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System;
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
            var tileStr = tiles.Select(tile => tile.ToString()).ToList();
            var str = string.Join("/", tileStr);
            output = new PlayerActions() {
                new PlayerAction {
                    priority = PlayerAction.Priority.PLAY,
                    player = hand.player,
                    options = tileStr,
                    msg = new HMessage($"{str}：切牌"),
                    trigger = (PlayerAction action) => {
                        hand.game.eventBus.Queue(new PlayTileEvent {
                            player = hand.player,
                            tile = hand.GetTile(tiles[action.choice]),
                            riichi = false,
                        });
                    }
                }
            };
            return true;
        }
    }
}
