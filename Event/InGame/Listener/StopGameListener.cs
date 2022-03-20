using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class StopGameListener {
        public static Task ExecuteStopGame(StopGameEvent ev) {
            var banker = ev.game.Banker;
            banker.points += ev.game.info.riichiStick * ev.game.config.riichiPoints;
            ev.game.info.riichiStick = 0;
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<StopGameEvent>(ExecuteStopGame, EventPriority.Execute);
        }
    }
}