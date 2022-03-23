using System.Threading.Tasks;

namespace RabiRiichi.Event.Query.Listener {
    public static class SyncGameStateListener {
        public static Task PrepareSyncGameState(SyncGameStateEvent ev) {
            return Task.CompletedTask;
        }

        public static void Register(EventBus bus) {
            bus.Register<SyncGameStateEvent>(PrepareSyncGameState, EventPriority.Prepare);
        }
    }
}