using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class StopGameListener {
        public static Task ExecuteStopGame(StopGameEvent ev) {
            var player = ev.game.players.MaxBy(player => player.points);
            player.points += ev.game.info.riichiStick * ev.game.config.riichiPoints;
            ev.game.info.riichiStick = 0;
            foreach (var p in ev.game.players) {
                p.hand.riichiStick = 0;
            }
            ev.endGamePoints.AddRange(ev.game.players.Select(player => player.points));
            ev.Q.Queue(new TerminateEvent(ev));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<StopGameEvent>(ExecuteStopGame, EventPriority.Execute);
        }
    }
}