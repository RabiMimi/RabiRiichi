using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent ev) {
            ev.game.info.round = ev.round;
            ev.game.info.dealer = ev.dealer;
            ev.game.info.honba = ev.honba;
            ev.game.info.Reset();
            foreach (var player in ev.game.players) {
                player.Reset();
            }
            int dealer = ev.game.info.dealer;
            for (int i = 0; i < ev.game.config.playerCount; i++) {
                int playerId = (i + dealer) % ev.game.config.playerCount;
                ev.Q.Queue(new DealHandEvent(ev, playerId));
            }
            ev.Q.Queue(new RevealDoraEvent(ev));
            ev.Q.Queue(new IncreaseJunEvent(ev, dealer));
            ev.Q.Queue(new DealerFirstTurnEvent(ev, dealer));
            return Task.CompletedTask;
        }


        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
        }
    }
}