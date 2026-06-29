using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
  public static class AgariListener {
    public static Task ExecuteAgari(AgariEvent ev) {
      foreach (var info in ev.agariInfos) {
        var hand = ev.game.GetPlayer(info.playerId).hand;
        hand.agariTile = ev.agariInfos.incoming;
        // Persist the score breakdown so it survives past this transient event
        // and can be included in the sync snapshot (e.g. for reconnects on the
        // result screen).
        hand.agariScore = info.scores;
      }
      var calcScoreEv = new CalcScoreEvent(ev, ev.agariInfos);
      ev.Q.Queue(calcScoreEv);
      var applyScoreEv = new ApplyScoreEvent(calcScoreEv, calcScoreEv.scoreChange);
      ev.Q.Queue(applyScoreEv);
      ev.Q.Queue(new ConcludeGameEvent(applyScoreEv, ConcludeGameReason.Agari));
      return Task.CompletedTask;
    }

    public static void Register(EventBus eventBus) {
      eventBus.Subscribe<AgariEvent>(ExecuteAgari, EventPriority.Execute);
    }
  }
}