using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    class DftPostDrawTile : ListenerBase {
        public override uint CanListen(EventBase ev) => Priority.Default;

        public override async Task<bool> Handle(EventBase ev) {
            var e = (DrawTileEvent) ev;
            Debug.Assert(e.game.wall.Draw(e.tile));
            var player = e.player;
            var incoming = new Riichi.GameTile {
                tile = e.tile,
                source = Riichi.TileSource.Wall,
            };
            player.hand.Add(incoming);
            if (e.doraIndicator.IsValid) {
                e.game.wall.RevealDoraIndicator(e.doraIndicator);
                if (e.type == DrawTileType.CloseRinshan) {
                    // TODO: Log
                    // await e.game.SendPublic($"新的宝牌指示牌：{e.doraIndicator}");
                }
            } else {
                await ev.game.actionManager.OnDrawTile(player.hand, incoming, false);
            }
            return false;
        }
    }
}
