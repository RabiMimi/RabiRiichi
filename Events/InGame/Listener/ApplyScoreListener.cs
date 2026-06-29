using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
  public static class ApplyScoreListener {
    public static Task ApplyScore(ApplyScoreEvent ev) {
      var scoreChange = ev.scoreChange;
      foreach (var player in ev.game.players) {
        var delta = scoreChange.DeltaScore(player.id);
        player.points += delta;
        // Persist the net settlement change (agari or ryuukyoku) so the result
        // screen can be rebuilt on reconnect (the transfer list is otherwise
        // discarded).
        player.hand.pointDelta += delta;
      }
      return Task.CompletedTask;
    }

    public static void Register(EventBus eventBus) {
      eventBus.Subscribe<ApplyScoreEvent>(ApplyScore, EventPriority.Execute);
    }
  }
}