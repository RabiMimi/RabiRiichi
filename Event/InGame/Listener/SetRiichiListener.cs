using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SetRiichiListener {
        public static Task ExecuteRiichi(SetRiichiEvent ev) {
            ev.player.hand.Riichi(ev.riichiTile, ev.wRiichi);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Register<SetRiichiEvent>(ExecuteRiichi, EventPriority.Execute);
        }
    }
}