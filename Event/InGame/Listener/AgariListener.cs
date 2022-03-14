using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class AgariListener {
        public static Task ExecuteAgari(AgariEvent ev) {
            ev.game.eventBus.Queue(new CalcScoreEvent(ev.game, ev.agariInfos));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<AgariEvent>(ExecuteAgari, EventPriority.Execute);
        }
    }
}