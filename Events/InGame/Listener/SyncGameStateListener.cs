using RabiRiichi.Communication.Sync;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class SyncGameStateListener {
        public static Task AddGameState(SyncGameStateEvent ev) {
            ev.gameState = new GameState(ev.game, ev.playerId);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SyncGameStateEvent>(AddGameState, EventPriority.Execute);
        }
    }
}