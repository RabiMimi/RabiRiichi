using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class SetRiichiListener {
        public static Task ExecuteRiichi(SetRiichiEvent ev) {
            ev.player.hand.Riichi(ev.riichiTile, ev.wRiichi);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SetRiichiEvent>(ExecuteRiichi, EventPriority.Execute);
        }
    }
}