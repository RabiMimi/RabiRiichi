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
            foreach (var player in e.game.players) {
                bus.Queue(new DealHandEvent(e.game, player));
            }
            bus.Queue(new RevealDoraEvent(e.game));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
            eventBus.Register<BeginGameEvent>(AfterUpdateInfo, EventPriority.After);
        }
    }
}