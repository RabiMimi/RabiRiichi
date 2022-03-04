using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class DrawTileListener {

        public static Task PrepareTile(DrawTileEvent e) {
            if (!e.tile.IsEmpty) {
                // 已经有牌了，不需要再摸牌
                return Task.CompletedTask;
            }
            // 从牌山随机选取一张牌
            var yama = e.game.wall;
            if (!yama.Has(1)) {
                e.Cancel();
                return Task.CompletedTask;
            }
            e.tile = yama.SelectOne();
            return Task.CompletedTask;
        }

        public static Task DrawTile(DrawTileEvent e) {
            e.game.wall.Draw(e.tile);
            var player = e.player;
            var incoming = new GameTile(e.tile) {
                source = e.source,
            };
            player.hand.Add(incoming);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<DrawTileEvent>(PrepareTile, EventPriority.Prepare);
        }
    }
}
