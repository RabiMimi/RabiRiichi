using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class AddTileListener {
        private static Task ExecuteAddTile(AddTileEvent ev) {
            ev.player.hand.Add(ev.incoming);
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<AddTileEvent>(ExecuteAddTile, EventPriority.Execute);
        }
    }
}