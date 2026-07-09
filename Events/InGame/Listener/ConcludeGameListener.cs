using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
  public static class ConcludeGameListener {
    public static Task PrepareConcludeGame(ConcludeGameEvent ev) {
      ev.doras.AddRange(ev.game.wall.doras.Take(ev.game.wall.revealedDoraCount));

      bool anyWinnerHasRiichi = false;
      foreach (var player in ev.game.players) {
        if (player.hand.agariTile != null && player.hand.riichi) {
          anyWinnerHasRiichi = true;
          break;
        }
      }

      if (anyWinnerHasRiichi) {
        ev.uradoras.AddRange(ev.game.wall.uradoras.Take(ev.game.wall.revealedUradoraCount));
      }
      return Task.CompletedTask;
    }

    public static Task ExecuteConcludeGame(ConcludeGameEvent ev) {
      ev.Q.Queue(new NextGameEvent(ev));
      return Task.CompletedTask;
    }

    public static void Register(EventBus eventBus) {
      eventBus.Subscribe<ConcludeGameEvent>(PrepareConcludeGame, EventPriority.Prepare);
      eventBus.Subscribe<ConcludeGameEvent>(ExecuteConcludeGame, EventPriority.Execute);
    }
  }
}