using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class BeginGameListener {
        public static Task UpdateGameInfo(BeginGameEvent e) {
            e.game.gameInfo.wind = e.wind;
            e.game.gameInfo.round = e.round;
            e.game.gameInfo.honba = e.honba;
            e.game.gameInfo.Clear();
            return Task.CompletedTask;
        }

        public static Task AfterUpdateInfo(BeginGameEvent e) {
            var bus = e.game.eventBus;
            foreach (var player in e.game.players) {
                bus.Queue(new DealHandEvent(e.game, player));
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<BeginGameEvent>(UpdateGameInfo, EventPriority.Execute);
            eventBus.Register<BeginGameEvent>(AfterUpdateInfo, EventPriority.After);
        }
    }
}