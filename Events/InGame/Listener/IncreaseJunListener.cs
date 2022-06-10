using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
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
            eventBus.Subscribe<IncreaseJunEvent>(PrepareJun, EventPriority.Prepare);
            eventBus.Subscribe<IncreaseJunEvent>(IncreaseJun, EventPriority.Execute);
        }
    }
}