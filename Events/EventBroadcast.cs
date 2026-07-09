using RabiRiichi.Communication;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events {
  public static class EventBroadcast {
    public static Task Send(EventBase ev) {
      // Tee every event exactly once for full-game capture (e.g. replay),
      // before the per-player filtering below drops [RabiPrivate] events for
      // non-owner seats.
      ev.game.onGodViewEvent?.Invoke(ev);

      var players = ev.game.players.AsEnumerable();
      if (ev is IRabiPlayerMessage msg && msg.GetType().Has<RabiPrivateAttribute>()) {
        players = players.Where(p => p.id == msg.playerId);
      }
      foreach (var player in players) {
        ev.game.SendEvent(player.id, ev);
      }
      return Task.CompletedTask;
    }

    public static void Register(EventBus eventBus) {
      eventBus.Subscribe<EventBase>(Send, EventPriority.Broadcast);
    }
  }
}