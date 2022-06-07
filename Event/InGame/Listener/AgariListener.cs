using RabiRiichi.Core.Config;
using RabiRiichi.Util;
using System.Linq;
using System.Threading.Tasks;

namespace RabiRiichi.Event.InGame.Listener {
    public static class AgariListener {
        public static Task ExecuteAgari(AgariEvent ev) {
            foreach (var info in ev.agariInfos) {
                ev.game.GetPlayer(info.playerId).hand.agariTile = ev.agariInfos.incoming;
            }
            var calcScoreEv = new CalcScoreEvent(ev, ev.agariInfos);
            ev.Q.Queue(calcScoreEv);
            var applyScoreEv = new ApplyScoreEvent(calcScoreEv, calcScoreEv.scoreChange);
            ev.Q.Queue(applyScoreEv);
            bool switchDealer = true;
            if (ev.game.config.continuationOption.HasAnyFlag(ContinuationOption.RenchanOnDealerWin)) {
                switchDealer = ev.agariInfos.All(info => info.playerId != ev.game.info.dealer);
            }
            ev.Q.Queue(new ConcludeGameEvent(applyScoreEv, switchDealer, false));
            return Task.CompletedTask;
        }

        public static void Register(EventBus eventBus) {
            eventBus.Subscribe<AgariEvent>(ExecuteAgari, EventPriority.Execute);
        }
    }
}