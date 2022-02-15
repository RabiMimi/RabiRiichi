using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.Listener {
    public static class DefaultDrawTile {
        public static Task PrepareTile(EventBase ev) {
            var e = (DrawTileEvent) ev;
            // ??? 啥意思
            int drawCount = e.type == DrawTileType.Wall ? 1 : 2;
            var yama = ev.game.wall;
            if (yama.NumRemaining < drawCount) {
                ev.Cancel();
                return Task.CompletedTask;
            }
            var tiles  = ev.game.rand.Choice(yama.remaining, drawCount);
            e.tile = tiles[0];
            if (drawCount > 1) {
                e.doraIndicator = tiles[1];
            }
            return Task.CompletedTask;
        }

        public static Task DrawTile(EventBase ev) {
            var e = (DrawTileEvent) ev;
            e.game.wall.Draw(e.tile);
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
                // await ev.game.actionManager.OnDrawTile(player.hand, incoming, false);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(PrepareTile, Priority.Prepare);
        }
    }
}
