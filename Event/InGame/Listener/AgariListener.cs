using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class AgariListener {
        public static Task ExecuteAgari(AgariEvent ev) {
            foreach (var info in ev.agariInfos) {
                ev.game.GetPlayer(info.playerId).hand.agari = true;
            }
            ev.game.eventBus.Queue(new CalcScoreEvent(ev.game, ev.agariInfos));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<AgariEvent>(ExecuteAgari, EventPriority.Execute);
        }
    }
}