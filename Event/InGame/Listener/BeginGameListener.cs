using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent ev) {
            ev.game.info.round = ev.round;
            ev.game.info.banker = ev.banker;
            ev.game.info.honba = ev.honba;
            ev.game.info.Reset();
            foreach (var player in ev.game.players) {
                player.Reset();
            }
            var bus = ev.bus;
            int banker = ev.game.info.banker;
            for (int i = 0; i < ev.game.config.playerCount; i++) {
                int playerId = (i + banker) % ev.game.config.playerCount;
                bus.Queue(new DealHandEvent(ev, playerId));
            }
            bus.Queue(new RevealDoraEvent(ev));
            bus.Queue(new IncreaseJunEvent(ev, banker));
            bus.Queue(new BankerFirstTurnEvent(ev, banker));
            return Task.CompletedTask;
        }


        public static void Register(EventBus eventBus) {
            eventBus.Register<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
        }
    }
}