using RabiRiichi.Core;
using RabiRiichi.Generated.Core;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
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
                ev.Q.Queue(new EndGameRyuukyokuEvent(ev));
            } else {
                ev.Q.Queue(new IncreaseJunEvent(ev, ev.nextPlayerId));
                ev.Q.Queue(new DrawTileEvent(ev, ev.nextPlayerId, TileSource.Wall));
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<NextPlayerEvent>(PreparePlayer, EventPriority.Prepare);
            eventBus.Subscribe<NextPlayerEvent>(NextPlayer, EventPriority.Execute);
        }
    }
}