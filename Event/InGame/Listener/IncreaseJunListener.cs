using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class IncreaseJunListener {
        public static Task PrepareJun(IncreaseJunEvent ev) {
            ev.increasedJun = ev.player.hand.jun + 1;
            return Task.CompletedTask;
        }

        public static Task IncreaseJun(IncreaseJunEvent ev) {
            ev.player.hand.jun = ev.increasedJun;
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<IncreaseJunEvent>(PrepareJun, EventPriority.Prepare);
            eventBus.Register<IncreaseJunEvent>(IncreaseJun, EventPriority.Execute);
        }
    }
}