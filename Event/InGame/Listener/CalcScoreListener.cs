using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class CalcScoreListener {
        public static Task CalcScore(CalcScoreEvent ev) {
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<CalcScoreEvent>(CalcScore, EventPriority.Execute);
        }
    }
}