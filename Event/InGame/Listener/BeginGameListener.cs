using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent e) {
            e.game.info.wind = e.wind;
            e.game.info.round = e.round;
            e.game.info.honba = e.honba;
            e.game.info.Reset();
            return Task.CompletedTask;
        }

        public static Task AfterUpdateInfo(BeginGameEvent e) {
            var bus = e.game.eventBus;
            int banker = e.game.info.Banker;
            for (int i = 0; i < e.game.config.playerCount; i++) {
                int playerId = (i + banker) % e.game.config.playerCount;
                bus.Queue(new DealHandEvent(e.game, playerId));
            }
            bus.Queue(new RevealDoraEvent(e.game));
            bus.Queue(new IncreaseJunEvent(e.game, banker));
            bus.Queue(new DrawTileEvent(e.game, banker));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
            eventBus.Register<BeginGameEvent>(AfterUpdateInfo, EventPriority.After);
        }
    }
}