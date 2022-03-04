using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    /// TODO: 写的啥东西 等会儿鲨了重写
    public static class DefaultDrawTile {
        public static Task PrepareTile(DrawTileEvent e) {
            // ??? 啥意思
            int drawCount = e.type == DrawTileType.Wall ? 1 : 2;
            var yama = e.game.wall;
            if (yama.NumRemaining < drawCount) {
                e.Cancel();
                return Task.CompletedTask;
            }
            var tiles = e.game.rand.Choice(yama.remaining, drawCount);
            e.tile = tiles[0];
            if (drawCount > 1) {
                e.doraIndicator = tiles[1];
            }
            return Task.CompletedTask;
        }

        public static Task DrawTile(DrawTileEvent e) {
            e.game.wall.Draw(e.tile);
            var player = e.player;
            var incoming = new GameTile(e.tile) {
                source = TileSource.Wall,
            };
            player.hand.Add(incoming);
            if (e.doraIndicator.IsValid) {
                // e.game.wall.RevealDora(e.doraIndicator);
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
            eventBus.Register<DrawTileEvent>(PrepareTile, EventPriority.Prepare);
        }
    }
}
