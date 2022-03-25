using RabiRiichi.Communication.Sync;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class SyncGameStateListener {
        public static Task AddGameState(SyncGameStateEvent ev) {
            ev.states["game_state"] = new GameState(ev.game, ev.playerId);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<SyncGameStateEvent>(AddGameState, EventPriority.Execute);
        }
    }
}