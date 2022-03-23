using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class ApplyScoreListener {
        public static Task ApplyScore(ApplyScoreEvent ev) {
            var scoreChange = ev.scoreChange;
            foreach (var player in ev.game.players) {
                player.points += scoreChange.DeltaScore(player.id);
            }
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<ApplyScoreEvent>(ApplyScore, EventPriority.Execute);
        }
    }
}