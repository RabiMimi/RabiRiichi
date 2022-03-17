using RabiRiichi.Riichi;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class NextPlayerListener {
        public static Task PreparePlayer(NextPlayerEvent ev) {
            if (ev.game.wall.IsHaitei) {
                ev.nextPlayerId = -1;
            } else {
                ev.nextPlayerId = ev.player.NextPlayerId;
            }
            return Task.CompletedTask;
        }

        public static Task NextPlayer(NextPlayerEvent ev) {
            if (ev.nextPlayerId < 0) {
                // TODO: Implement Ryuukyoku
                ev.bus.Queue(new RyuukyokuEvent(ev.game));
            } else {
                ev.bus.Queue(new IncreaseJunEvent(ev.game, ev.nextPlayerId));
                ev.bus.Queue(new DrawTileEvent(ev.game, ev.nextPlayerId, TileSource.Wall, DiscardReason.Draw));
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<NextPlayerEvent>(PreparePlayer, EventPriority.Prepare);
            eventBus.Register<NextPlayerEvent>(NextPlayer, EventPriority.Execute);
        }
    }
}