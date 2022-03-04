using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class RevealDoraListener {
        public static Task PrepareDora(RevealDoraEvent e) {
            int drawCount = 0;
            if (e.dora.IsEmpty) {
                drawCount++;
            }
            if (e.uraDora.IsEmpty) {
                drawCount++;
            }
            // 从牌山随机选取牌
            var yama = e.game.wall;
            var tiles = yama.Select(drawCount);
            // 分配抽到的牌为宝牌或里宝
            if (e.dora.IsEmpty) {
                e.dora = tiles[0];
                tiles.RemoveAt(0);
            }
            if (e.uraDora.IsEmpty) {
                e.uraDora = tiles[0];
                tiles.RemoveAt(0);
            }
            return Task.CompletedTask;
        }

        public static Task RevealDora(RevealDoraEvent e) {
            e.game.wall.RevealDora(e.dora, e.uraDora);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<RevealDoraEvent>(PrepareDora, EventPriority.Execute);
            eventBus.Register<RevealDoraEvent>(RevealDora, EventPriority.After);
        }
    }
}