using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class RevealDoraListener {
        public static Task RevealDora(RevealDoraEvent e) {
            e.dora = e.game.wall.RevealDora(e.playerId >= 0);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<RevealDoraEvent>(RevealDora, EventPriority.Execute);
        }
    }
}