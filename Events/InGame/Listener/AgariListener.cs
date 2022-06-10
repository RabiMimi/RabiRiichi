using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Events.InGame.Listener {
    public static class AgariListener {
        public static Task ExecuteAgari(AgariEvent ev) {
            foreach (var info in ev.agariInfos) {
                ev.game.GetPlayer(info.playerId).hand.agariTile = ev.agariInfos.incoming;
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