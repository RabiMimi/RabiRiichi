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
            ev.game.wall.Reset();
            int dealer = ev.game.info.dealer;
            int curPlayer = dealer;
            for (int r = 0; r < 4; r++) {
                for (int i = 0; i < ev.game.config.playerCount; i++) {
                    ev.Q.Queue(new DealHandEvent(ev, curPlayer, r == 3 ? 1 : 4));
                    curPlayer = ev.game.NextPlayerId(curPlayer);
                }
            }
            var lastDrawEv = new DealHandEvent(ev, curPlayer, 1);
            ev.Q.Queue(lastDrawEv);
            ev.Q.Queue(new RevealDoraEvent(ev));
            ev.Q.Queue(new IncreaseJunEvent(ev, dealer));
            lastDrawEv.OnFinish += () => {
                ev.Q.Queue(new DealerFirstTurnEvent(ev, dealer, lastDrawEv.tiles[0]));
            };
            return Task.CompletedTask;
        }


        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
        }
    }
}